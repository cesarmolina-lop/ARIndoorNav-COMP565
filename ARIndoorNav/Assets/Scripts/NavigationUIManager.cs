using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// All UI panels: anchor prompt → destination picker → HUD → arrival → debug.
/// Destination chaining: arrival screen has "Go somewhere else" → picker.
/// Debug panel: 5-tap top-right corner on device, D key in Editor.
/// </summary>
public class NavigationUIManager : MonoBehaviour
{
    [Header("A — Anchor prompt")]
    public GameObject anchorPromptPanel;
    public TMP_Text   anchorPromptText;

    [Header("B — Destination picker")]
    public GameObject destinationPickerPanel;
    public Transform  destinationButtonContainer;
    public GameObject destinationButtonPrefab;
    public TMP_Text   pickerTitleText;

    [Header("C — HUD")]
    public GameObject hudPanel;
    public TMP_Text   hudCurrentLocationText;
    public TMP_Text   hudDestinationText;
    public TMP_Text   hudStatusText;
    public Button     stopNavButton;

    [Header("D — Arrival")]
    public GameObject arrivalPanel;
    public TMP_Text   arrivalMessageText;
    public Button     navigateAgainButton;
    public Button     arrivalDoneButton;

    [Header("E — Debug panel")]
    public GameObject debugPanel;
    public TMP_Text   debugNodeText;
    public TMP_Text   debugRSSIText;
    public TMP_Text   debugOverrideText;
    public Transform  debugOverrideButtonContainer;
    public GameObject debugOverrideButtonPrefab;

    private int   _tapCount  = 0;
    private float _tapExpiry = 0f;
    private const int   TAPS   = 5;
    private const float WINDOW = 1.5f;

    void Start()
    {
        stopNavButton?.onClick.AddListener(() => NavigationManager.Instance?.StopNavigation());
        navigateAgainButton?.onClick.AddListener(OnNavigateAgain);
        arrivalDoneButton?.onClick.AddListener(() => ShowPanel(destinationPickerPanel));

        if (anchorPromptText)
            anchorPromptText.text = "Point camera at the floor and tap to set your start point.";

        ShowPanel(anchorPromptPanel);
        debugPanel?.SetActive(false);
        StartCoroutine(RefreshDebug());
    }

    void Update()
    {
        DetectDebugTap();

#if UNITY_EDITOR
        if (UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.dKey.wasPressedThisFrame)
            ToggleDebug();
#endif
    }

    // ── Called by NavigationManager ────────────────────────────────────────────
    public void OnAnchorReady()
    {
        BuildDestButtons();
        BuildDebugButtons();
        ShowPanel(destinationPickerPanel);
        if (pickerTitleText) pickerTitleText.text = "Where would you like to go?";
    }

    public void OnNavigationStarted(string id, string label)
    {
        ShowPanel(hudPanel);
        if (hudDestinationText)     hudDestinationText.text     = $"Heading to: {label}";
        if (hudStatusText)          hudStatusText.text          = "On route";
        if (hudCurrentLocationText) hudCurrentLocationText.text = "Locating...";
    }

    public void OnNavigationStopped()
    {
        ShowPanel(destinationPickerPanel);
        if (pickerTitleText) pickerTitleText.text = "Where would you like to go?";
    }

    public void UpdateCurrentLocation(string loc)
    {
        if (hudCurrentLocationText) hudCurrentLocationText.text = $"You are near: {loc}";
    }

    public void ShowLocating()
    {
        if (hudStatusText)          hudStatusText.text          = "Locating...";
        if (hudCurrentLocationText) hudCurrentLocationText.text = "Searching for beacons...";
    }

    public void OnArrived(string id, string label)
    {
        ShowPanel(arrivalPanel);
        if (arrivalMessageText) arrivalMessageText.text = $"You have arrived at\n{label}!";
    }

    // ── Button handlers ────────────────────────────────────────────────────────
    void OnNavigateAgain()
    {
        ShowPanel(destinationPickerPanel);
        if (pickerTitleText) pickerTitleText.text = "Navigate somewhere else?";
    }

