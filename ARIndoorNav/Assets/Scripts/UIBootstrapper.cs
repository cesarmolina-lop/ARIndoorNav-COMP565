using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Builds the entire HoloNav UI at runtime — no manual Canvas setup needed.
/// Add this script to any GameObject in the scene.
/// It creates its own Canvas, all panels, and all buttons.
/// DELETE your existing Canvas before using this.
/// </summary>
public class UIBootstrapper : MonoBehaviour
{
    // ── Color palette ──────────────────────────────────────────────────────────
    static Color COL_BG      = new Color(0.05f, 0.05f, 0.05f, 0.85f);
    static Color COL_PANEL   = new Color(0.10f, 0.10f, 0.12f, 0.95f);
    static Color COL_BTN     = new Color(0.18f, 0.38f, 0.82f, 1.00f);
    static Color COL_BTN_RED = new Color(0.75f, 0.18f, 0.18f, 1.00f);
    static Color COL_BTN_GRN = new Color(0.15f, 0.62f, 0.35f, 1.00f);
    static Color COL_TEXT    = Color.white;
    static Color COL_MUTED   = new Color(0.75f, 0.75f, 0.75f, 1f);

    // ── Public refs (filled at runtime, used by NavigationUIManager) ───────────
    [HideInInspector] public NavigationUIManager uiManager;

    void Awake()
    {
        BuildUI();
    }

    void BuildUI()
    {
        // ── Canvas ─────────────────────────────────────────────────────────────
        var canvasGO = new GameObject("HoloNavCanvas");
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var cs = canvasGO.AddComponent<CanvasScaler>();
        cs.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(390, 844); // iPhone 13 Pro Max logical res
        cs.matchWidthOrHeight  = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // EventSystem
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // ── NavigationUIManager ────────────────────────────────────────────────
        var uiGO = new GameObject("NavigationUIManager");
        uiManager = uiGO.AddComponent<NavigationUIManager>();

        // ── Build panels ───────────────────────────────────────────────────────
        uiManager.anchorPromptPanel      = BuildAnchorPromptPanel(canvasGO.transform, out uiManager.anchorPromptText);
        uiManager.destinationPickerPanel = BuildDestPickerPanel(canvasGO.transform,
            out uiManager.destinationButtonContainer, out uiManager.destinationButtonPrefab, out uiManager.pickerTitleText);
        uiManager.hudPanel               = BuildHUDPanel(canvasGO.transform,
            out uiManager.hudCurrentLocationText, out uiManager.hudDestinationText,
            out uiManager.hudStatusText, out uiManager.stopNavButton);
        uiManager.arrivalPanel           = BuildArrivalPanel(canvasGO.transform,
            out uiManager.arrivalMessageText, out uiManager.navigateAgainButton, out uiManager.arrivalDoneButton);
        uiManager.debugPanel             = BuildDebugPanel(canvasGO.transform,
            out uiManager.debugNodeText, out uiManager.debugRSSIText, out uiManager.debugOverrideText,
            out uiManager.debugOverrideButtonContainer, out uiManager.debugOverrideButtonPrefab);

        // Wire NavigationManager reference
        if (NavigationManager.Instance != null)
            NavigationManager.Instance.uiManager = uiManager;

        Debug.Log("[UIBootstrapper] UI built.");
    }

    // ── Panel builders ─────────────────────────────────────────────────────────

    GameObject BuildAnchorPromptPanel(Transform canvas, out TMP_Text promptText)
    {
        var panel = MakePanel(canvas, "AnchorPromptPanel", new Vector2(0, -200), new Vector2(340, 160));
        promptText = MakeText(panel.transform, "PointText",
            "Point camera at the floor\nand tap to place your start point.",
            24, COL_TEXT, new Vector2(0, 10), new Vector2(300, 120));
        promptText.alignment = TextAlignmentOptions.Center;
        return panel;
    }

