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

public class MogulApp : PhoneApp
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

    private View _currentView = View.List;
    private MainTab _mainTab = MainTab.Quests;
    private string _selectedRowId;
    private string _detailLocationId;
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
        SetTabVisual(_propertiesTabButton, _mainTab == MainTab.Properties);
        SetTabVisual(_ordersTabButton, _mainTab == MainTab.Orders);
        SetTabVisual(_questsTabButton, _mainTab == MainTab.Quests);

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

    private void SetTabVisual(GameObject button, bool selected)
    {
        if (button == null) return;
        var image = button.GetComponent<Image>();
        if (image != null)
            image.color = selected ? ColorGold : ColorRow;

        var label = button.transform.Find("Label")?.GetComponent<Text>();
        if (label != null)
            label.color = selected ? ColorDark : ColorMuted;
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

    private void BuildListPanel(GameObject container)
    {
        _listPanel = new GameObject("ListPanel");
        _listPanel.transform.SetParent(container.transform, false);
        var pr = _listPanel.AddComponent<RectTransform>();
        pr.anchorMin = new Vector2(0f, 0f);
        pr.anchorMax = new Vector2(1f, 0.82f);
        pr.sizeDelta = Vector2.zero;

        var scrollGo = new GameObject("Scroll");
        scrollGo.transform.SetParent(_listPanel.transform, false);
        var srRect = scrollGo.AddComponent<RectTransform>();
        srRect.anchorMin = new Vector2(0.02f, 0.02f);
        srRect.anchorMax = new Vector2(0.98f, 0.98f);
        srRect.sizeDelta = Vector2.zero;

        var scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 30f;

        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollGo.transform, false);
        var vpRect = viewport.AddComponent<RectTransform>();
        vpRect.anchorMin = Vector2.zero;
        vpRect.anchorMax = Vector2.one;
        vpRect.sizeDelta = Vector2.zero;
        // RectMask2D clips by bounds without needing a Graphic — avoids the
        // Mask+near-zero-alpha-Image trap that wipes everything to black.
        viewport.AddComponent<RectMask2D>();
        scrollRect.viewport = vpRect;

        var content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        _listContent = content.AddComponent<RectTransform>();
        _listContent.anchorMin = new Vector2(0f, 1f);
        _listContent.anchorMax = new Vector2(1f, 1f);
        _listContent.pivot = new Vector2(0.5f, 1f);
        _listContent.sizeDelta = Vector2.zero;

        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;   // honour LayoutElement.preferredHeight on rows
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.spacing = 4f;
        vlg.padding = new RectOffset(4, 4, 4, 4);

        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.content = _listContent;

        RefreshList();
    }

    private void BuildQuestPanel(GameObject container)
    {
        _questPanel = new GameObject("QuestPanel");
        _questPanel.transform.SetParent(container.transform, false);
        var pr = _questPanel.AddComponent<RectTransform>();
        pr.anchorMin = new Vector2(0f, 0f);
        pr.anchorMax = new Vector2(1f, 0.82f);
        pr.sizeDelta = Vector2.zero;
        _questPanel.SetActive(false);

        var scrollGo = new GameObject("QuestScroll");
        scrollGo.transform.SetParent(_questPanel.transform, false);
        var srRect = scrollGo.AddComponent<RectTransform>();
        srRect.anchorMin = new Vector2(0.02f, 0.02f);
        srRect.anchorMax = new Vector2(0.98f, 0.98f);
        srRect.sizeDelta = Vector2.zero;

        var scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 30f;

        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollGo.transform, false);
        var vpRect = viewport.AddComponent<RectTransform>();
        vpRect.anchorMin = Vector2.zero;
        vpRect.anchorMax = Vector2.one;
        vpRect.sizeDelta = Vector2.zero;
        viewport.AddComponent<RectMask2D>();
        scrollRect.viewport = vpRect;

        var content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        _questContent = content.AddComponent<RectTransform>();
        _questContent.anchorMin = new Vector2(0f, 1f);
        _questContent.anchorMax = new Vector2(1f, 1f);
        _questContent.pivot = new Vector2(0.5f, 1f);
        _questContent.sizeDelta = Vector2.zero;

        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.spacing = 5f;
        vlg.padding = new RectOffset(4, 4, 4, 4);

        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.content = _questContent;

        RefreshQuestPanel();
    }

    private void BuildOrdersPanel(GameObject container)
    {
        _ordersPanel = new GameObject("OrdersPanel");
        _ordersPanel.transform.SetParent(container.transform, false);
        var pr = _ordersPanel.AddComponent<RectTransform>();
        pr.anchorMin = new Vector2(0f, 0f);
        pr.anchorMax = new Vector2(1f, 0.82f);
        pr.sizeDelta = Vector2.zero;
        _ordersPanel.SetActive(false);

        var scrollGo = new GameObject("OrdersScroll");
        scrollGo.transform.SetParent(_ordersPanel.transform, false);
        var srRect = scrollGo.AddComponent<RectTransform>();
        srRect.anchorMin = new Vector2(0.02f, 0.02f);
        srRect.anchorMax = new Vector2(0.98f, 0.98f);
        srRect.sizeDelta = Vector2.zero;

        var scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 30f;

        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollGo.transform, false);
        var vpRect = viewport.AddComponent<RectTransform>();
        vpRect.anchorMin = Vector2.zero;
        vpRect.anchorMax = Vector2.one;
        vpRect.sizeDelta = Vector2.zero;
        viewport.AddComponent<RectMask2D>();
        scrollRect.viewport = vpRect;

        var content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        _ordersContent = content.AddComponent<RectTransform>();
        _ordersContent.anchorMin = new Vector2(0f, 1f);
        _ordersContent.anchorMax = new Vector2(1f, 1f);
        _ordersContent.pivot = new Vector2(0.5f, 1f);
        _ordersContent.sizeDelta = Vector2.zero;

        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.spacing = 5f;
        vlg.padding = new RectOffset(4, 4, 4, 4);

        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.content = _ordersContent;

        RefreshOrdersPanel();
    }

    private void RefreshList()
    {
        if (_listContent == null) return;
        for (int i = _listContent.childCount - 1; i >= 0; i--)
            GameObject.Destroy(_listContent.GetChild(i).gameObject);

        foreach (var location in PropertySystem.Catalog)
        {
            if (!PropertySystem.IsVisible(location.Id)) continue;
            bool owned = PropertySystem.IsOwned(location.Id);
            bool selected = !owned && _selectedRowId == location.Id;
            BuildRow(location, owned, selected);
        }
    }

    private void RefreshQuestPanel()
    {
        if (_questContent == null) return;
        for (int i = _questContent.childCount - 1; i >= 0; i--)
            GameObject.Destroy(_questContent.GetChild(i).gameObject);

        BuildQuestHeader("QUESTS");
        foreach (var quest in MogulQuestSystem.GetAvailable(MogulObjectiveType.Quest, MogulNetwork.Data))
            BuildQuestRow(quest);

        BuildQuestHeader("TASKS");
        foreach (var task in MogulQuestSystem.GetAvailable(MogulObjectiveType.Task, MogulNetwork.Data))
            BuildQuestRow(task);
    }

    private void RefreshOrdersPanel()
    {
        if (_ordersContent == null) return;
        for (int i = _ordersContent.childCount - 1; i >= 0; i--)
            GameObject.Destroy(_ordersContent.GetChild(i).gameObject);

        if (!string.IsNullOrEmpty(_ordersStatusMessage) && Time.time < _ordersStatusUntil)
            BuildOrderStatusRow(_ordersStatusMessage);

        var orders = OnlineOrderSystem.GetOrders();
        bool any = false;
        for (int i = orders.Count - 1; i >= 0; i--)
        {
            if (orders[i].Status == "Open")
            {
                BuildOrderRow(orders[i]);
                any = true;
            }
        }

        if (!any)
        {
            var row = new GameObject("OrdersEmpty");
            row.transform.SetParent(_ordersContent, false);
            var le = row.AddComponent<LayoutElement>();
            le.preferredHeight = 80f;
            le.minHeight = 80f;
            row.AddComponent<Image>().color = ColorRow;
            var textObj = MakeText(row, "Text", "No live online orders. Stock an owned location and new buyers will appear.");
            var text = textObj.GetComponent<Text>();
            text.fontSize = 12;
            text.color = ColorMuted;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            var tr = textObj.GetComponent<RectTransform>();
            tr.anchorMin = new Vector2(0.06f, 0.12f);
            tr.anchorMax = new Vector2(0.94f, 0.88f);
            tr.sizeDelta = Vector2.zero;
        }
    }

    private void BuildOrderStatusRow(string message)
    {
        var row = new GameObject("OrdersStatus");
        row.transform.SetParent(_ordersContent, false);
        var le = row.AddComponent<LayoutElement>();
        le.preferredHeight = 42f;
        le.minHeight = 42f;
        row.AddComponent<Image>().color = ColorRowSel;
        var textObj = MakeText(row, "Text", message);
        var text = textObj.GetComponent<Text>();
        text.fontSize = 12;
        text.color = ColorGold;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        var tr = textObj.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0.04f, 0.12f);
        tr.anchorMax = new Vector2(0.96f, 0.88f);
        tr.sizeDelta = Vector2.zero;
    }

    private void BuildOrderRow(OnlineOrderData order)
    {
        var profile = CustomerTypes.Get(order.CustomerTypeId);
        var loc = PropertySystem.Find(order.LocationId);
        var row = new GameObject("Order_" + order.Id);
        row.transform.SetParent(_ordersContent, false);
        row.AddComponent<Image>().color = ColorRow;
        var le = row.AddComponent<LayoutElement>();
        le.preferredHeight = 116f;
        le.minHeight = 116f;

        var accent = new GameObject("Accent");
        accent.transform.SetParent(row.transform, false);
        accent.AddComponent<Image>().color = profile.Type == MogulCustomerType.Importer
            ? ColorGold
            : profile.Type == MogulCustomerType.GangLeader ? ColorAccent : ColorMuted;
        var ar = accent.GetComponent<RectTransform>();
        ar.anchorMin = new Vector2(0f, 0.1f);
        ar.anchorMax = new Vector2(0.006f, 0.9f);
        ar.sizeDelta = Vector2.zero;

        var titleObj = MakeText(row, "Title", $"{profile.DisplayName.ToUpper()} · {order.CustomerName}");
        var title = titleObj.GetComponent<Text>();
        title.fontSize = 13;
        title.fontStyle = FontStyle.Bold;
        title.color = Color.white;
        title.alignment = TextAnchor.UpperLeft;
        var tr = titleObj.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0.025f, 0.70f);
        tr.anchorMax = new Vector2(0.68f, 0.94f);
        tr.sizeDelta = Vector2.zero;

        string lineText = BuildOrderLineText(order);
        var linesObj = MakeText(row, "Lines", lineText);
        var lines = linesObj.GetComponent<Text>();
        lines.fontSize = 11;
        lines.color = new Color(0.84f, 0.84f, 0.84f, 1f);
        lines.alignment = TextAnchor.UpperLeft;
        lines.horizontalOverflow = HorizontalWrapMode.Wrap;
        lines.verticalOverflow = VerticalWrapMode.Truncate;
        var lr = linesObj.GetComponent<RectTransform>();
        lr.anchorMin = new Vector2(0.025f, 0.23f);
        lr.anchorMax = new Vector2(0.66f, 0.70f);
        lr.sizeDelta = Vector2.zero;

        string locName = loc != null ? loc.Name : order.LocationId;
        var metaObj = MakeText(row, "Meta",
            $"{locName} · due day {order.DeadlineDay} {order.DeadlineTime:0000} · ${order.Total + order.Tip:0}");
        var meta = metaObj.GetComponent<Text>();
        meta.fontSize = 11;
        meta.color = ColorMuted;
        meta.alignment = TextAnchor.UpperLeft;
        var mr = metaObj.GetComponent<RectTransform>();
        mr.anchorMin = new Vector2(0.025f, 0.05f);
        mr.anchorMax = new Vector2(0.66f, 0.23f);
        mr.sizeDelta = Vector2.zero;

        BuildButton(row, "Fulfill", "FULFILL",
            new Vector2(0.70f, 0.50f), new Vector2(0.96f, 0.82f),
            ColorGold, ColorDark,
            () =>
            {
                if (MogulNetwork.RequestFulfillOnlineOrder(order.Id, out var error))
                    ShowOrderStatus(MogulNetwork.IsHost ? "Order fulfilled." : "Fulfillment requested.");
                else
                    ShowOrderStatus("Cannot fulfill: " + (error ?? "requirements not met"));
                RefreshOrdersPanel();
            });

        BuildButton(row, "Dismiss", "DECLINE",
            new Vector2(0.70f, 0.16f), new Vector2(0.96f, 0.44f),
            ColorRowSel, ColorMuted,
            () =>
            {
                MogulNetwork.RequestAction(MogulActions.DismissOnlineOrder, order.Id);
                ShowOrderStatus("Order declined.");
                RefreshOrdersPanel();
            });
    }

    private void ShowOrderStatus(string message)
    {
        _ordersStatusMessage = message;
        _ordersStatusUntil = Time.time + 5f;
    }

    private static string BuildOrderLineText(OnlineOrderData order)
    {
        var lines = new List<string>();
        foreach (var line in order.Lines)
            lines.Add($"{line.Quantity}x {line.DisplayName} ${line.Price:0.##} · Total: ${line.Price * line.Quantity:0.##}");
        return string.Join("\n", lines);
    }

    private void BuildQuestHeader(string label)
    {
        var row = new GameObject("Header_" + label);
        row.transform.SetParent(_questContent, false);
        var le = row.AddComponent<LayoutElement>();
        le.preferredHeight = 26f;
        le.minHeight = 26f;

        var textObj = MakeText(row, "Label", label);
        var text = textObj.GetComponent<Text>();
        text.fontSize = 12;
        text.fontStyle = FontStyle.Bold;
        text.color = ColorGold;
        text.alignment = TextAnchor.MiddleLeft;
        var tr = textObj.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0.025f, 0f);
        tr.anchorMax = new Vector2(0.98f, 1f);
        tr.sizeDelta = Vector2.zero;
    }

    private void BuildQuestRow(MogulQuestDefinition quest)
    {
        var data = MogulNetwork.Data;
        int progress = MogulQuestSystem.GetProgress(quest, data);
        bool active = data.ActiveQuestId == quest.Id;
        bool complete = MogulQuestSystem.IsComplete(quest, data);
        bool claimed = MogulQuestSystem.IsClaimed(quest, data);

        var row = new GameObject("Quest_" + quest.Id);
        row.transform.SetParent(_questContent, false);
        var rowImg = row.AddComponent<Image>();
        rowImg.color = claimed ? ColorRowOwned : active ? ColorRowSel : ColorRow;
        var le = row.AddComponent<LayoutElement>();
        le.preferredHeight = 102f;
        le.minHeight = 102f;

        var accent = new GameObject("Accent");
        accent.transform.SetParent(row.transform, false);
        accent.AddComponent<Image>().color = claimed ? ColorMuted : complete ? ColorGold : ColorAccent;
        var ar = accent.GetComponent<RectTransform>();
        ar.anchorMin = new Vector2(0f, 0.1f);
        ar.anchorMax = new Vector2(0.006f, 0.9f);
        ar.sizeDelta = Vector2.zero;

        var titleObj = MakeText(row, "Title", quest.Title.ToUpper());
        var titleText = titleObj.GetComponent<Text>();
        titleText.fontSize = 13;
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = Color.white;
        titleText.alignment = TextAnchor.UpperLeft;
        var tr = titleObj.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0.025f, 0.68f);
        tr.anchorMax = new Vector2(0.6f, 0.94f);
        tr.sizeDelta = Vector2.zero;

        var descObj = MakeText(row, "Description", quest.Description);
        var descText = descObj.GetComponent<Text>();
        descText.fontSize = 11;
        descText.color = new Color(0.82f, 0.82f, 0.82f, 1f);
        descText.alignment = TextAnchor.UpperLeft;
        descText.horizontalOverflow = HorizontalWrapMode.Wrap;
        descText.verticalOverflow = VerticalWrapMode.Truncate;
        var dr = descObj.GetComponent<RectTransform>();
        dr.anchorMin = new Vector2(0.025f, 0.28f);
        dr.anchorMax = new Vector2(0.62f, 0.68f);
        dr.sizeDelta = Vector2.zero;

        string status = claimed
            ? $"CLAIMED · +{quest.ReachReward} reach"
            : $"{quest.Objective}: {Math.Min(progress, quest.Target)}/{quest.Target} · +{quest.ReachReward} reach";
        var statusObj = MakeText(row, "Status", status);
        var statusText = statusObj.GetComponent<Text>();
        statusText.fontSize = 11;
        statusText.color = complete && !claimed ? ColorGold : ColorMuted;
        statusText.alignment = TextAnchor.UpperLeft;
        var sr = statusObj.GetComponent<RectTransform>();
        sr.anchorMin = new Vector2(0.025f, 0.06f);
        sr.anchorMax = new Vector2(0.64f, 0.28f);
        sr.sizeDelta = Vector2.zero;

        string buttonText = claimed ? "CLAIMED" : complete ? "CLAIM" : active ? "TRACKING" : "TRACK";
        Color buttonColor = claimed ? ColorRowOwned : complete ? ColorGold : active ? ColorAccent : ColorRow;
        Color textColor = complete ? ColorDark : active ? Color.white : ColorMuted;
        BuildButton(row, "QuestAction", buttonText,
            new Vector2(0.68f, 0.24f), new Vector2(0.96f, 0.76f),
            buttonColor, textColor,
            () =>
            {
                if (claimed) return;
                if (complete)
                    MogulQuestSystem.RequestClaim(quest.Id);
                else
                    MogulQuestSystem.RequestTrack(quest.Id);
                RefreshQuestPanel();
            });
    }

    private void BuildRow(MogulLocation location, bool owned, bool selected)
    {
        float h = selected ? 110f : 56f;
        bool locked = !owned && !PropertySystem.IsPurchasable(location.Id);

        var row = new GameObject("Row_" + location.Id);
        row.transform.SetParent(_listContent, false);
        var rowImg = row.AddComponent<Image>();
        rowImg.color = locked ? ColorHeader : owned ? ColorRowOwned : (selected ? ColorRowSel : ColorRow);
        var le = row.AddComponent<LayoutElement>();
        le.preferredHeight = h;
        le.minHeight = h;

        var accent = new GameObject("Accent");
        accent.transform.SetParent(row.transform, false);
        accent.AddComponent<Image>().color = locked ? ColorMuted : owned ? ColorMuted : ColorGold;
        var ar = accent.GetComponent<RectTransform>();
        ar.anchorMin = new Vector2(0f, 0.1f);
        ar.anchorMax = new Vector2(0.006f, 0.9f);
        ar.sizeDelta = Vector2.zero;

        var nameObj = MakeText(row, "Name", location.Name.ToUpper());
        var nameText = nameObj.GetComponent<Text>();
        nameText.fontSize = 14;
        nameText.fontStyle = FontStyle.Bold;
        nameText.color = Color.white;
        nameText.alignment = TextAnchor.MiddleLeft;
        var nr = nameObj.GetComponent<RectTransform>();
        if (owned)
        {
            nr.anchorMin = new Vector2(0.025f, 0f);
            nr.anchorMax = new Vector2(0.5f, 1f);
        }
        else if (selected)
        {
            nr.anchorMin = new Vector2(0.025f, 0.7f);
            nr.anchorMax = new Vector2(0.55f, 1f);
        }
        else
        {
            nr.anchorMin = new Vector2(0.025f, 0f);
            nr.anchorMax = new Vector2(0.55f, 1f);
        }
        nr.sizeDelta = Vector2.zero;

        if (owned)
        {
            BuildButton(row, "Manage", "MANAGE",
                new Vector2(0.5f, 0.18f), new Vector2(0.73f, 0.82f),
                ColorGold, ColorDark,
                () => OpenSubview(View.Manage, location.Id));

            BuildButton(row, "Customize", "CUSTOMIZE",
                new Vector2(0.75f, 0.18f), new Vector2(0.98f, 0.82f),
                ColorAccent, Color.white,
                () => OpenSubview(View.Customize, location.Id));
        }
        else if (!selected)
        {
            var priceObj = MakeText(row, "Price", locked ? "LOCKED" : "$" + location.Price.ToString("N0"));
            var priceText = priceObj.GetComponent<Text>();
            priceText.fontSize = 13;
            priceText.color = locked ? ColorMuted : ColorGold;
            priceText.alignment = TextAnchor.MiddleRight;
            var pr2 = priceObj.GetComponent<RectTransform>();
            pr2.anchorMin = new Vector2(0.6f, 0f);
            pr2.anchorMax = new Vector2(0.97f, 1f);
            pr2.sizeDelta = Vector2.zero;

            var clickGo = new GameObject("Click");
            clickGo.transform.SetParent(row.transform, false);
            var cr = clickGo.AddComponent<RectTransform>();
            cr.anchorMin = Vector2.zero;
            cr.anchorMax = Vector2.one;
            cr.sizeDelta = Vector2.zero;
            var clickImg = clickGo.AddComponent<Image>();
            clickImg.color = new Color(0f, 0f, 0f, 0f);
            clickImg.raycastTarget = true;
            clickGo.AddComponent<Button>().onClick.AddListener(new Action(() =>
            {
                if (locked) return;
                _selectedRowId = location.Id;
                RefreshList();
            }));
        }
        else
        {
            var descObj = MakeText(row, "Desc", location.Description);
            var descText = descObj.GetComponent<Text>();
            descText.fontSize = 12;
            descText.color = new Color(0.85f, 0.85f, 0.85f, 1f);
            descText.alignment = TextAnchor.UpperLeft;
            descText.horizontalOverflow = HorizontalWrapMode.Wrap;
            descText.verticalOverflow = VerticalWrapMode.Truncate;
            var dr = descObj.GetComponent<RectTransform>();
            dr.anchorMin = new Vector2(0.025f, 0.34f);
            dr.anchorMax = new Vector2(0.7f, 0.7f);
            dr.sizeDelta = Vector2.zero;

            var roomSize = BuildingPreview.GetEffectiveRoomSize(location);
            var sizeObj = MakeText(row, "Size",
                $"{roomSize.x:F0} × {roomSize.z:F0} m  ·  Door: {location.Door}");
            var sizeText = sizeObj.GetComponent<Text>();
            sizeText.fontSize = 11;
            sizeText.color = ColorMuted;
            sizeText.alignment = TextAnchor.UpperLeft;
            var sr = sizeObj.GetComponent<RectTransform>();
            sr.anchorMin = new Vector2(0.025f, 0.06f);
            sr.anchorMax = new Vector2(0.7f, 0.32f);
            sr.sizeDelta = Vector2.zero;

            var priceObj = MakeText(row, "Price", "$" + location.Price.ToString("N0"));
            var priceText = priceObj.GetComponent<Text>();
            priceText.fontSize = 16;
            priceText.fontStyle = FontStyle.Bold;
            priceText.color = ColorGold;
            priceText.alignment = TextAnchor.MiddleRight;
            var pr2 = priceObj.GetComponent<RectTransform>();
            pr2.anchorMin = new Vector2(0.55f, 0.7f);
            pr2.anchorMax = new Vector2(0.97f, 1f);
            pr2.sizeDelta = Vector2.zero;

            BuildButton(row, "Confirm", "CONFIRM PURCHASE",
                new Vector2(0.7f, 0.12f), new Vector2(0.97f, 0.55f),
                ColorGold, ColorDark,
                () =>
                {
                    string error;
                    try { error = PropertySystem.TryPurchaseWithDesign(location.Id, "classic"); }
                    catch (Exception ex)
                    {
                        MelonLoader.MelonLogger.Error("[Mogul] TryPurchase threw: " + ex.Message);
                        return;
                    }
                    if (error != null)
                    {
                        MelonLoader.MelonLogger.Warning("[Mogul] Purchase failed: " + error);
                        return;
                    }
                    _selectedRowId = null;
                    RefreshList();
                });

            var collapseGo = new GameObject("Collapse");
            collapseGo.transform.SetParent(row.transform, false);
            var cor = collapseGo.AddComponent<RectTransform>();
            cor.anchorMin = new Vector2(0f, 0.7f);
            cor.anchorMax = new Vector2(0.7f, 1f);
            cor.sizeDelta = Vector2.zero;
            var ci = collapseGo.AddComponent<Image>();
            ci.color = new Color(0f, 0f, 0f, 0f);
            ci.raycastTarget = true;
            collapseGo.AddComponent<Button>().onClick.AddListener(new Action(() =>
            {
                _selectedRowId = null;
                RefreshList();
            }));
        }
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

        var title = MakeText(shell, "Title", "MOGUL");
        var titleText = title.GetComponent<Text>();
        titleText.fontSize = 28;
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = Color.white;
        titleText.alignment = TextAnchor.UpperLeft;
        var tr = title.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0.035f, 0.82f);
        tr.anchorMax = new Vector2(0.45f, 0.96f);
        tr.sizeDelta = Vector2.zero;

        var sub = MakeText(shell, "Subtitle", "Management hub");
        var subText = sub.GetComponent<Text>();
        subText.fontSize = 13;
        subText.color = ColorMuted;
        subText.alignment = TextAnchor.UpperLeft;
        var subr = sub.GetComponent<RectTransform>();
        subr.anchorMin = new Vector2(0.04f, 0.76f);
        subr.anchorMax = new Vector2(0.5f, 0.84f);
        subr.sizeDelta = Vector2.zero;

        var propCard = BuildHubCard(shell, "P", "PROPERTIES",
            "View and manage your locations.",
            "VIEW PROPERTIES", ColorGreen,
            new Vector2(0.035f, 0.06f), new Vector2(0.325f, 0.74f),
            () => SelectMainTab(MainTab.Properties));
        _hubPropertiesCardGroup = propCard.AddComponent<CanvasGroup>();

        BuildHubCard(shell, "O", "ORDERS",
            "Check and fulfill customer orders.",
            "VIEW ORDERS", ColorAccent,
            new Vector2(0.355f, 0.06f), new Vector2(0.645f, 0.74f),
            () => SelectMainTab(MainTab.Orders));

        BuildHubCard(shell, "!", "QUESTS",
            "View objectives and rewards.",
            "VIEW QUESTS", ColorPurple,
            new Vector2(0.675f, 0.06f), new Vector2(0.965f, 0.74f),
            () => SelectMainTab(MainTab.Quests));
    }

    private GameObject BuildHubCard(GameObject parent, string icon, string title, string description,
        string actionLabel, Color accent, Vector2 anchorMin, Vector2 anchorMax, Action onClick)
    {
        var card = new GameObject("HubCard_" + title);
        card.transform.SetParent(parent.transform, false);
        card.AddComponent<Image>().color = new Color(accent.r * 0.10f, accent.g * 0.10f, accent.b * 0.10f, 0.58f);
        var cr = EnsureRect(card);
        cr.anchorMin = anchorMin;
        cr.anchorMax = anchorMax;
        cr.sizeDelta = Vector2.zero;
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

        var iconObj = MakeText(card, "Icon", icon);
        var iconText = iconObj.GetComponent<Text>();
        iconText.fontSize = 42;
        iconText.fontStyle = FontStyle.Bold;
        iconText.color = accent;
        iconText.alignment = TextAnchor.MiddleCenter;
        var ir = iconObj.GetComponent<RectTransform>();
        ir.anchorMin = new Vector2(0.08f, 0.68f);
        ir.anchorMax = new Vector2(0.92f, 0.90f);
        ir.sizeDelta = Vector2.zero;

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

        return card;
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

    private void BuildManagePanel(GameObject container)
    {
        _managePanel = new GameObject("ManagePanel");
        _managePanel.transform.SetParent(container.transform, false);
        var r = _managePanel.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(0f, 0f);
        r.anchorMax = new Vector2(1f, 0.9f);
        r.sizeDelta = Vector2.zero;
        _managePanel.SetActive(false);
    }

    private void BuildCustomizePanel(GameObject container)
    {
        _customizePanel = new GameObject("CustomizePanel");
        _customizePanel.transform.SetParent(container.transform, false);
        var r = _customizePanel.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(0f, 0f);
        r.anchorMax = new Vector2(1f, 0.9f);
        r.sizeDelta = Vector2.zero;
        _customizePanel.SetActive(false);

        BuildSection(_customizePanel, "DECORATIONS",
            new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.95f),
            "(no decorations available yet)");
    }

    private void BuildMixBuilderPanel(GameObject container)
    {
        _mixBuilderPanel = new GameObject("MixBuilderPanel");
        _mixBuilderPanel.transform.SetParent(container.transform, false);
        var r = _mixBuilderPanel.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(0f, 0f);
        r.anchorMax = new Vector2(1f, 0.9f);
        r.sizeDelta = Vector2.zero;
        _mixBuilderPanel.SetActive(false);
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

    private void RefreshManagePanel()
    {
        if (_managePanel == null) return;
        for (int i = _managePanel.transform.childCount - 1; i >= 0; i--)
            GameObject.Destroy(_managePanel.transform.GetChild(i).gameObject);

        TryBuildManageSection(BuildInventorySection, "INVENTORY",
            new Vector2(0.04f, 0.52f), new Vector2(0.96f, 0.95f));
        TryBuildManageSection(BuildBudtenderOrderSection, "ORDERS",
            new Vector2(0.04f, 0.25f), new Vector2(0.96f, 0.49f));
        TryBuildManageSection(BuildEmployeeSection, "EMPLOYEES",
            new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.20f));
    }

    private void TryBuildManageSection(Action build, string label, Vector2 anchorMin, Vector2 anchorMax)
    {
        try
        {
            build();
        }
        catch (Exception ex)
        {
            MelonLoader.MelonLogger.Warning($"[Mogul] Manage section {label} failed: {ex.Message}");
            BuildSection(_managePanel, label, anchorMin, anchorMax, "(section unavailable)");
        }
    }

    private void BuildInventorySection()
    {
        var box = BuildSection(_managePanel, "INVENTORY",
            new Vector2(0.04f, 0.52f), new Vector2(0.96f, 0.95f),
            "");

        float mult = PricingSystem.GetLocationMultiplier(_detailLocationId);
        var globalObj = MakeText(box, "GlobalPrice", $"Store multiplier  x{mult:0.0}");
        var globalText = globalObj.GetComponent<Text>();
        globalText.fontSize = 11;
        globalText.fontStyle = FontStyle.Bold;
        globalText.color = ColorGold;
        globalText.alignment = TextAnchor.MiddleLeft;
        var gr = globalObj.GetComponent<RectTransform>();
        gr.anchorMin = new Vector2(0.03f, 0.75f);
        gr.anchorMax = new Vector2(0.45f, 0.88f);
        gr.sizeDelta = Vector2.zero;

        BuildButton(box, "StoreMultDown", "-0.1",
            new Vector2(0.48f, 0.76f), new Vector2(0.58f, 0.88f),
            ColorRow, ColorGold,
            () =>
            {
                PricingSystem.RequestSetLocationMultiplier(_detailLocationId, mult - PricingSystem.MultiplierStep);
                RefreshManagePanel();
            });

        BuildButton(box, "StoreMultUp", "+0.1",
            new Vector2(0.60f, 0.76f), new Vector2(0.70f, 0.88f),
            ColorRow, ColorGold,
            () =>
            {
                PricingSystem.RequestSetLocationMultiplier(_detailLocationId, mult + PricingSystem.MultiplierStep);
                RefreshManagePanel();
            });

        BuildInventoryPricingRows(box);
    }

    private void BuildInventoryPricingRows(GameObject box)
    {
        var stock = new List<StorageProduct>();

        if (LocationSpawner.TryGetSpawnedBuilding(_detailLocationId, out var buildingRoot) && buildingRoot != null)
            stock = StorageScanner.Scan(buildingRoot);
        stock.AddRange(StorageScanner.ScanVirtual(_detailLocationId));

        if (stock.Count == 0)
        {
            var emptyObj = MakeText(box, "StockEmpty", "(no stocked products in this location)");
            var empty = emptyObj.GetComponent<Text>();
            empty.fontSize = 12;
            empty.color = ColorMuted;
            empty.alignment = TextAnchor.MiddleCenter;
            var er = emptyObj.GetComponent<RectTransform>();
            er.anchorMin = new Vector2(0.03f, 0.08f);
            er.anchorMax = new Vector2(0.97f, 0.70f);
            er.sizeDelta = Vector2.zero;
            return;
        }

        int count = Math.Min(stock.Count, 3);
        for (int i = 0; i < count; i++)
        {
            float top = 0.70f - i * 0.20f;
            try
            {
                BuildInventoryPricingRow(box, stock[i], top);
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Warning($"[Mogul] Inventory pricing row failed for {stock[i]?.ProductId ?? "(null)"}: {ex}");
                BuildInventorySummaryRow(box, stock[i], top);
            }
        }

        if (stock.Count > count)
        {
            var moreObj = MakeText(box, "MoreStock", $"+ {stock.Count - count} more products");
            var more = moreObj.GetComponent<Text>();
            more.fontSize = 10;
            more.color = ColorMuted;
            more.alignment = TextAnchor.MiddleLeft;
            var mr = moreObj.GetComponent<RectTransform>();
            mr.anchorMin = new Vector2(0.03f, 0.03f);
            mr.anchorMax = new Vector2(0.50f, 0.10f);
            mr.sizeDelta = Vector2.zero;
        }
    }

    private void BuildInventoryPricingRow(GameObject parent, StorageProduct item, float top)
    {
        if (parent == null || item == null || string.IsNullOrEmpty(item.ProductId)) return;

        string productId = item.ProductId;
        int qualityLevel = item.QualityLevel;
        string displayName = string.IsNullOrEmpty(item.DisplayName) ? productId : item.DisplayName;
        float bottom = top - 0.17f;
        var row = new GameObject("PriceRow_" + productId + "_" + qualityLevel);
        row.transform.SetParent(parent.transform, false);
        var rr = row.AddComponent<RectTransform>();
        rr.anchorMin = new Vector2(0.03f, bottom);
        rr.anchorMax = new Vector2(0.97f, top);
        rr.sizeDelta = Vector2.zero;
        row.AddComponent<Image>().color = ColorHeader;

        var nameObj = MakeText(row, "Name", $"{displayName} · {item.QualityName} · {item.TotalPackages} pkg");
        var name = nameObj.GetComponent<Text>();
        name.fontSize = 10;
        name.fontStyle = FontStyle.Bold;
        name.color = Color.white;
        name.alignment = TextAnchor.UpperLeft;
        var nr = nameObj.GetComponent<RectTransform>();
        nr.anchorMin = new Vector2(0.02f, 0.54f);
        nr.anchorMax = new Vector2(0.40f, 0.96f);
        nr.sizeDelta = Vector2.zero;

        float productMult = PricingSystem.GetProductMultiplier(_detailLocationId, productId, qualityLevel);
        float manual = PricingSystem.GetManualPrice(_detailLocationId, productId, qualityLevel);
        string priceLabel = manual >= 0f
            ? $"Base ${item.BasePrice:0}  ->  Manual ${item.Price:0}"
            : $"Base ${item.BasePrice:0}  ->  x{productMult:0.0} = ${item.Price:0}";
        var priceObj = MakeText(row, "Price", priceLabel);
        var price = priceObj.GetComponent<Text>();
        price.fontSize = 10;
        price.color = ColorMuted;
        price.alignment = TextAnchor.UpperLeft;
        var pr = priceObj.GetComponent<RectTransform>();
        pr.anchorMin = new Vector2(0.02f, 0.08f);
        pr.anchorMax = new Vector2(0.43f, 0.50f);
        pr.sizeDelta = Vector2.zero;

        BuildButton(row, "ItemMultDown", "-x",
            new Vector2(0.45f, 0.56f), new Vector2(0.53f, 0.92f),
            ColorRow, ColorGold,
            () =>
            {
                PricingSystem.RequestSetProductMultiplier(_detailLocationId, productId, qualityLevel, productMult - PricingSystem.MultiplierStep);
                RefreshManagePanel();
            });
        BuildButton(row, "ItemMultUp", "+x",
            new Vector2(0.54f, 0.56f), new Vector2(0.62f, 0.92f),
            ColorRow, ColorGold,
            () =>
            {
                PricingSystem.RequestSetProductMultiplier(_detailLocationId, productId, qualityLevel, productMult + PricingSystem.MultiplierStep);
                RefreshManagePanel();
            });

        float currentManual = manual >= 0f ? manual : item.Price;
        BuildButton(row, "PriceDown", "-$",
            new Vector2(0.66f, 0.56f), new Vector2(0.74f, 0.92f),
            ColorRow, ColorGold,
            () =>
            {
                PricingSystem.RequestSetManualPrice(_detailLocationId, productId, qualityLevel, currentManual - PricingSystem.PriceStep);
                RefreshManagePanel();
            });
        BuildButton(row, "PriceUp", "+$",
            new Vector2(0.75f, 0.56f), new Vector2(0.83f, 0.92f),
            ColorRow, ColorGold,
            () =>
            {
                PricingSystem.RequestSetManualPrice(_detailLocationId, productId, qualityLevel, currentManual + PricingSystem.PriceStep);
                RefreshManagePanel();
            });
        BuildButton(row, "Auto", "AUTO",
            new Vector2(0.85f, 0.56f), new Vector2(0.98f, 0.92f),
            manual >= 0f ? ColorAccent : ColorRowOwned,
            manual >= 0f ? Color.white : ColorMuted,
            () =>
            {
                PricingSystem.RequestClearManualPrice(_detailLocationId, productId, qualityLevel);
                RefreshManagePanel();
            });
    }

    private void BuildInventorySummaryRow(GameObject parent, StorageProduct item, float top)
    {
        if (parent == null || item == null || string.IsNullOrEmpty(item.ProductId)) return;

        float bottom = top - 0.17f;
        var row = new GameObject("InventoryRow_" + item.ProductId + "_" + item.QualityLevel);
        row.transform.SetParent(parent.transform, false);
        var rr = row.AddComponent<RectTransform>();
        rr.anchorMin = new Vector2(0.03f, bottom);
        rr.anchorMax = new Vector2(0.97f, top);
        rr.sizeDelta = Vector2.zero;
        row.AddComponent<Image>().color = ColorHeader;

        string displayName = string.IsNullOrEmpty(item.DisplayName) ? item.ProductId : item.DisplayName;
        var textObj = MakeText(row, "Text", $"{displayName} · {item.QualityName} · {item.TotalPackages} pkg · ${item.Price:0}");
        var text = textObj.GetComponent<Text>();
        text.fontSize = 11;
        text.fontStyle = FontStyle.Bold;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        var tr = textObj.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0.03f, 0.08f);
        tr.anchorMax = new Vector2(0.97f, 0.92f);
        tr.sizeDelta = Vector2.zero;
    }

    private void BuildBudtenderOrderSection()
    {
        var box = BuildSection(_managePanel, "ORDERS",
            new Vector2(0.04f, 0.25f), new Vector2(0.96f, 0.49f),
            "");

        bool hasBudtender = EmployeeSystem.HasRole(_detailLocationId, EmployeeRole.Budtender);
        var order = EmployeeSystem.GetBudtenderOrder(_detailLocationId);
        int maxIngredients = StrainMixingSystem.MaxIngredientSlots;
        if (_strainBaseIndex < 0 || _strainBaseIndex >= EmployeeProduction.BudtenderProducts.Length)
            _strainBaseIndex = 0;
        while (_strainIngredientIds.Count > maxIngredients)
            _strainIngredientIds.RemoveAt(_strainIngredientIds.Count - 1);

        string status = !hasBudtender
            ? "Hire a budtender to place production orders"
            : order == null
                ? $"Build strain · {maxIngredients} mix slot{(maxIngredients == 1 ? "" : "s")} unlocked"
                : $"{StrainMixingSystem.BuildOrderDisplayName(order)} · {order.Quantity} pkg · ready day {order.ReadyDay} after 17:00";

        var statusObj = MakeText(box, "OrderStatus", status);
        var statusText = statusObj.GetComponent<Text>();
        statusText.fontSize = 11;
        statusText.color = order == null ? ColorMuted : ColorGold;
        statusText.alignment = TextAnchor.UpperLeft;
        statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
        statusText.verticalOverflow = VerticalWrapMode.Truncate;
        var sr = statusObj.GetComponent<RectTransform>();
        sr.anchorMin = new Vector2(0.03f, 0.77f);
        sr.anchorMax = new Vector2(0.97f, 0.94f);
        sr.sizeDelta = Vector2.zero;

        if (!hasBudtender)
            return;

        bool enabled = hasBudtender && order == null;
        if (!enabled)
            return;

        for (int i = 0; i < EmployeeProduction.BudtenderProducts.Length; i++)
        {
            float x0 = 0.03f + i * 0.235f;
            BuildBaseButton(box, i, new Vector2(x0, 0.54f), new Vector2(x0 + 0.215f, 0.72f), enabled);
        }

        var selectedBase = EmployeeProduction.BudtenderProducts[_strainBaseIndex];
        string preview = $"Selected base: {selectedBase.DisplayName}";
        var previewObj = MakeText(box, "RecipePreview", preview);
        var previewText = previewObj.GetComponent<Text>();
        previewText.fontSize = 10;
        previewText.color = ColorMuted;
        previewText.alignment = TextAnchor.MiddleLeft;
        previewText.horizontalOverflow = HorizontalWrapMode.Wrap;
        previewText.verticalOverflow = VerticalWrapMode.Truncate;
        var pr = previewObj.GetComponent<RectTransform>();
        pr.anchorMin = new Vector2(0.03f, 0.04f);
        pr.anchorMax = new Vector2(0.57f, 0.18f);
        pr.sizeDelta = Vector2.zero;

        BuildButton(box, "CreateMix", "CREATE MIX",
            new Vector2(0.59f, 0.04f), new Vector2(0.77f, 0.18f),
            ColorAccent,
            Color.white,
            () =>
            {
                ShowView(View.MixBuilder);
            });

        BuildButton(box, "StartBase", "START BASE",
            new Vector2(0.79f, 0.04f), new Vector2(0.97f, 0.18f),
            enabled ? ColorGold : ColorRow,
            enabled ? ColorDark : ColorMuted,
            () =>
            {
                if (!enabled) return;
                EmployeeSystem.RequestBudtenderOrder(_detailLocationId, selectedBase.ProductId);
                RefreshManagePanel();
            });
    }

    private void BuildBaseButton(GameObject parent, int index, Vector2 min, Vector2 max, bool enabled)
    {
        var product = EmployeeProduction.BudtenderProducts[index];
        bool selected = index == _strainBaseIndex;
        string label = product.ProductId == "granddaddypurple" ? "GRANDDADDY" : product.DisplayName.ToUpper();
        BuildButton(parent, "Base_" + product.ProductId, label,
            min, max,
            selected ? ColorGold : ColorRow,
            enabled ? (selected ? ColorDark : ColorGold) : ColorMuted,
            () =>
            {
                if (!enabled) return;
                _strainBaseIndex = index;
                if (_currentView == View.MixBuilder) RefreshMixBuilderPanel();
                else RefreshManagePanel();
            });
    }

    private void RefreshMixBuilderPanel()
    {
        if (_mixBuilderPanel == null) return;
        for (int i = _mixBuilderPanel.transform.childCount - 1; i >= 0; i--)
            GameObject.Destroy(_mixBuilderPanel.transform.GetChild(i).gameObject);

        bool hasBudtender = EmployeeSystem.HasRole(_detailLocationId, EmployeeRole.Budtender);
        bool enabled = hasBudtender && EmployeeSystem.GetBudtenderOrder(_detailLocationId) == null;
        int maxIngredients = StrainMixingSystem.MaxIngredientSlots;
        if (_strainBaseIndex < 0 || _strainBaseIndex >= EmployeeProduction.BudtenderProducts.Length)
            _strainBaseIndex = 0;
        while (_strainIngredientIds.Count > maxIngredients)
            _strainIngredientIds.RemoveAt(_strainIngredientIds.Count - 1);

        var box = BuildSection(_mixBuilderPanel, "CREATE MIX",
            new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.95f),
            "");

        var selectedBase = EmployeeProduction.BudtenderProducts[_strainBaseIndex];
        var recipeObj = MakeText(box, "Recipe", StrainMixingSystem.BuildRecipeName(selectedBase.ProductId, _strainIngredientIds));
        var recipe = recipeObj.GetComponent<Text>();
        recipe.fontSize = 12;
        recipe.fontStyle = FontStyle.Bold;
        recipe.color = ColorGold;
        recipe.alignment = TextAnchor.MiddleLeft;
        recipe.horizontalOverflow = HorizontalWrapMode.Wrap;
        var rr = recipeObj.GetComponent<RectTransform>();
        rr.anchorMin = new Vector2(0.03f, 0.76f);
        rr.anchorMax = new Vector2(0.70f, 0.85f);
        rr.sizeDelta = Vector2.zero;

        for (int i = 0; i < EmployeeProduction.BudtenderProducts.Length; i++)
        {
            float x0 = 0.03f + i * 0.235f;
            BuildBaseButton(box, i, new Vector2(x0, 0.62f), new Vector2(x0 + 0.215f, 0.73f), enabled);
        }

        for (int i = 0; i < maxIngredients; i++)
        {
            int row = i / 4;
            int col = i % 4;
            float x0 = 0.03f + col * 0.235f;
            float y0 = row == 0 ? 0.47f : 0.33f;
            BuildSelectedIngredientSlot(box, i, new Vector2(x0, y0), new Vector2(x0 + 0.215f, y0 + 0.14f), enabled);
        }

        BuildIngredientScrollGrid(box, StrainMixingSystem.GetIngredients(), enabled, maxIngredients);

        BuildButton(box, "StartMix", "START MIX",
            new Vector2(0.74f, 0.86f), new Vector2(0.97f, 0.97f),
            enabled && _strainIngredientIds.Count > 0 ? ColorGold : ColorDark,
            enabled && _strainIngredientIds.Count > 0 ? ColorDark : ColorMuted,
            () =>
            {
                if (!enabled || _strainIngredientIds.Count == 0) return;
                EmployeeSystem.RequestBudtenderOrder(_detailLocationId, selectedBase.ProductId, _strainIngredientIds);
                _strainIngredientIds.Clear();
                ShowView(View.Manage);
            });
    }

    private void BuildSelectedIngredientSlot(GameObject parent, int index, Vector2 min, Vector2 max, bool enabled)
    {
        var slot = new GameObject("MixSlot_" + index);
        slot.transform.SetParent(parent.transform, false);
        var sr = slot.AddComponent<RectTransform>();
        sr.anchorMin = min;
        sr.anchorMax = max;
        sr.sizeDelta = Vector2.zero;
        slot.AddComponent<Image>().color = index < _strainIngredientIds.Count ? ColorRowSel : ColorHeader;

        string ingredientId = index < _strainIngredientIds.Count ? _strainIngredientIds[index] : "";
        BudtenderIngredient ingredient = null;
        if (!string.IsNullOrEmpty(ingredientId))
            StrainMixingSystem.TryGetIngredient(ingredientId, out ingredient);

        if (ingredient != null)
        {
            BuildIngredientIcon(slot, ingredient, new Vector2(0.04f, 0.18f), new Vector2(0.38f, 0.90f));
            var labelObj = MakeText(slot, "Label", $"{index + 1}. {ingredient.DisplayName}");
            var label = labelObj.GetComponent<Text>();
            label.fontSize = 10;
            label.fontStyle = FontStyle.Bold;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            var lr = labelObj.GetComponent<RectTransform>();
            lr.anchorMin = new Vector2(0.42f, 0.14f);
            lr.anchorMax = new Vector2(0.94f, 0.90f);
            lr.sizeDelta = Vector2.zero;

            BuildButton(slot, "Remove", "X",
                new Vector2(0.80f, 0.68f), new Vector2(0.98f, 0.98f),
                ColorDark, ColorGold,
                () =>
                {
                    if (!enabled) return;
                    StrainMixingSystem.TryRemoveIngredientAt(_strainIngredientIds, index);
                    RefreshMixBuilderPanel();
                });
        }
        else
        {
            var emptyObj = MakeText(slot, "Empty", $"{index + 1}");
            var empty = emptyObj.GetComponent<Text>();
            empty.fontSize = 12;
            empty.color = ColorMuted;
            empty.alignment = TextAnchor.MiddleCenter;
            var er = emptyObj.GetComponent<RectTransform>();
            er.anchorMin = Vector2.zero;
            er.anchorMax = Vector2.one;
            er.sizeDelta = Vector2.zero;
        }
    }

    private void BuildIngredientTile(GameObject parent, BudtenderIngredient ingredient, Vector2 min, Vector2 max, bool enabled, int maxIngredients)
    {
        bool canAdd = enabled && _strainIngredientIds.Count < maxIngredients;
        var tile = new GameObject("Ingredient_" + ingredient.IngredientId);
        tile.transform.SetParent(parent.transform, false);
        var tr = tile.AddComponent<RectTransform>();
        tr.anchorMin = min;
        tr.anchorMax = max;
        tr.sizeDelta = Vector2.zero;
        tile.AddComponent<Image>().color = canAdd ? ColorRow : ColorDark;
        tile.AddComponent<Button>().onClick.AddListener(new Action(() =>
        {
            if (StrainMixingSystem.TryAddIngredient(_strainIngredientIds, ingredient.IngredientId, maxIngredients))
                RefreshMixBuilderPanel();
        }));

        BuildIngredientIcon(tile, ingredient, new Vector2(0.04f, 0.18f), new Vector2(0.34f, 0.90f));
        var labelObj = MakeText(tile, "Label", ingredient.DisplayName.ToUpper());
        var label = labelObj.GetComponent<Text>();
        label.fontSize = 10;
        label.fontStyle = FontStyle.Bold;
        label.color = canAdd ? Color.white : ColorMuted;
        label.alignment = TextAnchor.MiddleLeft;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        var lr = labelObj.GetComponent<RectTransform>();
        lr.anchorMin = new Vector2(0.38f, 0.16f);
        lr.anchorMax = new Vector2(0.96f, 0.90f);
        lr.sizeDelta = Vector2.zero;
    }

    private void BuildIngredientScrollGrid(GameObject parent, IReadOnlyList<BudtenderIngredient> ingredients, bool enabled, int maxIngredients)
    {
        var scrollGo = new GameObject("IngredientScroll");
        scrollGo.transform.SetParent(parent.transform, false);
        var sr = scrollGo.AddComponent<RectTransform>();
        sr.anchorMin = new Vector2(0.03f, 0.05f);
        sr.anchorMax = new Vector2(0.97f, 0.32f);
        sr.sizeDelta = Vector2.zero;

        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;

        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollGo.transform, false);
        var vr = viewport.AddComponent<RectTransform>();
        vr.anchorMin = Vector2.zero;
        vr.anchorMax = Vector2.one;
        vr.sizeDelta = Vector2.zero;
        viewport.AddComponent<RectMask2D>();
        scroll.viewport = vr;

        var content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        var cr = content.AddComponent<RectTransform>();
        cr.anchorMin = new Vector2(0f, 1f);
        cr.anchorMax = new Vector2(1f, 1f);
        cr.pivot = new Vector2(0.5f, 1f);
        cr.sizeDelta = Vector2.zero;

        var grid = content.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(132f, 56f);
        grid.spacing = new Vector2(6f, 6f);
        grid.padding = new RectOffset(0, 0, 0, 4);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;

        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = cr;

        if (ingredients == null || ingredients.Count == 0)
            return;

        for (int i = 0; i < ingredients.Count; i++)
            BuildIngredientGridTile(content, ingredients[i], enabled, maxIngredients);
    }

    private void BuildIngredientGridTile(GameObject parent, BudtenderIngredient ingredient, bool enabled, int maxIngredients)
    {
        if (ingredient == null) return;
        bool canAdd = enabled && _strainIngredientIds.Count < maxIngredients;
        var tile = new GameObject("Ingredient_" + ingredient.IngredientId);
        tile.transform.SetParent(parent.transform, false);
        tile.AddComponent<RectTransform>();
        tile.AddComponent<Image>().color = canAdd ? ColorRow : ColorDark;
        tile.AddComponent<Button>().onClick.AddListener(new Action(() =>
        {
            if (StrainMixingSystem.TryAddIngredient(_strainIngredientIds, ingredient.IngredientId, maxIngredients))
                RefreshMixBuilderPanel();
        }));

        BuildIngredientIcon(tile, ingredient, new Vector2(0.04f, 0.18f), new Vector2(0.34f, 0.90f));
        var labelObj = MakeText(tile, "Label", ingredient.DisplayName.ToUpper());
        var label = labelObj.GetComponent<Text>();
        label.fontSize = 9;
        label.fontStyle = FontStyle.Bold;
        label.color = canAdd ? Color.white : ColorMuted;
        label.alignment = TextAnchor.MiddleLeft;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        var lr = labelObj.GetComponent<RectTransform>();
        lr.anchorMin = new Vector2(0.38f, 0.12f);
        lr.anchorMax = new Vector2(0.96f, 0.90f);
        lr.sizeDelta = Vector2.zero;
    }

    private void BuildIngredientIcon(GameObject parent, BudtenderIngredient ingredient, Vector2 min, Vector2 max)
    {
        var icon = new GameObject("Icon");
        icon.transform.SetParent(parent.transform, false);
        var ir = icon.AddComponent<RectTransform>();
        ir.anchorMin = min;
        ir.anchorMax = max;
        ir.sizeDelta = Vector2.zero;
        var image = icon.AddComponent<Image>();
        image.color = new Color(0.18f, 0.18f, 0.18f, 1f);

        var sprite = TryGetIngredientSprite(ingredient);
        if (sprite != null)
        {
            image.sprite = sprite;
            image.preserveAspect = true;
            image.color = Color.white;
            return;
        }

        var initialObj = MakeText(icon, "Initial", string.IsNullOrEmpty(ingredient.DisplayName) ? "?" : ingredient.DisplayName.Substring(0, 1).ToUpper());
        var initial = initialObj.GetComponent<Text>();
        initial.fontSize = 18;
        initial.fontStyle = FontStyle.Bold;
        initial.color = ColorGold;
        initial.alignment = TextAnchor.MiddleCenter;
        var r = initialObj.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.sizeDelta = Vector2.zero;
    }

    private static Sprite TryGetIngredientSprite(BudtenderIngredient ingredient)
    {
        if (ingredient == null) return null;
        foreach (var candidate in ingredient.CandidateIds)
        {
            try
            {
                var def = Registry.GetItem<ItemDefinition>(candidate);
                if (def?.Icon != null) return def.Icon;
            }
            catch
            {
            }
        }
        return null;
    }

    private void BuildGrowSection()
    {
        BuildSection(_managePanel, "GROW",
            new Vector2(0.04f, 0.22f), new Vector2(0.96f, 0.28f),
            EmployeeSystem.GetBudtenderGrowStatus(_detailLocationId));
    }

    private void BuildEmployeeSection()
    {
        var box = BuildSection(_managePanel, "EMPLOYEES",
            new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.20f),
            "");

        var employees = EmployeeSystem.GetEmployees(_detailLocationId);
        string roster = employees.Count == 0 ? "(no employees hired)" : "";
        for (int i = 0; i < employees.Count; i++)
        {
            if (i > 0) roster += "\n";
            roster += $"{employees[i].Role}: {employees[i].DisplayName}";
        }

        var rosterObj = MakeText(box, "Roster", roster);
        var rosterText = rosterObj.GetComponent<Text>();
        rosterText.fontSize = 12;
        rosterText.color = ColorMuted;
        rosterText.alignment = TextAnchor.UpperLeft;
        rosterText.horizontalOverflow = HorizontalWrapMode.Wrap;
        rosterText.verticalOverflow = VerticalWrapMode.Truncate;
        var rr = rosterObj.GetComponent<RectTransform>();
        rr.anchorMin = new Vector2(0.03f, 0.34f);
        rr.anchorMax = new Vector2(0.58f, 0.82f);
        rr.sizeDelta = Vector2.zero;

        BuildEmployeeRoleButton(box, EmployeeRole.Cashier, "HIRE CASHIER", "FIRE CASHIER", 0.62f);
        BuildEmployeeRoleButton(box, EmployeeRole.Budtender, "HIRE BUDTENDER", "FIRE BUDTENDER", 0.38f);

        BuildButton(box, "MoveObjects", "MOVE OBJECTS",
            new Vector2(0.03f, 0.08f), new Vector2(0.34f, 0.27f),
            MogulPlacementSystem.IsActive ? ColorAccent : ColorRow,
            MogulPlacementSystem.IsActive ? Color.white : ColorGold,
            () =>
            {
                MogulPlacementSystem.Begin(_detailLocationId);
                RefreshManagePanel();
            });

        BuildButton(box, "ResetObjects", "RESET POS",
            new Vector2(0.36f, 0.08f), new Vector2(0.58f, 0.27f),
            ColorRow, ColorGold,
            () =>
            {
                MogulNetwork.RequestAction(MogulActions.ClearObjectPlacements, _detailLocationId);
                LocationSpawner.RebuildBuilding(_detailLocationId);
                RefreshManagePanel();
            });
    }

    private void BuildEmployeeRoleButton(GameObject parent, EmployeeRole role, string hireLabel, string fireLabel, float yMin)
    {
        bool hired = EmployeeSystem.HasRole(_detailLocationId, role);
        BuildButton(parent, "Employee_" + role, hired ? fireLabel : hireLabel,
            new Vector2(0.62f, yMin), new Vector2(0.96f, yMin + 0.16f),
            hired ? ColorRowSel : ColorGold,
            hired ? ColorGold : ColorDark,
            () =>
            {
                if (EmployeeSystem.HasRole(_detailLocationId, role))
                    EmployeeSystem.RequestFire(_detailLocationId, role);
                else
                    EmployeeSystem.RequestHire(_detailLocationId, role);
                RefreshManagePanel();
            });
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
