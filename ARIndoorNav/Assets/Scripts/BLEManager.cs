using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// HoloNav BLEManager
/// Wraps BluetoothLEHardwareInterface (Shatalmic asset) for iBeacon ranging.
///
/// SETUP:
///   1. Set BEACON_UUID to the UUID you assign all beacons in the FeasyBeacon app.
///      Format: "XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX"  (generate at uuidgenerator.net)
///   2. Set BEACON_IDENTIFIER to any short label (no spaces).
///   3. Attach to a persistent GameObject in your AR scene.
///   4. In Xcode after building: add NSBluetoothAlwaysUsageDescription
///      and NSLocationWhenInUseUsageDescription to Info.plist.
///
/// iOS NOTE: iOS does NOT expose signal power (measured power/TxPower).
///   We use raw RSSI + iOSProximity enum for localization.
///   "Strongest RSSI wins" is reliable for room-level detection in a house.
/// </summary>
public class BLEManager : MonoBehaviour
{
    // ── Configuration — EDIT THESE ─────────────────────────────────────────────
    // Replace with YOUR project UUID (same UUID configured on all beacons).
    private const string BEACON_UUID = "YOUR-UUID-HERE-REPLACE-ME";
    private const string BEACON_IDENTIFIER = "HoloNav";

    // Rolling average window for RSSI smoothing.
    private const int SMOOTHING_WINDOW = 5;

    // Signals weaker than this are ignored.
    private const float MIN_RSSI = -88f;

    // Delay before starting scan after BLE initializes (matches asset example pattern).
    private const float SCAN_START_DELAY = 0.5f;

    // ── Singleton ──────────────────────────────────────────────────────────────
    public static BLEManager Instance { get; private set; }

    // ── State ──────────────────────────────────────────────────────────────────
    // Smoothed RSSI per beacon Major number.
    private Dictionary<int, Queue<float>> _rssiHistory = new Dictionary<int, Queue<float>>();
    private Dictionary<int, float> _smoothedRSSI = new Dictionary<int, float>();

    // iOSProximity per beacon Major (immediate/near/far/unknown).
    private Dictionary<int, string> _proximity = new Dictionary<int, string>();

    public bool IsInitialized { get; private set; } = false;
    public bool IsScanning { get; private set; } = false;

    private float _scanDelayTimer = 0f;
    private bool _waitingToScan = false;

    // ── Unity Lifecycle ────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // 5th parameter = true: required for beacon scanning (requests location on Android,
        // needed for BLE scan permissions). Safe to pass true on iOS as well.
        BluetoothLEHardwareInterface.Initialize(true, false, () =>
        {
            Debug.Log("[BLEManager] Initialized. Waiting before scan...");
            IsInitialized = true;
            _waitingToScan = true;
            _scanDelayTimer = SCAN_START_DELAY;
        },
        (error) =>
        {
            Debug.LogError($"[BLEManager] Init error: {error}");
            if (error.Contains("Bluetooth LE Not Enabled"))
                BluetoothLEHardwareInterface.BluetoothEnable(true);
        }, true);
    }

    private void Update()
    {
        // Delay scan start after init (matches asset example pattern).
        if (_waitingToScan)
        {
            _scanDelayTimer -= Time.deltaTime;
            if (_scanDelayTimer <= 0f)
            {
                _waitingToScan = false;
                StartScanning();
            }
        }
    }

    private void OnDestroy()
    {
        StopScanning();
        BluetoothLEHardwareInterface.DeInitialize(() => { });
    }

    // ── Scanning ───────────────────────────────────────────────────────────────
    private void StartScanning()
    {
        if (IsScanning) return;
        IsScanning = true;

        // UUID format required by this asset: "UUID:Identifier"
        string[] uuids = new string[] { $"{BEACON_UUID}:{BEACON_IDENTIFIER}" };

        BluetoothLEHardwareInterface.ScanForBeacons(uuids, (iBeaconData) =>
        {
            // iBeaconData fields available on iOS:
            //   .UUID (string)       — the beacon's proximity UUID
            //   .Major (int)         — our room identifier
            //   .Minor (int)         — not used
            //   .RSSI (int)          — received signal strength (negative dBm)
            //   .iOSProximity (enum) — immediate / near / far / unknown
            //   .AndroidSignalPower  — NOT available on iOS (always 0)

            int major = iBeaconData.Major;
            float rssi = (float)iBeaconData.RSSI;
            string proximity = iBeaconData.iOSProximity.ToString();

            // Ignore implausibly weak or invalid signals.
            if (rssi < -100f || rssi >= 0f) return;

            // Store proximity.
            _proximity[major] = proximity;

            // Add to rolling RSSI history.
            if (!_rssiHistory.ContainsKey(major))
                _rssiHistory[major] = new Queue<float>();

            Queue<float> history = _rssiHistory[major];
            history.Enqueue(rssi);
            if (history.Count > SMOOTHING_WINDOW)
                history.Dequeue();

            // Recompute smoothed average.
            float sum = 0f;
            foreach (float r in history) sum += r;
            _smoothedRSSI[major] = sum / history.Count;

            Debug.Log($"[BLEManager] Major={major} RSSI={rssi:F0} Smoothed={_smoothedRSSI[major]:F1} Proximity={proximity}");
        });

        Debug.Log($"[BLEManager] Scanning for UUID: {BEACON_UUID}");
    }

    public void StopScanning()
    {
        if (!IsScanning) return;
        BluetoothLEHardwareInterface.StopScan();
        IsScanning = false;
        Debug.Log("[BLEManager] Scan stopped.");
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Smoothed RSSI for a beacon by Major number.
    /// Returns MIN_RSSI - 1 if no data (so it's never "strongest").
    /// </summary>
    public float GetSmoothedRSSI(int major)
    {
        return _smoothedRSSI.TryGetValue(major, out float r) ? r : MIN_RSSI - 1f;
    }

    /// <summary>
    /// iOS proximity enum string: "Immediate", "Near", "Far", "Unknown".
    /// Useful as a secondary confidence check.
    /// </summary>
    public string GetProximity(int major)
    {
        return _proximity.TryGetValue(major, out string p) ? p : "Unknown";
    }

    /// <summary>
    /// True if we have at least one reading above the minimum threshold.
    /// </summary>
    public bool HasReading(int major)
    {
        return _smoothedRSSI.ContainsKey(major) && _smoothedRSSI[major] >= MIN_RSSI;
    }

    /// <summary>
    /// All current smoothed readings — used by the debug panel.
    /// </summary>
    public Dictionary<int, float> GetAllReadings()
    {
        return new Dictionary<int, float>(_smoothedRSSI);
    }
}