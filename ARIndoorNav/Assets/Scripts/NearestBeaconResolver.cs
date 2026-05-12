using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// HoloNav NearestBeaconResolver
/// Maps BLE beacon RSSI readings to indoor graph node IDs.
///
/// Strategy: "Strongest beacon wins" with hysteresis to prevent flickering.
/// Each beacon is anchored to one graph node. The node whose beacon has
/// the highest (least negative) smoothed RSSI is the current position —
/// but only after that beacon has been dominant for HYSTERESIS_COUNT readings.
///
/// SETUP:
///   1. Place this on a persistent GameObject in your AR scene.
///   2. Edit the BeaconNodeMapping list in the Inspector to match your
///      physical beacon Major numbers and your graph node IDs.
///   3. Ensure BLEManager is also in the scene.
/// </summary>
public class NearestBeaconResolver : MonoBehaviour
{
    // ── Configuration ──────────────────────────────────────────────────────────

    [System.Serializable]
    public class BeaconNodeMap
    {
        [Tooltip("The Major number assigned to this beacon in the FeasyBeacon app")]
        public int beaconMajor;

        [Tooltip("The node ID in indoor_graph.json this beacon is physically placed at")]
        public string nodeId;

        [Tooltip("Human-readable label for debug/UI display")]
        public string label;
    }

    [Header("Beacon → Node Mapping")]
    [Tooltip("One entry per physical beacon. Match Major numbers to your graph node IDs.")]
    public List<BeaconNodeMap> beaconMappings = new List<BeaconNodeMap>()
    {
        // ── DEFAULT MAPPINGS — edit these in Inspector or here ────────────────
        // Major numbers 1/2/3 are placeholders — set them after configuring
        // your beacons in the FeasyBeacon app.
        new BeaconNodeMap { beaconMajor = 1, nodeId = "front_door",     label = "Front Door"     },
        new BeaconNodeMap { beaconMajor = 2, nodeId = "kitchen",        label = "Kitchen"        },
        new BeaconNodeMap { beaconMajor = 3, nodeId = "master_bedroom", label = "Master Bedroom" },
    };

    [Header("Tuning")]
    [Tooltip("How many consecutive readings the new strongest beacon must hold before switching nodes. Increase to reduce flickering, decrease to speed up transitions.")]
    [Range(1, 10)]
    public int hysteresisCount = 3;

    [Tooltip("If all beacon signals are weaker than this, report unknown position.")]
    public float minimumRSSI = -82f;

    [Tooltip("Allow manual override from debug UI (set via SetManualOverride).")]
    public bool allowManualOverride = true;

    // ── Singleton ──────────────────────────────────────────────────────────────
    public static NearestBeaconResolver Instance { get; private set; }

    // ── State ──────────────────────────────────────────────────────────────────
    private string _currentNodeId = null;
    private string _candidateNodeId = null;
    private int _candidateCount = 0;

    private string _manualOverrideNodeId = null;
    private float _manualOverrideExpiry = 0f;
    private const float MANUAL_OVERRIDE_DURATION = 30f;

    // ── Events ─────────────────────────────────────────────────────────────────
    public delegate void NodeChangedHandler(string newNodeId, string label);
    public event NodeChangedHandler OnNodeChanged;

    // ── Unity Lifecycle ────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        // Expire manual override
        if (_manualOverrideNodeId != null && Time.time > _manualOverrideExpiry)
        {
            Debug.Log("[NearestBeaconResolver] Manual override expired.");
            _manualOverrideNodeId = null;
        }

        // Poll BLE and resolve position every frame
        // (BLEManager smoothing already handles noise; polling here is cheap)
        ResolvePosition();
    }

    // ── Core Logic ─────────────────────────────────────────────────────────────
    private void ResolvePosition()
    {
        if (BLEManager.Instance == null) return;

        // Find the beacon with the strongest smoothed RSSI
        string bestNodeId = null;
        float bestRSSI = minimumRSSI; // anything weaker than this is ignored

        foreach (var mapping in beaconMappings)
        {
            float rssi = BLEManager.Instance.GetSmoothedRSSI(mapping.beaconMajor);
            if (rssi > bestRSSI)
            {
                bestRSSI = rssi;
                bestNodeId = mapping.nodeId;
            }
        }

        // No beacon strong enough — unknown position
        if (bestNodeId == null)
        {
            if (_currentNodeId != null)
            {
                _currentNodeId = null;
                _candidateNodeId = null;
                _candidateCount = 0;
                OnNodeChanged?.Invoke(null, null);
                Debug.Log("[NearestBeaconResolver] Position unknown (all beacons too weak).");
            }
            return;
        }

        // Hysteresis: candidate must hold for hysteresisCount consecutive reads
        if (bestNodeId == _candidateNodeId)
        {
            _candidateCount++;
        }
        else
        {
            _candidateNodeId = bestNodeId;
            _candidateCount = 1;
        }

        if (_candidateCount >= hysteresisCount && bestNodeId != _currentNodeId)
        {
            _currentNodeId = bestNodeId;
            string label = GetLabel(bestNodeId);
            Debug.Log($"[NearestBeaconResolver] Node confirmed: {_currentNodeId} ({label})");
            OnNodeChanged?.Invoke(_currentNodeId, label);
        }
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the current confirmed node ID, or null if position is unknown.
    /// Respects manual override if active.
    /// </summary>
    public string GetCurrentNodeId()
    {
        if (allowManualOverride && _manualOverrideNodeId != null && Time.time < _manualOverrideExpiry)
            return _manualOverrideNodeId;

        return _currentNodeId;
    }

    /// <summary>
    /// Returns the human-readable label for the current node, or "Locating..." if unknown.
    /// </summary>
    public string GetCurrentLabel()
    {
        string nodeId = GetCurrentNodeId();
        if (nodeId == null) return "Locating...";
        return GetLabel(nodeId);
    }

    /// <summary>
    /// Manually override the current position for MANUAL_OVERRIDE_DURATION seconds.
    /// Used by the debug panel as a demo fallback.
    /// </summary>
    public void SetManualOverride(string nodeId)
    {
        _manualOverrideNodeId = nodeId;
        _manualOverrideExpiry = Time.time + MANUAL_OVERRIDE_DURATION;
        Debug.Log($"[NearestBeaconResolver] Manual override set: {nodeId} for {MANUAL_OVERRIDE_DURATION}s");
        OnNodeChanged?.Invoke(nodeId, GetLabel(nodeId));
    }

    public void ClearManualOverride()
    {
        _manualOverrideNodeId = null;
        Debug.Log("[NearestBeaconResolver] Manual override cleared.");
    }

    public bool IsManualOverrideActive()
    {
        return _manualOverrideNodeId != null && Time.time < _manualOverrideExpiry;
    }

    public float ManualOverrideSecondsRemaining()
    {
        return Mathf.Max(0f, _manualOverrideExpiry - Time.time);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────
    private string GetLabel(string nodeId)
    {
        foreach (var m in beaconMappings)
            if (m.nodeId == nodeId) return m.label;
        return nodeId; // fallback: return raw ID
    }
}
