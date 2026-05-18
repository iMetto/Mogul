using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Il2CppScheduleOne;
using Il2CppScheduleOne.ItemFramework;
using Mogul.Data;
using Mogul.Systems;
using S1API.PhoneApp;
using UnityEngine;
using UnityEngine.UI;

namespace Mogul.Apps;

public partial class MogulApp
{
    private static Texture2D LoadModTexture(string filename)
    {
        try
        {
            var dir  = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var path = Path.Combine(dir, filename);
            if (!File.Exists(path)) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.LoadImage(File.ReadAllBytes(path));
            return tex;
        }
        catch { return null; }
    }

    private void BuildBackground(GameObject container)
    {
        var bg = new GameObject("Background");
        bg.transform.SetParent(container.transform, false);
        bg.AddComponent<Image>().color = ColorBg;
        var r = bg.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.sizeDelta = Vector2.zero;

        var vignette = new GameObject("BackgroundVignette");
        vignette.transform.SetParent(container.transform, false);
        vignette.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.22f);
        var vr = vignette.GetComponent<RectTransform>();
        vr.anchorMin = new Vector2(0.02f, 0.04f);
        vr.anchorMax = new Vector2(0.98f, 0.96f);
        vr.sizeDelta = Vector2.zero;
    }

    private void BuildHeader(GameObject container)
    {
        var header = new GameObject("Header");
        header.transform.SetParent(container.transform, false);
        header.AddComponent<Image>().color = ColorHeader;
        var hr = header.GetComponent<RectTransform>();
        hr.anchorMin = new Vector2(0f, 0.9f);
        hr.anchorMax = new Vector2(1f, 1f);
        hr.sizeDelta = Vector2.zero;

        _backButton = new GameObject("BackButton");
        _backButton.transform.SetParent(header.transform, false);
        var br = _backButton.AddComponent<RectTransform>();
        br.anchorMin = new Vector2(0.012f, 0.2f);
        br.anchorMax = new Vector2(0.06f, 0.8f);
        br.sizeDelta = Vector2.zero;
        _backButton.AddComponent<Image>().color = ColorRow;
        _backButton.AddComponent<Button>().onClick.AddListener(new Action(() =>
        {
            ShowView(_currentView == View.MixBuilder ? View.Manage
                   : _currentView == View.List      ? View.Hub
                   :                                  View.List);
        }));
        var backLabel = MakeText(_backButton, "Label", "<");
        var backText = backLabel.GetComponent<Text>();
        backText.fontSize = 22;
        backText.fontStyle = FontStyle.Bold;
        backText.color = ColorGold;
        backText.alignment = TextAnchor.MiddleCenter;
        var blr = backLabel.GetComponent<RectTransform>();
        blr.anchorMin = Vector2.zero;
        blr.anchorMax = Vector2.one;
        blr.sizeDelta = Vector2.zero;

        var title = MakeText(header, "Title", "MOGUL");
        _titleText = title.GetComponent<Text>();
        _titleText.fontSize = 22;
        _titleText.fontStyle = FontStyle.Bold;
        _titleText.color = ColorGold;
        _titleText.alignment = TextAnchor.MiddleLeft;
        var tr = title.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0.08f, 0f);
        tr.anchorMax = new Vector2(0.55f, 1f);
        tr.sizeDelta = Vector2.zero;

        var reach = MakeText(header, "Reach", BuildReachLabel());
        _reachText = reach.GetComponent<Text>();
        _reachText.fontSize = 13;
        _reachText.color = ColorMuted;
        _reachText.alignment = TextAnchor.MiddleRight;
        var rr = reach.GetComponent<RectTransform>();
        rr.anchorMin = new Vector2(0.55f, 0f);
        rr.anchorMax = new Vector2(0.98f, 1f);
        rr.sizeDelta = Vector2.zero;
    }

    private void BuildTabBar(GameObject container)
    {
        _tabBar = new GameObject("MainTabs");
        _tabBar.transform.SetParent(container.transform, false);
        var tr = _tabBar.AddComponent<RectTransform>();
        tr.anchorMin = new Vector2(0f, 0.82f);
        tr.anchorMax = new Vector2(1f, 0.9f);
        tr.sizeDelta = Vector2.zero;
        _tabBar.AddComponent<Image>().color = ColorBg;

        _propertiesTabButton = BuildButton(_tabBar, "Tab_Properties", "PROPERTIES",
            new Vector2(0.03f, 0.16f), new Vector2(0.32f, 0.86f),
            ColorGold, ColorDark,
            () => SelectMainTab(MainTab.Properties));

        _ordersTabButton = BuildButton(_tabBar, "Tab_Orders", "ORDERS",
            new Vector2(0.355f, 0.16f), new Vector2(0.645f, 0.86f),
            ColorRow, ColorMuted,
            () => SelectMainTab(MainTab.Orders));

        _questsTabButton = BuildButton(_tabBar, "Tab_Quests", "QUESTS",
            new Vector2(0.68f, 0.16f), new Vector2(0.97f, 0.86f),
            ColorRow, ColorMuted,
            () => SelectMainTab(MainTab.Quests));
    }

    private void BuildHubPanel(GameObject container)
    {
        _hubPanel = new GameObject("HubPanel");
        _hubPanel.transform.SetParent(container.transform, false);
        var r = _hubPanel.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(0f, 0f);
        r.anchorMax = new Vector2(1f, 0.9f);
        r.sizeDelta = Vector2.zero;
        _hubPanel.SetActive(false);

        var shell = new GameObject("HubShell");
        shell.transform.SetParent(_hubPanel.transform, false);
        shell.AddComponent<Image>().color = new Color(0.02f, 0.025f, 0.032f, 0.62f);
        var sr = EnsureRect(shell);
        sr.anchorMin = new Vector2(0.035f, 0.05f);
        sr.anchorMax = new Vector2(0.965f, 0.95f);
        sr.sizeDelta = Vector2.zero;

        var texProperty = LoadModTexture("property.png");
        var texOrders   = LoadModTexture("orders.png");
        var texQuests   = LoadModTexture("quests.png");

        var propCard = BuildHubCard(shell, "PROPERTIES",
            "View and manage your locations.",
            "VIEW PROPERTIES", ColorGreen,
            new Vector2(0.035f, 0.06f), new Vector2(0.325f, 0.93f),
            () => SelectMainTab(MainTab.Properties), texProperty);
        _hubPropertiesCardGroup = propCard.AddComponent<CanvasGroup>();

        BuildHubCard(shell, "ORDERS",
            "Check and fulfill customer orders.",
            "VIEW ORDERS", ColorAccent,
            new Vector2(0.355f, 0.06f), new Vector2(0.645f, 0.93f),
            () => SelectMainTab(MainTab.Orders), texOrders);

        BuildHubCard(shell, "QUESTS",
            "View objectives and rewards.",
            "VIEW QUESTS", ColorPurple,
            new Vector2(0.675f, 0.06f), new Vector2(0.965f, 0.93f),
            () => SelectMainTab(MainTab.Quests), texQuests);
    }

    private GameObject BuildHubCard(GameObject parent, string title, string description,
        string actionLabel, Color accent, Vector2 anchorMin, Vector2 anchorMax, Action onClick,
        Texture2D bgTexture = null)
    {
        var card = new GameObject("HubCard_" + title);
        card.transform.SetParent(parent.transform, false);
        card.AddComponent<Image>().color = new Color(accent.r * 0.25f, accent.g * 0.25f, accent.b * 0.25f, 1f);
        var cr = EnsureRect(card);
        cr.anchorMin = anchorMin;
        cr.anchorMax = anchorMax;
        cr.sizeDelta = Vector2.zero;

        if (bgTexture != null)
        {
            var bgImg = new GameObject("BgImage");
            bgImg.transform.SetParent(card.transform, false);
            var ri = bgImg.AddComponent<RawImage>();
            ri.texture = bgTexture;
            ri.color = new Color(1f, 1f, 1f, 0.38f);
            var bir = EnsureRect(bgImg);
            bir.anchorMin = Vector2.zero;
            bir.anchorMax = Vector2.one;
            bir.sizeDelta = Vector2.zero;
        }

        var button = card.AddComponent<Button>();
        button.onClick.AddListener(onClick);
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
        colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        colors.fadeDuration = 0.10f;
        button.colors = colors;

        var strip = new GameObject("Strip");
        strip.transform.SetParent(card.transform, false);
        strip.AddComponent<Image>().color = accent;
        var sr = EnsureRect(strip);
        sr.anchorMin = new Vector2(0f, 1f);
        sr.anchorMax = new Vector2(1f, 1f);
        sr.pivot = new Vector2(0.5f, 1f);
        sr.sizeDelta = new Vector2(0f, 5f);
        sr.anchoredPosition = Vector2.zero;

        var titleObj = MakeText(card, "Title", title);
        var titleText = titleObj.GetComponent<Text>();
        titleText.fontSize = 15;
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = accent;
        titleText.alignment = TextAnchor.MiddleCenter;
        var tr = titleObj.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0.08f, 0.52f);
        tr.anchorMax = new Vector2(0.92f, 0.66f);
        tr.sizeDelta = Vector2.zero;

        var divider = new GameObject("Divider");
        divider.transform.SetParent(card.transform, false);
        divider.AddComponent<Image>().color = new Color(accent.r, accent.g, accent.b, 0.55f);
        var dr = EnsureRect(divider);
        dr.anchorMin = new Vector2(0.18f, 0.49f);
        dr.anchorMax = new Vector2(0.82f, 0.50f);
        dr.sizeDelta = Vector2.zero;

        var descObj = MakeText(card, "Desc", description);
        var descText = descObj.GetComponent<Text>();
        descText.fontSize = 12;
        descText.color = Color.white;
        descText.alignment = TextAnchor.UpperCenter;
        descText.horizontalOverflow = HorizontalWrapMode.Wrap;
        var descr = descObj.GetComponent<RectTransform>();
        descr.anchorMin = new Vector2(0.10f, 0.25f);
        descr.anchorMax = new Vector2(0.90f, 0.46f);
        descr.sizeDelta = Vector2.zero;

        var open = BuildButton(card, "Open", actionLabel + "  >",
            new Vector2(0.10f, 0.07f), new Vector2(0.90f, 0.20f),
            new Color(accent.r, accent.g, accent.b, 0.30f), Color.white, onClick);
        open.GetComponent<Button>().transition = Selectable.Transition.ColorTint;

        // Outline borders (left / right / bottom — top strip handles the top edge)
        var borderL = new GameObject("BorderL");
        borderL.transform.SetParent(card.transform, false);
        borderL.AddComponent<Image>().color = new Color(accent.r, accent.g, accent.b, 0.45f);
        var blr = EnsureRect(borderL);
        blr.anchorMin = Vector2.zero;
        blr.anchorMax = new Vector2(0f, 1f);
        blr.pivot = new Vector2(0f, 0.5f);
        blr.sizeDelta = new Vector2(2f, 0f);

        var borderR = new GameObject("BorderR");
        borderR.transform.SetParent(card.transform, false);
        borderR.AddComponent<Image>().color = new Color(accent.r, accent.g, accent.b, 0.45f);
        var brr = EnsureRect(borderR);
        brr.anchorMin = new Vector2(1f, 0f);
        brr.anchorMax = Vector2.one;
        brr.pivot = new Vector2(1f, 0.5f);
        brr.sizeDelta = new Vector2(2f, 0f);

        var borderB = new GameObject("BorderB");
        borderB.transform.SetParent(card.transform, false);
        borderB.AddComponent<Image>().color = new Color(accent.r, accent.g, accent.b, 0.45f);
        var bbrr = EnsureRect(borderB);
        bbrr.anchorMin = Vector2.zero;
        bbrr.anchorMax = new Vector2(1f, 0f);
        bbrr.pivot = new Vector2(0.5f, 0f);
        bbrr.sizeDelta = new Vector2(0f, 2f);

        return card;
    }
}
