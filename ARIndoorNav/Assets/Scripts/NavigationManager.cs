using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// HoloNav NavigationManager — the central hub.
///
/// Responsibilities:
///   - Loads and owns the IndoorGraph
///   - Receives current node from NearestBeaconResolver
///   - Runs A* when start or destination changes
///   - Converts route nodes → AR world-space Vector3 points
///   - Feeds those points to ProceduralPathMesh, TurnMarkerPlacer, DestinationBeaconPlacer
///   - Handles multi-leg navigation (destination → new destination chaining)
///   - Detects arrival and notifies NavigationUIManager
///
/// SETUP:
///   1. Attach to a GameObject in your AR scene (e.g. a new "NavigationManager" GO).
///   2. Assign all Inspector references.
///   3. Place indoor_graph.json in Assets/Resources/
///   4. The anchorRoot is your existing AnchorRoot transform (set via ARTapToPlace or PlaceAnchorRoot).
/// </summary>
public class NavigationManager : MonoBehaviour
{
    // ── Inspector References ───────────────────────────────────────────────────
    [Header("AR Scene References")]
    [Tooltip("The transform placed on the floor by ARTapToPlace. All graph coords transform relative to this.")]
    public Transform anchorRoot;

    [Tooltip("Your existing ProceduralPathMesh script on PathMeshGenerator.")]
    public ProceduralPathMesh pathMesh;

    [Tooltip("Your existing TurnMarkerPlacer script on TurnMarkerSystem.")]
    public TurnMarkerPlacer turnMarkerPlacer;

    [Tooltip("Your existing DestinationBeaconPlacer script on DestinationBeaconSystem.")]
    public DestinationBeaconPlacer destinationBeaconPlacer;

    [Tooltip("NavigationUIManager for HUD updates.")]
    public NavigationUIManager uiManager;

    [Header("Graph Settings")]
    [Tooltip("Filename inside Resources/ folder, without extension.")]
    public string graphResourceName = "indoor_graph";

    [Tooltip("Y height of path points relative to anchor root (meters above floor).")]
    public float pathYOffset = 0.02f;

    [Header("Routing")]
    [Tooltip("How often (seconds) to re-run routing when position changes.")]
    public float routeUpdateInterval = 2.5f;

    // ── Singleton ──────────────────────────────────────────────────────────────
    public static NavigationManager Instance { get; private set; }

    // ── State ──────────────────────────────────────────────────────────────────
    private IndoorGraph _graph;
    private string _currentNodeId   = null;
    private string _destinationId   = null;
    private string _lastRoutedStart = null;
    private string _lastRoutedGoal  = null;
    private bool   _isNavigating    = false;
    private bool   _anchorPlaced    = false;

    // ── Unity Lifecycle ────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        LoadGraph();

        if (NearestBeaconResolver.Instance != null)
            NearestBeaconResolver.Instance.OnNodeChanged += OnNodeChanged;