    // ── Button builders ────────────────────────────────────────────────────────
    void BuildDestButtons()
    {
        if (!destinationButtonContainer || !destinationButtonPrefab) return;
        foreach (Transform c in destinationButtonContainer) Destroy(c.gameObject);

        foreach (var dest in NavigationManager.Instance?.GetDestinations() ?? new List<GraphNode>())
        {
            var btn = Instantiate(destinationButtonPrefab, destinationButtonContainer);
            var lbl = btn.GetComponentInChildren<TMP_Text>();
            if (lbl) lbl.text = dest.label;
            string id = dest.id;
            btn.GetComponent<Button>().onClick.AddListener(() => NavigationManager.Instance?.StartNavigation(id));
        }
    }

    void BuildDebugButtons()
    {
        if (!debugOverrideButtonContainer || !debugOverrideButtonPrefab) return;
        if (NearestBeaconResolver.Instance == null) return;
        foreach (Transform c in debugOverrideButtonContainer) Destroy(c.gameObject);

        foreach (var m in NearestBeaconResolver.Instance.beaconMappings)
        {
            var btn = Instantiate(debugOverrideButtonPrefab, debugOverrideButtonContainer);
            var lbl = btn.GetComponentInChildren<TMP_Text>();
            if (lbl) lbl.text = m.label;
            string id = m.nodeId;
            btn.GetComponent<Button>().onClick.AddListener(() => NearestBeaconResolver.Instance?.SetManualOverride(id));
        }
    }

    // ── Panel helper ───────────────────────────────────────────────────────────
    void ShowPanel(GameObject target)
    {
        anchorPromptPanel?.SetActive(target == anchorPromptPanel);
        destinationPickerPanel?.SetActive(target == destinationPickerPanel);
        hudPanel?.SetActive(target == hudPanel);
        arrivalPanel?.SetActive(target == arrivalPanel);
    }

    // ── Debug panel ────────────────────────────────────────────────────────────
    void DetectDebugTap()
    {
#if UNITY_EDITOR
        return;
#endif
#pragma warning disable CS0162
        if (Input.touchCount != 1) return;
        var t = Input.GetTouch(0);
        if (t.phase != TouchPhase.Began) return;
        if (t.position.x < Screen.width * 0.75f || t.position.y < Screen.height * 0.75f) return;
        if (Time.time > _tapExpiry) _tapCount = 0;
        _tapCount++;
        _tapExpiry = Time.time + WINDOW;
        if (_tapCount >= TAPS) { _tapCount = 0; ToggleDebug(); }
#pragma warning restore CS0162
    }

    void ToggleDebug() => debugPanel?.SetActive(!debugPanel.activeSelf);

    IEnumerator RefreshDebug()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.5f);
            if (!debugPanel || !debugPanel.activeSelf) continue;

            if (NearestBeaconResolver.Instance != null)
            {
                string id     = NearestBeaconResolver.Instance.GetCurrentNodeId();
                string label  = NearestBeaconResolver.Instance.GetCurrentLabel();
                bool   manual = NearestBeaconResolver.Instance.IsManualOverrideActive();
                float  timer  = NearestBeaconResolver.Instance.ManualOverrideSecondsRemaining();
                if (debugNodeText)     debugNodeText.text     = $"Node: {label ?? "null"} ({id ?? "---"})";
                if (debugOverrideText) debugOverrideText.text = manual ? $"Override: {timer:F0}s" : "Override: off";
            }

            if (BLEManager.Instance != null && debugRSSIText)
            {
                var r  = BLEManager.Instance.GetAllReadings();
                var sb = new System.Text.StringBuilder("=== RSSI ===\n");
                foreach (var kv in r) sb.AppendLine($"Major {kv.Key}: {kv.Value:F1} dBm");
                if (r.Count == 0) sb.AppendLine("(no beacons)");
                debugRSSIText.text = sb.ToString();
            }
        }
    }
}