    GameObject BuildDestPickerPanel(Transform canvas, out Transform buttonContainer,
        out GameObject buttonPrefab, out TMP_Text titleText)
    {
        var panel = MakeFullPanel(canvas, "DestPickerPanel");

        titleText = MakeText(panel.transform, "Title", "Where would you like to go?",
            28, COL_TEXT, new Vector2(0, 330), new Vector2(360, 50));
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontStyle = FontStyles.Bold;

        // Scroll area
        var scroll     = MakeRect(panel.transform, "ScrollView", new Vector2(0, 0), new Vector2(360, 580));
        var viewport   = MakeRect(scroll.transform, "Viewport",  new Vector2(0, 0), new Vector2(360, 580));
        var mask       = viewport.AddComponent<RectMask2D>();
        var content    = MakeRect(viewport.transform, "Content", new Vector2(0, 0),  new Vector2(360, 0));
        var vlg        = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing    = 12;
        vlg.padding    = new RectOffset(16, 16, 16, 16);
        vlg.childControlHeight = true;
        vlg.childControlWidth  = true;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        buttonContainer = content.transform;

        // Build prefab (deactivated template)
        buttonPrefab = BuildButtonPrefab("DestButtonPrefab", "Destination", COL_BTN, 60);

        return panel;
    }

