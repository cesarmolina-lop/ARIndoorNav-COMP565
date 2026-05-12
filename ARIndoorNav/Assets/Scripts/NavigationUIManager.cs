using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HoloNav NavigationUIManager
///
/// UI panels:
///   [A] Anchor prompt  — shown on launch until user taps floor to place anchor
///   [B] Destination picker — shown after anchor placed, and after each arrival
///   [C] Navigation HUD — shown while navigating
///   [D] Arrival screen — shown on arrival, with "Go somewhere else?" for chaining
///   [E] Debug panel — 5-tap top-right corner to open
///
/// All references assigned in Inspector.
/// </summary>
public class NavigationUIManager : MonoBehaviour
{
    [Header("A — Anchor Prompt Panel")]
    public GameObject anchorPromptPanel;
    public TMP_Text anchorPromptText;

    [Header("B — Destination Picker Panel")]
    public GameObject destinationPickerPanel;
    public Transform destinationButtonContainer;
    public GameObject destinationButtonPrefab;
    public TMP_Text pickerTitleText;

    [Header("C — Navigation HUD")]
    public GameObject hudPanel;
    public TMP_Text hudCurrentLocationText;
    public TMP_Text hudDestinationText;
    public TMP_Text hudStatusText;
    public Button stopNavButton;

    [Header("D — Arrival Panel")]
    public GameObject arrivalPanel;
    public TMP_Text arrivalMessageText;
    public Button navigateAgainButton;
    public Button arrivalDoneButton;

    [Header("E — Debug Panel")]
    public GameObject debugPanel;
    public TMP_Text debugNodeText;
    public TMP_Text debugRSSIText;
    public TMP_Text debugOverrideText;
    public Transform debugOverrideButtonContainer;
    public GameObject debugOverrideButtonPrefab;

    private int _cornerTapCount = 0;
    private float _cornerTapExpiry = 0f;
    private const int CORNER_TAPS = 5;
    private const float TAP_WINDOW = 1.5f;

    private void Start()
    {
        stopNavButton?.onClick.AddListener(() => NavigationManager.Instance?.StopNavigation());
        navigateAgainButton?.onClick.AddListener(OnNavigateAgainPressed);
        arrivalDoneButton?.onClick.AddListener(() => ShowPanel(destinationPickerPanel));

        ShowPanel(anchorPromptPanel);
        debugPanel?.SetActive(false);

        if (anchorPromptText)
            anchorPromptText.text = "Point camera at the floor and tap to place your start point.";

        StartCoroutine(DebugPanelRefresh());
    }

    private void Update()
    {
        DetectDebugTap();
#if UNITY_EDITOR
        if (UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.dKey.wasPressedThisFrame)
            ToggleDebugPanel();
#endif
    }

    // ── Called by NavigationManager ────────────────────────────────────────────

    public void OnAnchorReady()
    {
        BuildDestinationButtons();
        BuildDebugOverrideButtons();
        ShowPanel(destinationPickerPanel);
        if (pickerTitleText) pickerTitleText.text = "Where would you like to go?";
    }

    public void OnNavigationStarted(string nodeId, string label)
    {
        ShowPanel(hudPanel);
        if (hudDestinationText) hudDestinationText.text = $"Heading to: {label}";
        if (hudStatusText) hudStatusText.text = "On route";
        if (hudCurrentLocationText) hudCurrentLocationText.text = "Locating...";
    }

    public void OnNavigationStopped()
    {
        ShowPanel(destinationPickerPanel);
        if (pickerTitleText) pickerTitleText.text = "Where would you like to go?";
    }

    public void UpdateCurrentLocation(string locationLabel)
    {
        if (hudCurrentLocationText)
            hudCurrentLocationText.text = $"You are near: {locationLabel}";
    }

    public void ShowLocating()
    {
        if (hudStatusText) hudStatusText.text = "Locating...";
        if (hudCurrentLocationText) hudCurrentLocationText.text = "Searching for beacons...";
    }

    public void OnArrived(string nodeId, string label)
    {
        ShowPanel(arrivalPanel);
        if (arrivalMessageText) arrivalMessageText.text = $"You have arrived at\n{label}!";
    }

    // ── Button handlers ────────────────────────────────────────────────────────

