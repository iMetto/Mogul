
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Schedule I inspired management hub UI.
/// Press F6 to toggle the menu. Press Esc to close.
/// Suitable for use in a Unity/BepInEx mod.
/// </summary>
public class ScheduleHubUI : MonoBehaviour
{
    private Canvas canvas;
    private GameObject root;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F6))
            Toggle();

        if (root != null && root.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            root.SetActive(false);
    }

    public void Toggle()
    {
        if (root == null)
            BuildUI();

        root.SetActive(!root.activeSelf);
    }

    private void BuildUI()
    {
        EnsureEventSystem();

        // Canvas
        var canvasGO = new GameObject("ScheduleHubCanvas");
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;
        canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGO.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(canvasGO);

        // Root overlay
        root = CreateUIObject("Root", canvas.transform);
        var rootImage = root.AddComponent<Image>();
        rootImage.color = new Color(0f, 0f, 0f, 0.65f);

        var rootRect = root.GetComponent<RectTransform>();
        Stretch(rootRect);

        // Main panel
        var panel = CreateUIObject("Panel", root.transform);
        var panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.08f, 0.10f, 0.15f, 0.96f);

        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(1200, 700);
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;

        // Header
        CreateText(panel.transform, "Schedule I", 24, new Color(0.5f, 1f, 0.8f),
            new Vector2(40, -30), TextAlignmentOptions.Left);
        CreateText(panel.transform, "Management Hub", 54, Color.white,
            new Vector2(40, -80), TextAlignmentOptions.Left, FontStyles.Bold);
        CreateText(panel.transform, "Choose a section to manage your empire.", 24,
            new Color(0.7f, 0.75f, 0.8f), new Vector2(40, -140), TextAlignmentOptions.Left);

        // Cards container
        float startX = -350f;
        CreateCard(panel.transform, "🏠", "Properties",
            "Manage houses, businesses, and upgrades.",
            new Color(0.10f, 0.75f, 0.45f), new Vector2(startX, -380));

        CreateCard(panel.transform, "📦", "Orders",
            "Track deliveries, stock, and customer requests.",
            new Color(0.20f, 0.55f, 1.0f), new Vector2(0, -380));

        CreateCard(panel.transform, "🎯", "Quests",
            "View objectives, missions, and rewards.",
            new Color(0.70f, 0.35f, 1.0f), new Vector2(-startX, -380));

        // Footer
        CreateText(panel.transform, "Start Page", 20,
            new Color(0.6f, 0.65f, 0.7f), new Vector2(40, -660), TextAlignmentOptions.Left);
        CreateText(panel.transform, "Properties • Orders • Quests", 20,
            new Color(0.6f, 0.65f, 0.7f), new Vector2(-40, -660), TextAlignmentOptions.Right);

        root.SetActive(false);
    }

    private void CreateCard(Transform parent, string icon, string title, string description, Color accent, Vector2 position)
    {
        var card = CreateUIObject(title + "Card", parent);
        var image = card.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.06f);

        var rect = card.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(300, 420);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.anchoredPosition = position;

        // Button
        var button = card.AddComponent<Button>();
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.15f);
        colors.pressedColor = new Color(1f, 1f, 1f, 0.08f);
        colors.fadeDuration = 0.15f;
        button.colors = colors;

        button.onClick.AddListener(() => Debug.Log("Open " + title + " view"));

        // Accent strip
        var strip = CreateUIObject("Accent", card.transform);
        var stripImage = strip.AddComponent<Image>();
        stripImage.color = accent;
        var stripRect = strip.GetComponent<RectTransform>();
        stripRect.anchorMin = new Vector2(0, 1);
        stripRect.anchorMax = new Vector2(1, 1);
        stripRect.pivot = new Vector2(0.5f, 1);
        stripRect.sizeDelta = new Vector2(0, 6);
        stripRect.anchoredPosition = Vector2.zero;

        // Text
        CreateText(card.transform, icon, 64, accent,
            new Vector2(0, -70), TextAlignmentOptions.Center);
        CreateText(card.transform, title, 34, Color.white,
            new Vector2(0, -150), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateText(card.transform, description, 20,
            new Color(0.75f, 0.8f, 0.85f), new Vector2(0, -225),
            TextAlignmentOptions.Center);

        // Open button label
        var open = CreateUIObject("Open", card.transform);
        var openImage = open.AddComponent<Image>();
        openImage.color = new Color(accent.r, accent.g, accent.b, 0.25f);

        var openRect = open.GetComponent<RectTransform>();
        openRect.sizeDelta = new Vector2(220, 52);
        openRect.anchorMin = openRect.anchorMax = new Vector2(0.5f, 0);
        openRect.anchoredPosition = new Vector2(0, 40);

        CreateText(open.transform, "Open View →", 20, Color.white,
            Vector2.zero, TextAlignmentOptions.Center);
    }

    private TMP_Text CreateText(
        Transform parent,
        string text,
        int fontSize,
        Color color,
        Vector2 anchoredPosition,
        TextAlignmentOptions alignment,
        FontStyles style = FontStyles.Normal)
    {
        var go = CreateUIObject("Text", parent);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.fontStyle = style;
        tmp.enableWordWrapping = true;

        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(260, 120);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;

        return tmp;
    }

    private GameObject CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
            return;

        new GameObject("EventSystem",
            typeof(EventSystem),
            typeof(StandaloneInputModule));
    }
}
