using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central hub: loads graph, runs A*, converts coords to AR world space,
/// drives ProceduralPathMesh / TurnMarkerPlacer / DestinationBeaconPlacer,
/// detects arrival, supports multi-leg chaining.
/// </summary>
public class NavigationManager : MonoBehaviour
{
    [Header("Scene references")]
    public ProceduralPathMesh      pathMesh;
    public TurnMarkerPlacer        turnMarkerPlacer;
    public DestinationBeaconPlacer destinationBeaconPlacer;
    public NavigationUIManager     uiManager;

    [Header("Graph")]
    public string graphResourceName = "indoor_graph";
    public float  pathYOffset        = 0.02f;

    [Header("Routing")]
    public float routeUpdateInterval = 2.5f;

    public static NavigationManager Instance { get; private set; }

    private IndoorGraph _graph;
    private Transform   _anchorRoot;
    private string      _destinationId   = null;
    private string      _lastStart       = null;
    private string      _lastGoal        = null;
    private bool        _isNavigating    = false;
    private bool        _anchorPlaced    = false;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        LoadGraph();
        if (NearestBeaconResolver.Instance != null)
            NearestBeaconResolver.Instance.OnNodeChanged += OnNodeChanged;
        InvokeRepeating(nameof(PollRoute), 2f, routeUpdateInterval);
    }

    void OnDestroy()
    {
        if (NearestBeaconResolver.Instance != null)
            NearestBeaconResolver.Instance.OnNodeChanged -= OnNodeChanged;
        CancelInvoke();
    }

    // ── Graph ──────────────────────────────────────────────────────────────────
    void LoadGraph()
    {
        var asset = Resources.Load<TextAsset>(graphResourceName);
        if (asset == null) { Debug.LogError($"[NavManager] Cannot load '{graphResourceName}' from Resources."); return; }
        _graph = JsonUtility.FromJson<IndoorGraph>(asset.text);
        _graph.Build();
        Debug.Log($"[NavManager] Graph loaded: {_graph.nodes.Count} nodes, {_graph.edges.Count} edges.");
    }

    // ── Public API ──────────────────────────────────────────────────────────────
    public void OnAnchorPlaced(Transform anchor)
    {
        _anchorRoot   = anchor;
        _anchorPlaced = true;
        Debug.Log("[NavManager] Anchor placed.");
        uiManager?.OnAnchorReady();
    }

    public void StartNavigation(string destinationId)
    {
        if (_graph == null || !_graph.HasNode(destinationId))
        { Debug.LogError($"[NavManager] Bad destination: {destinationId}"); return; }

        _destinationId = destinationId;
        _isNavigating  = true;
        _lastStart     = null;
        _lastGoal      = null;

        string label = _graph.GetNode(destinationId)?.label ?? destinationId;
        uiManager?.OnNavigationStarted(destinationId, label);
        PollRoute();
    }

    public void StopNavigation()
    {
        _isNavigating  = false;
        _destinationId = null;
        ClearVisuals();
        uiManager?.OnNavigationStopped();
    }

    public List<GraphNode> GetDestinations() => _graph?.GetDestinationNodes() ?? new List<GraphNode>();
    public bool IsNavigating => _isNavigating;

    // ── Routing ────────────────────────────────────────────────────────────────
    void OnNodeChanged(string nodeId, string label)
    {
        uiManager?.UpdateCurrentLocation(label ?? "Unknown");
        if (_isNavigating) PollRoute();
    }

    void PollRoute()
    {
        if (!_isNavigating || !_anchorPlaced) return;

        string current = NearestBeaconResolver.Instance?.GetCurrentNodeId();

        if (current == null) { uiManager?.ShowLocating(); ClearVisuals(); return; }

        if (current == _destinationId) { Arrive(); return; }

        if (current == _lastStart && _destinationId == _lastGoal) return;
        _lastStart = current; _lastGoal = _destinationId;

        var route = AStarRouter.FindPath(_graph, current, _destinationId);
        if (route == null || route.Count < 2) { ClearVisuals(); return; }

        var pts = ToWorldPoints(route);
        pathMesh?.SetPath(pts);
        turnMarkerPlacer?.GenerateTurnMarkers();
        destinationBeaconPlacer?.GenerateDestinationBeacon();
        Debug.Log($"[NavManager] Route: {route.Count} nodes.");
    }

    void Arrive()
    {
        _isNavigating = false;
        ClearVisuals();
        string label = _graph.GetNode(_destinationId)?.label ?? _destinationId;
        uiManager?.OnArrived(_destinationId, label);
        Debug.Log($"[NavManager] Arrived at {label}!");
    }

    List<Vector3> ToWorldPoints(List<GraphNode> nodes)
    {
        var pts = new List<Vector3>();
        foreach (var n in nodes)
            pts.Add(_anchorRoot.TransformPoint(new Vector3(n.x, pathYOffset, n.z)));
        return pts;
    }

    void ClearVisuals()
    {
        pathMesh?.SetPath(new List<Vector3>());
        turnMarkerPlacer?.ClearMarkers();
        destinationBeaconPlacer?.ClearBeacon();
    }
}
