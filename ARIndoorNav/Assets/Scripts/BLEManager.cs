using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Wraps BluetoothLEHardwareInterface (Shatalmic) for iBeacon ranging.
/// SETUP: Replace BEACON_UUID with your project UUID (uuidgenerator.net).
///        Format: "XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX"
/// iOS NOTE: AndroidSignalPower is always 0 on iOS — we use RSSI only.
/// </summary>
public class BLEManager : MonoBehaviour
{
    // ── Edit these ─────────────────────────────────────────────────────────────
    private const string BEACON_UUID       = "93cef9fe-d866-4c94-8e8d-a32f56c956a5";
    private const string BEACON_IDENTIFIER = "HoloNav";
    private const int    SMOOTHING_WINDOW  = 5;
    private const float  MIN_RSSI          = -88f;
    private const float  SCAN_DELAY        = 0.5f;

    public static BLEManager Instance { get; private set; }

    private Dictionary<int, Queue<float>> _history      = new Dictionary<int, Queue<float>>();
    private Dictionary<int, float>        _smoothed     = new Dictionary<int, float>();
    private Dictionary<int, string>       _proximity    = new Dictionary<int, string>();

    public bool IsInitialized { get; private set; }
    public bool IsScanning    { get; private set; }

    private float _delayTimer    = 0f;
    private bool  _waitingToScan = false;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        BluetoothLEHardwareInterface.Initialize(true, false,
            () => {
                Debug.Log("[BLEManager] Initialized.");
                IsInitialized  = true;
                _waitingToScan = true;
                _delayTimer    = SCAN_DELAY;
            },
            (error) => {
                Debug.LogError($"[BLEManager] Error: {error}");
                if (error.Contains("Bluetooth LE Not Enabled"))
                    BluetoothLEHardwareInterface.BluetoothEnable(true);
            }, true);
    }

    void Update()
    {
        if (!_waitingToScan) return;
        _delayTimer -= Time.deltaTime;
        if (_delayTimer > 0f) return;
        _waitingToScan = false;
        StartScanning();
    }

    void OnDestroy()
    {
        StopScanning();
        BluetoothLEHardwareInterface.DeInitialize(() => { });
    }

    void StartScanning()
    {
        if (IsScanning) return;
        IsScanning = true;

        BluetoothLEHardwareInterface.ScanForBeacons(
            new string[] { $"{BEACON_UUID}:{BEACON_IDENTIFIER}" },
            (beacon) => {
                int   major = beacon.Major;
                float rssi  = (float)beacon.RSSI;
                if (rssi < -100f || rssi >= 0f) return;

                _proximity[major] = beacon.iOSProximity.ToString();

                if (!_history.ContainsKey(major)) _history[major] = new Queue<float>();
                var q = _history[major];
                q.Enqueue(rssi);
                if (q.Count > SMOOTHING_WINDOW) q.Dequeue();

                float sum = 0f;
                foreach (float r in q) sum += r;
                _smoothed[major] = sum / q.Count;

                Debug.Log($"[BLEManager] Major={major} RSSI={rssi:F0} Smoothed={_smoothed[major]:F1} Proximity={_proximity[major]}");
            });

        Debug.Log($"[BLEManager] Scanning started. UUID={BEACON_UUID}");
    }

    public void StopScanning()
    {
        if (!IsScanning) return;
        BluetoothLEHardwareInterface.StopScan();
        IsScanning = false;
    }

    public float  GetSmoothedRSSI(int major) => _smoothed.TryGetValue(major, out float r) ? r : MIN_RSSI - 1f;
    public string GetProximity(int major)     => _proximity.TryGetValue(major, out string p) ? p : "Unknown";
    public bool   HasReading(int major)       => _smoothed.ContainsKey(major) && _smoothed[major] >= MIN_RSSI;
    public Dictionary<int, float> GetAllReadings() => new Dictionary<int, float>(_smoothed);
}
