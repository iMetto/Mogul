using System;
using System.Collections.Generic;
using Il2CppScheduleOne;
using Il2CppScheduleOne.ItemFramework;
using Mogul.Data;
using Mogul.Systems;
using S1API.PhoneApp;
using UnityEngine;
using UnityEngine.UI;

namespace Mogul.Apps;

public partial class MogulApp : PhoneApp
{
    private static readonly List<MogulApp> Instances = new();
    private const float ManageRefreshInterval = 0.75f;

    protected override string AppName => "MogulApp";
    protected override string AppTitle => "Mogul";
    protected override string IconLabel => "MGR";
    protected override string IconFileName => "imetto-Mogul/mogul_icon.png";
    protected override EOrientation Orientation => EOrientation.Horizontal;

    private static readonly Color ColorBg       = new Color(0.025f, 0.030f, 0.040f, 1f);
    private static readonly Color ColorHeader   = new Color(0.040f, 0.048f, 0.060f, 0.98f);
    private static readonly Color ColorPanel    = new Color(0.070f, 0.085f, 0.105f, 0.96f);
    private static readonly Color ColorRow      = new Color(0.095f, 0.115f, 0.140f, 0.92f);
    private static readonly Color ColorRowSel   = new Color(0.145f, 0.120f, 0.055f, 0.96f);
    private static readonly Color ColorRowOwned = new Color(0.065f, 0.115f, 0.085f, 0.94f);
    private static readonly Color ColorGold     = new Color(0.86f, 0.72f, 0.22f, 1f);
    private static readonly Color ColorMuted    = new Color(0.58f, 0.64f, 0.70f, 1f);
    private static readonly Color ColorDark     = new Color(0.025f, 0.030f, 0.040f, 1f);
    private static readonly Color ColorAccent   = new Color(0.34f, 0.62f, 0.90f, 1f);
    private static readonly Color ColorGreen    = new Color(0.42f, 0.80f, 0.27f, 1f);
    private static readonly Color ColorPurple   = new Color(0.68f, 0.36f, 0.92f, 1f);

    private enum MainTab { Properties, Orders, Quests }
    private enum View { Hub, List, Manage, Customize, MixBuilder }

    private Font _font;
    private Text _titleText;
    private Text _reachText;
    private GameObject _backButton;
    private GameObject _tabBar;
    private GameObject _propertiesTabButton;
    private GameObject _ordersTabButton;
    private GameObject _questsTabButton;

    private GameObject _listPanel;
    private GameObject _ordersPanel;
    private GameObject _questPanel;
    private GameObject _managePanel;
    private GameObject _customizePanel;
    private GameObject _mixBuilderPanel;
    private GameObject _hubPanel;
    private CanvasGroup _hubPropertiesCardGroup;
    private RectTransform _listContent;
    private RectTransform _ordersContent;
    private RectTransform _questContent;
    private GameObject _questDetailPanel;

    private View _currentView = View.List;
    private MainTab _mainTab = MainTab.Quests;
    private string _selectedRowId;
    private string _detailLocationId;
    private string _selectedQuestId;
    private string _ordersStatusMessage;
    private float _ordersStatusUntil;
    private float _nextManageRefreshAt;
    private int _strainBaseIndex;
    private readonly List<string> _strainIngredientIds = new();