    GameObject BuildHUDPanel(Transform canvas, out TMP_Text locText, out TMP_Text destText,
        out TMP_Text statusText, out Button stopBtn)
    {
        var panel = MakePanel(canvas, "HUDPanel", new Vector2(0, -330), new Vector2(390, 200));
        SetAnchor(panel, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0));
        panel.GetComponent<RectTransform>().sizeDelta    = new Vector2(0, 200);
        panel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 0);

        var vlg   = panel.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(16, 16, 12, 12);
        vlg.spacing = 6;
        vlg.childControlHeight = false;
        vlg.childControlWidth  = true;
        vlg.childForceExpandWidth = true;

        locText    = MakeTextInLayout(panel.transform, "LocText",   "You are near: ...",    20, COL_MUTED,  32);
        destText   = MakeTextInLayout(panel.transform, "DestText",  "Heading to: ...",      22, COL_TEXT,   36);
        statusText = MakeTextInLayout(panel.transform, "Status",    "On route",             18, COL_MUTED,  28);
        stopBtn    = MakeButtonInLayout(panel.transform, "StopBtn", "Stop Navigation",      COL_BTN_RED, 48);

        return panel;
    }

    GameObject BuildArrivalPanel(Transform canvas, out TMP_Text msgText,
        out Button againBtn, out Button doneBtn)
    {
        var panel = MakeFullPanel(canvas, "ArrivalPanel");
        var vlg   = panel.AddComponent<VerticalLayoutGroup>();
        vlg.padding            = new RectOffset(32, 32, 0, 0);
        vlg.spacing            = 20;
        vlg.childAlignment     = TextAnchor.MiddleCenter;
        vlg.childControlWidth  = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;

        msgText  = MakeTextInLayout(panel.transform, "ArrMsg",  "You have arrived!", 36, COL_TEXT,   80);
        msgText.fontStyle  = FontStyles.Bold;
        msgText.alignment  = TextAlignmentOptions.Center;
        againBtn = MakeButtonInLayout(panel.transform, "AgainBtn", "Go somewhere else", COL_BTN,     64);
        doneBtn  = MakeButtonInLayout(panel.transform, "DoneBtn",  "Done",              COL_BTN_GRN, 64);

        return panel;
    }

    GameObject BuildDebugPanel(Transform canvas, out TMP_Text nodeText, out TMP_Text rssiText,
        out TMP_Text overrideText, out Transform overrideBtnContainer, out GameObject overrideBtnPrefab)
    {
        var panel = MakePanel(canvas, "DebugPanel", new Vector2(-10, 10), new Vector2(280, 420));
        SetAnchor(panel, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
        panel.GetComponent<RectTransform>().anchoredPosition = new Vector2(-10, -10);

        var bg = panel.GetComponent<Image>();
        if (bg) bg.color = new Color(0, 0, 0, 0.9f);

        var vlg   = panel.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(12, 12, 12, 12);
        vlg.spacing = 6;
        vlg.childControlWidth  = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;

        var title = MakeTextInLayout(panel.transform, "DbgTitle", "DEBUG", 16, COL_TEXT, 24);
        title.fontStyle = FontStyles.Bold;

        nodeText     = MakeTextInLayout(panel.transform, "NodeTxt",     "Node: ---",     13, COL_MUTED, 22);
        overrideText = MakeTextInLayout(panel.transform, "OvrTxt",      "Override: off", 13, COL_MUTED, 22);
        rssiText     = MakeTextInLayout(panel.transform, "RSSITxt",     "=== RSSI ===",  12, COL_MUTED, 88);

        var sep = MakeTextInLayout(panel.transform, "Sep", "── Override position ──", 12, COL_MUTED, 20);
        sep.alignment = TextAlignmentOptions.Center;

        var container = MakeRect(panel.transform, "OvrBtnContainer", Vector2.zero, new Vector2(256, 0));
        var cvlg      = container.AddComponent<VerticalLayoutGroup>();
        cvlg.spacing  = 6;
        cvlg.childControlHeight = true;
        cvlg.childControlWidth  = true;
        cvlg.childForceExpandWidth = true;
        container.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var cle = container.AddComponent<LayoutElement>();
        cle.flexibleHeight = 1;

        overrideBtnContainer = container.transform;
        overrideBtnPrefab    = BuildButtonPrefab("OvrBtnPrefab", "Override", COL_BTN, 40);

        panel.SetActive(false);
        return panel;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    GameObject MakePanel(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt  = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta        = size;
        var img = go.AddComponent<Image>();
        img.color = COL_PANEL;
        var outline = go.AddComponent<Outline>();
        outline.effectColor    = new Color(0.3f, 0.5f, 1f, 0.4f);
        outline.effectDistance = new Vector2(1, -1);
        return go;
    }

    GameObject MakeFullPanel(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = COL_BG;
        return go;
    }

    GameObject MakeRect(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta        = size;
        return go;
    }

    TMP_Text MakeText(Transform parent, string name, string text, float size, Color col, Vector2 pos, Vector2 sz)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta        = sz;
        var t  = go.AddComponent<TextMeshProUGUI>();
        t.text      = text;
        t.fontSize  = size;
        t.color     = col;
        return t;
    }

    TMP_Text MakeTextInLayout(Transform parent, string name, string text, float size, Color col, float height)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        var t  = go.AddComponent<TextMeshProUGUI>();
        t.text      = text;
        t.fontSize  = size;
        t.color     = col;
        return t;
    }

    Button MakeButtonInLayout(Transform parent, string name, string label, Color col, float height)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);
        var le  = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        var img = go.AddComponent<Image>();
        img.color = col;
        var btn = go.AddComponent<Button>();
        var cb  = btn.colors;
        cb.normalColor      = col;
        cb.highlightedColor = col * 1.2f;
        cb.pressedColor     = col * 0.8f;
        btn.colors = cb;
        var txtGO = new GameObject("Label");
        txtGO.transform.SetParent(go.transform, false);
        var txtRT = txtGO.AddComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = Vector2.zero; txtRT.offsetMax = Vector2.zero;
        var t = txtGO.AddComponent<TextMeshProUGUI>();
        t.text      = label;
        t.fontSize  = 20;
        t.color     = Color.white;
        t.alignment = TextAlignmentOptions.Center;
        t.fontStyle = FontStyles.Bold;
        return btn;
    }

    GameObject BuildButtonPrefab(string name, string label, Color col, float height)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(transform, false);
        var le  = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        var img = go.AddComponent<Image>();
        img.color = col;
        go.AddComponent<Button>();
        var txtGO = new GameObject("Label");
        txtGO.transform.SetParent(go.transform, false);
        var txtRT = txtGO.AddComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = Vector2.zero; txtRT.offsetMax = Vector2.zero;
        var t = txtGO.AddComponent<TextMeshProUGUI>();
        t.text      = label;
        t.fontSize  = 18;
        t.color     = Color.white;
        t.alignment = TextAlignmentOptions.Center;
        go.SetActive(false);
        return go;
    }

    void SetAnchor(GameObject go, Vector2 min, Vector2 max, Vector2 pivot)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.pivot     = pivot;
    }
}