        InvokeRepeating(nameof(TryUpdateRoute), 2f, routeUpdateInterval);
    }

    private void OnDestroy()
    {
        if (NearestBeaconResolver.Instance != null)
            NearestBeaconResolver.Instance.OnNodeChanged -= OnNodeChanged;
        CancelInvoke();
    }

    // ── Graph Loading ──────────────────────────────────────────────────────────
    private void LoadGraph()
    {
        TextAsset jsonAsset = Resources.Load<TextAsset>(graphResourceName);
        if (jsonAsset == null)
        {
            Debug.LogError($"[NavigationManager] Could not load '{graphResourceName}' from Resources/. " +
                           "Make sure indoor_graph.json is in Assets/Resources/.");
            return;
        }

        _graph = JsonUtility.FromJson<IndoorGraph>(jsonAsset.text);
        _graph.Build();
        Debug.Log($"[NavigationManager] Graph loaded: {_graph.nodes.Count} nodes, {_graph.edges.Count} edges.");
    }

    // ── Public API (called by UI and ARTapToPlace) ─────────────────────────────

    /// <summary>
    /// Called by ARTapToPlace after the anchor is placed on the floor.
    /// This unlocks routing and path drawing.
    /// </summary>
    public void OnAnchorPlaced(Transform placedAnchor)
    {
        anchorRoot = placedAnchor;
        _anchorPlaced = true;
        Debug.Log("[NavigationManager] Anchor placed. Ready to navigate.");
        uiManager?.OnAnchorReady();
    }

    /// <summary>
    /// Start navigating to a destination. Safe to call mid-navigation for chaining.
    /// </summary>
    public void StartNavigation(string destinationNodeId)
    {
        if (_graph == null) { Debug.LogError("[NavigationManager] Graph not loaded."); return; }
        if (!_graph.HasNode(destinationNodeId))
        {
            Debug.LogError($"[NavigationManager] Destination '{destinationNodeId}' not in graph.");
            return;
        }

        _destinationId  = destinationNodeId;
        _isNavigating   = true;
        _lastRoutedStart = null; // force re-route
        _lastRoutedGoal  = null;

        string label = _graph.GetNode(destinationNodeId)?.label ?? destinationNodeId;
        Debug.Log($"[NavigationManager] Navigation started → {label}");
        uiManager?.OnNavigationStarted(destinationNodeId, label);

        TryUpdateRoute();
    }

    /// <summary>
    /// Stop navigation, clear visuals, return to destination picker.
    /// </summary>
    public void StopNavigation()
    {
        _isNavigating  = false;
        _destinationId = null;
        ClearVisuals();
        uiManager?.OnNavigationStopped();
        Debug.Log("[NavigationManager] Navigation stopped.");
    }

    /// <summary>
    /// Returns all destination nodes for populating the UI picker.
    /// </summary>
    public List<GraphNode> GetDestinations()
    {
        return _graph?.GetDestinationNodes() ?? new List<GraphNode>();
    }

    public bool IsNavigating => _isNavigating;

    // ── Routing Logic ──────────────────────────────────────────────────────────
    private void OnNodeChanged(string newNodeId, string label)
    {
        _currentNodeId = newNodeId;
        uiManager?.UpdateCurrentLocation(label ?? "Unknown");

        if (_isNavigating) TryUpdateRoute();
    }

    private void TryUpdateRoute()
    {
        if (!_isNavigating || !_anchorPlaced) return;

        string currentNode = NearestBeaconResolver.Instance?.GetCurrentNodeId() ?? _currentNodeId;

        if (currentNode == null)
        {
            uiManager?.ShowLocating();
            ClearVisuals();
            return;
        }

        // Check arrival
        if (currentNode == _destinationId)
        {
            HandleArrival();
            return;
        }

        // Skip re-routing if nothing changed
        if (currentNode == _lastRoutedStart && _destinationId == _lastRoutedGoal)
            return;

        _lastRoutedStart = currentNode;
        _lastRoutedGoal  = _destinationId;

        // Run A*
        List<GraphNode> route = AStarRouter.FindPath(_graph, currentNode, _destinationId);

        if (route == null || route.Count < 2)
        {
            Debug.LogWarning($"[NavigationManager] No route from {currentNode} to {_destinationId}.");
            ClearVisuals();
            return;
        }

        // Convert graph coords → AR world space
        List<Vector3> worldPoints = GraphNodesToWorldPoints(route);

        // Feed to existing visual scripts
        pathMesh?.SetPath(worldPoints);
        turnMarkerPlacer?.GenerateTurnMarkers();
        destinationBeaconPlacer?.GenerateDestinationBeacon();

        Debug.Log($"[NavigationManager] Route drawn: {route.Count} nodes, {worldPoints.Count} points.");
    }

    private void HandleArrival()
    {
        _isNavigating = false;
        ClearVisuals();

        string label = _graph.GetNode(_destinationId)?.label ?? _destinationId;
        Debug.Log($"[NavigationManager] Arrived at {label}!");
        uiManager?.OnArrived(_destinationId, label);
    }

    // ── Coordinate Conversion ──────────────────────────────────────────────────
    /// <summary>
    /// Converts graph-local (x, z) node coords into AR world-space Vector3 positions
    /// using the anchorRoot transform as the spatial origin.
    /// </summary>
    private List<Vector3> GraphNodesToWorldPoints(List<GraphNode> nodes)
    {
        var worldPoints = new List<Vector3>();
        foreach (var node in nodes)
        {
            // Graph coords are flat (x, z). Y is controlled by pathYOffset.
            Vector3 localPos = new Vector3(node.x, pathYOffset, node.z);
            Vector3 worldPos = anchorRoot.TransformPoint(localPos);
            worldPoints.Add(worldPos);
        }
        return worldPoints;
    }

    // ── Visuals ────────────────────────────────────────────────────────────────
    private void ClearVisuals()
    {
        // Clear path mesh
        pathMesh?.SetPath(new List<Vector3>());
        turnMarkerPlacer?.ClearMarkers();
        destinationBeaconPlacer?.ClearBeacon();
    }
}