    protected override void OnCreatedUI(GameObject container)
    {
        try
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildBackground(container);
            BuildHeader(container);
            BuildTabBar(container);
            BuildListPanel(container);
            BuildOrdersPanel(container);
            BuildQuestPanel(container);
            BuildManagePanel(container);
            BuildCustomizePanel(container);
            BuildMixBuilderPanel(container);
            BuildHubPanel(container);
            if (!Instances.Contains(this))
                Instances.Add(this);

            ShowView(View.Hub);
            MogulNetwork.OnDataChanged += _ =>
            {
                RefreshHeader();
                RefreshTabs();
                if (_currentView == View.List && _mainTab == MainTab.Properties) RefreshList();
                if (_currentView == View.List && _mainTab == MainTab.Orders) RefreshOrdersPanel();
                if (_currentView == View.List && _mainTab == MainTab.Quests) RefreshQuestPanel();
                if (_currentView == View.Manage) RefreshManagePanel();
            };
        }
        catch (Exception ex)
        {
            MelonLoader.MelonLogger.Error("[Mogul] OnCreatedUI crashed: " + ex);
        }
    }

    public static void TickOpenManagePanels()
    {
        for (int i = Instances.Count - 1; i >= 0; i--)
        {
            var app = Instances[i];
            if (app == null || app._managePanel == null)
            {
                Instances.RemoveAt(i);
                continue;
            }

            if (app._currentView != View.Manage || !app._managePanel.activeInHierarchy) continue;
            if (Time.time < app._nextManageRefreshAt) continue;

            app._nextManageRefreshAt = Time.time + ManageRefreshInterval;
            app.RefreshManagePanel();
        }
    }

    private void SelectMainTab(MainTab tab)
    {
        if (tab == MainTab.Properties && !IsPropertiesTabAvailable())
            return;
        _mainTab = tab;
        _currentView = View.List;
        _selectedRowId = null;
        _detailLocationId = null;
        ShowView(View.List);
    }

    private void RefreshTabs()
    {
        bool propertiesUnlocked = IsPropertiesTabAvailable();
        if (!propertiesUnlocked && _mainTab == MainTab.Properties)
            _mainTab = MainTab.Quests;
        if (_propertiesTabButton != null)
            _propertiesTabButton.SetActive(propertiesUnlocked);
        SetTabVisual(_propertiesTabButton, _mainTab == MainTab.Properties, ColorGreen);
        SetTabVisual(_ordersTabButton,     _mainTab == MainTab.Orders,     ColorAccent);
        SetTabVisual(_questsTabButton,     _mainTab == MainTab.Quests,     ColorPurple);

        if (_hubPropertiesCardGroup != null)
        {
            _hubPropertiesCardGroup.alpha = propertiesUnlocked ? 1f : 0.35f;
            _hubPropertiesCardGroup.interactable = propertiesUnlocked;
            _hubPropertiesCardGroup.blocksRaycasts = propertiesUnlocked;
        }
    }

    private static bool IsPropertiesTabAvailable()
    {
        return MogulQuestSystem.IsUnlocked(MogulQuestSystem.UnlockPropertiesTab)
            || (MogulNetwork.Data?.RegisteredLocationIds?.Count ?? 0) > 0;
    }

    private void SetTabVisual(GameObject button, bool selected, Color accent)
    {
        if (button == null) return;
        var image = button.GetComponent<Image>();
        if (image != null)
            image.color = selected
                ? new Color(accent.r * 0.55f, accent.g * 0.55f, accent.b * 0.55f, 1f)
                : new Color(accent.r * 0.18f, accent.g * 0.18f, accent.b * 0.18f, 1f);

        var label = button.transform.Find("Label")?.GetComponent<Text>();
        if (label != null)
            label.color = selected ? Color.white : new Color(1f, 1f, 1f, 0.45f);
    }

    private static string BuildReachLabel()
    {
        int reach = MogulNetwork.Data.Reach;
        var tier  = ReachSystem.GetTier(reach);
        return $"REACH  {ReachSystem.FormatReach(reach)}  ·  {ReachSystem.GetTierName(tier).ToUpper()}";
    }

    private void RefreshHeader()
    {
        if (_reachText != null) _reachText.text = BuildReachLabel();
    }

    private void OpenSubview(View view, string locationId)
    {
        _detailLocationId = locationId;
        ShowView(view);
    }

    private void ShowView(View view)
    {
        _currentView = view;
        bool isHub = view == View.Hub;
        bool topLevel = view == View.List;
        if (_hubPanel != null)        _hubPanel.SetActive(isHub);
        if (_tabBar != null)          _tabBar.SetActive(topLevel);
        if (_listPanel != null)       _listPanel.SetActive(topLevel && _mainTab == MainTab.Properties);
        if (_ordersPanel != null)     _ordersPanel.SetActive(topLevel && _mainTab == MainTab.Orders);
        if (_questPanel != null)      _questPanel.SetActive(topLevel && _mainTab == MainTab.Quests);
        if (_managePanel != null)     _managePanel.SetActive(view == View.Manage);
        if (_customizePanel != null)  _customizePanel.SetActive(view == View.Customize);
        if (_mixBuilderPanel != null) _mixBuilderPanel.SetActive(view == View.MixBuilder);
        if (_backButton != null)      _backButton.SetActive(!isHub);
        RefreshTabs();

        var loc = string.IsNullOrEmpty(_detailLocationId) ? null : PropertySystem.Find(_detailLocationId);
        switch (view)
        {
            case View.Hub:
                _titleText.text = "MOGUL";
                _selectedRowId = null;
                break;
            case View.List:
                _titleText.text = _mainTab == MainTab.Properties ? "MOGUL  ·  PROPERTIES"
                    : _mainTab == MainTab.Orders ? "MOGUL  ·  ONLINE ORDERS"
                    : "MOGUL  ·  QUESTS";
                _selectedRowId = null;
                if (_mainTab == MainTab.Properties) RefreshList();
                else if (_mainTab == MainTab.Orders) RefreshOrdersPanel();
                else RefreshQuestPanel();
                break;
            case View.Manage:
                _titleText.text = (loc != null ? loc.Name.ToUpper() : "") + "  ·  MANAGE";
                _nextManageRefreshAt = Time.time + ManageRefreshInterval;
                RefreshManagePanel();
                break;
            case View.Customize:
                _titleText.text = (loc != null ? loc.Name.ToUpper() : "") + "  ·  CUSTOMIZE";
                break;
            case View.MixBuilder:
                _titleText.text = (loc != null ? loc.Name.ToUpper() : "") + "  ·  CREATE MIX";
                RefreshMixBuilderPanel();
                break;
        }
    }

    private static RectTransform EnsureRect(GameObject obj)
    {
        return obj.GetComponent<RectTransform>() ?? obj.AddComponent<RectTransform>();
    }

    private GameObject BuildButton(GameObject parent, string name, string label,
        Vector2 anchorMin, Vector2 anchorMax,
        Color bg, Color fg, Action onClick)
    {
        var btn = new GameObject(name);
        btn.transform.SetParent(parent.transform, false);
        var br = btn.AddComponent<RectTransform>();
        br.anchorMin = anchorMin;
        br.anchorMax = anchorMax;
        br.sizeDelta = Vector2.zero;
        btn.AddComponent<Image>().color = bg;
        var button = btn.AddComponent<Button>();
        button.onClick.AddListener(onClick);
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.86f);
        colors.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        var lo = MakeText(btn, "Label", label);
        var lt = lo.GetComponent<Text>();
        lt.fontSize = 11;
        lt.fontStyle = FontStyle.Bold;
        lt.color = fg;
        lt.alignment = TextAnchor.MiddleCenter;
        var lr = lo.GetComponent<RectTransform>();
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.sizeDelta = Vector2.zero;
        return btn;
    }

    private GameObject BuildSection(GameObject parent, string title,
        Vector2 anchorMin, Vector2 anchorMax, string emptyText)
    {
        var box = new GameObject("Section_" + title);
        box.transform.SetParent(parent.transform, false);
        var br = box.AddComponent<RectTransform>();
        br.anchorMin = anchorMin;
        br.anchorMax = anchorMax;
        br.sizeDelta = Vector2.zero;
        box.AddComponent<Image>().color = ColorRow;

        var titleObj = MakeText(box, "Title", title);
        var titleText = titleObj.GetComponent<Text>();
        titleText.fontSize = 12;
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = ColorGold;
        titleText.alignment = TextAnchor.UpperLeft;
        var tr = titleObj.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0.02f, 0.85f);
        tr.anchorMax = new Vector2(0.98f, 0.98f);
        tr.sizeDelta = Vector2.zero;

        var emptyObj = MakeText(box, "Empty", emptyText);
        var emptyTextC = emptyObj.GetComponent<Text>();
        emptyTextC.fontSize = 12;
        emptyTextC.color = ColorMuted;
        emptyTextC.alignment = TextAnchor.MiddleCenter;
        var er = emptyObj.GetComponent<RectTransform>();
        er.anchorMin = new Vector2(0.02f, 0.05f);
        er.anchorMax = new Vector2(0.98f, 0.85f);
        er.sizeDelta = Vector2.zero;
        return box;
    }

    private GameObject MakeText(GameObject parent, string name, string text)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent.transform, false);
        var t = obj.AddComponent<Text>();
        t.text = text ?? "";
        t.font = _font;
        t.supportRichText = false;
        EnsureRect(obj);
        return obj;
    }
}