    private void OnNavigateAgainPressed()
    {
        ShowPanel(destinationPickerPanel);
        if (pickerTitleText) pickerTitleText.text = "Navigate somewhere else?";
    }

    // ── Button builders ────────────────────────────────────────────────────────

    private void BuildDestinationButtons()
    {
        if (destinationButtonContainer == null || destinationButtonPrefab == null) return;
        foreach (Transform child in destinationButtonContainer) Destroy(child.gameObject);

        var destinations = NavigationManager.Instance?.GetDestinations() ?? new List<GraphNode>();
        foreach (var dest in destinations)
        {
            GameObject btn = Instantiate(destinationButtonPrefab, destinationButtonContainer);
            var lbl = btn.GetComponentInChildren<TMP_Text>();
            if (lbl) lbl.text = dest.label;

            string capturedId = dest.id;
            btn.GetComponent<Button>().onClick.AddListener(() =>
                NavigationManager.Instance?.StartNavigation(capturedId));
        }
    }

    private void BuildDebugOverrideButtons()
    {
        if (debugOverrideButtonContainer == null || debugOverrideButtonPrefab == null) return;
        if (NearestBeaconResolver.Instance == null) return;
        foreach (Transform child in debugOverrideButtonContainer) Destroy(child.gameObject);

        foreach (var mapping in NearestBeaconResolver.Instance.beaconMappings)
        {
            GameObject btn = Instantiate(debugOverrideButtonPrefab, debugOverrideButtonContainer);
            var lbl = btn.GetComponentInChildren<TMP_Text>();
            if (lbl) lbl.text = mapping.label;

            string capturedId = mapping.nodeId;
            btn.GetComponent<Button>().onClick.AddListener(() =>
                NearestBeaconResolver.Instance?.SetManualOverride(capturedId));
        }
    }

    // ── Panel helper ───────────────────────────────────────────────────────────

    private void ShowPanel(GameObject target)
    {
        anchorPromptPanel?.SetActive(target == anchorPromptPanel);
        destinationPickerPanel?.SetActive(target == destinationPickerPanel);
        hudPanel?.SetActive(target == hudPanel);
        arrivalPanel?.SetActive(target == arrivalPanel);
    }

    // ── Debug panel ────────────────────────────────────────────────────────────

    private void DetectDebugTap()
    {
#if UNITY_EDITOR
        return; // handled via keyboard in editor
#endif
        if (Input.touchCount != 1) return;
        var t = Input.GetTouch(0);
        if (t.phase != TouchPhase.Began) return;
        if (t.position.x < Screen.width * 0.75f || t.position.y < Screen.height * 0.75f) return;

        if (Time.time > _cornerTapExpiry) _cornerTapCount = 0;
        _cornerTapCount++;
        _cornerTapExpiry = Time.time + TAP_WINDOW;
        if (_cornerTapCount >= CORNER_TAPS) { _cornerTapCount = 0; ToggleDebugPanel(); }
    }

    private void ToggleDebugPanel() => debugPanel?.SetActive(!debugPanel.activeSelf);

    private IEnumerator DebugPanelRefresh()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.5f);
            if (debugPanel == null || !debugPanel.activeSelf) continue;

            if (NearestBeaconResolver.Instance != null)
            {
                string nodeId = NearestBeaconResolver.Instance.GetCurrentNodeId();
                string label = NearestBeaconResolver.Instance.GetCurrentLabel();
                bool manual = NearestBeaconResolver.Instance.IsManualOverrideActive();
                float timer = NearestBeaconResolver.Instance.ManualOverrideSecondsRemaining();
                if (debugNodeText)
                    debugNodeText.text = $"Node: {label ?? "null"} ({nodeId ?? "---"})";
                if (debugOverrideText)
                    debugOverrideText.text = manual ? $"Override active: {timer:F0}s" : "Override: off";
            }

            if (BLEManager.Instance != null && debugRSSIText != null)
            {
                var readings = BLEManager.Instance.GetAllReadings();
                var sb = new System.Text.StringBuilder("=== RSSI ===\n");
                foreach (var kv in readings) sb.AppendLine($"Major {kv.Key}: {kv.Value:F1} dBm");
                if (readings.Count == 0) sb.AppendLine("(none detected)");
                debugRSSIText.text = sb.ToString();
            }
        }
    }
}