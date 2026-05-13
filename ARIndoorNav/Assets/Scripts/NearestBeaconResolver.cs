using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Maps BLE beacon RSSI to indoor graph node IDs.
/// "Strongest beacon wins" with hysteresis to prevent flickering.
/// Supports manual override for demo fallback (debug panel).
/// </summary>
public class NearestBeaconResolver : MonoBehaviour
{
    [System.Serializable]
    public class BeaconNodeMap
    {
        public int    beaconMajor;
        public string nodeId;
        public string label;
    }

    [Header("Beacon → Node mapping (set Major numbers after configuring beacons in FeasyBeacon app)")]
    public List<BeaconNodeMap> beaconMappings = new List<BeaconNodeMap>
    {
        new BeaconNodeMap { beaconMajor = 1, nodeId = "front_door",     label = "Front Door"     },
        new BeaconNodeMap { beaconMajor = 2, nodeId = "kitchen",        label = "Kitchen"        },
        new BeaconNodeMap { beaconMajor = 3, nodeId = "master_bedroom", label = "Master Bedroom" },
    };

    [Header("Tuning")]
    [Range(1,10)] public int   hysteresisCount = 3;
    public float minimumRSSI = -82f;

    public static NearestBeaconResolver Instance { get; private set; }

    private string _currentNodeId   = null;
    private string _candidateNodeId = null;
    private int    _candidateCount  = 0;

    private string _overrideNodeId  = null;
    private float  _overrideExpiry  = 0f;
    private const float OVERRIDE_DURATION = 30f;

    public delegate void NodeChangedHandler(string nodeId, string label);
    public event NodeChangedHandler OnNodeChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        if (_overrideNodeId != null && Time.time > _overrideExpiry) _overrideNodeId = null;
        ResolvePosition();
    }

    void ResolvePosition()
    {
        if (BLEManager.Instance == null) return;

        string bestNode = null;
        float  bestRSSI = minimumRSSI;

        foreach (var m in beaconMappings)
        {
            float rssi = BLEManager.Instance.GetSmoothedRSSI(m.beaconMajor);
            if (rssi > bestRSSI) { bestRSSI = rssi; bestNode = m.nodeId; }
        }

        if (bestNode == null)
        {
            if (_currentNodeId != null) { _currentNodeId = null; _candidateNodeId = null; _candidateCount = 0; OnNodeChanged?.Invoke(null, null); }
            return;
        }

        if (bestNode == _candidateNodeId) _candidateCount++;
        else { _candidateNodeId = bestNode; _candidateCount = 1; }

        if (_candidateCount >= hysteresisCount && bestNode != _currentNodeId)
        {
            _currentNodeId = bestNode;
            OnNodeChanged?.Invoke(_currentNodeId, GetLabel(_currentNodeId));
            Debug.Log($"[BeaconResolver] Node: {_currentNodeId}");
        }
    }

    public string GetCurrentNodeId()
    {
        if (_overrideNodeId != null && Time.time < _overrideExpiry) return _overrideNodeId;
        return _currentNodeId;
    }

    public string GetCurrentLabel()
    {
        string id = GetCurrentNodeId();
        return id == null ? "Locating..." : GetLabel(id);
    }

    public void SetManualOverride(string nodeId)
    {
        _overrideNodeId = nodeId;
        _overrideExpiry = Time.time + OVERRIDE_DURATION;
        OnNodeChanged?.Invoke(nodeId, GetLabel(nodeId));
        Debug.Log($"[BeaconResolver] Override: {nodeId} for {OVERRIDE_DURATION}s");
    }

    public void  ClearManualOverride()            => _overrideNodeId = null;
    public bool  IsManualOverrideActive()          => _overrideNodeId != null && Time.time < _overrideExpiry;
    public float ManualOverrideSecondsRemaining()  => Mathf.Max(0f, _overrideExpiry - Time.time);

    string GetLabel(string nodeId)
    {
        foreach (var m in beaconMappings) if (m.nodeId == nodeId) return m.label;
        return nodeId;
    }
}
