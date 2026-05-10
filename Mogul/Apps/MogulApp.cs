using System;
using Mogul.Data;
using Mogul.Systems;
using S1API.PhoneApp;
using UnityEngine;
using UnityEngine.UI;

namespace Mogul.Apps;

public class MogulApp : PhoneApp
{
    protected override string AppName => "MogulApp";
    protected override string AppTitle => "Mogul";
    protected override string IconLabel => "MGR";
    protected override string IconFileName => "imetto-Mogul/mogul_icon.png";
    protected override EOrientation Orientation => EOrientation.Horizontal;

    private static readonly Color ColorBg       = new Color(0.04f, 0.04f, 0.04f, 1f);
    private static readonly Color ColorHeader   = new Color(0.07f, 0.07f, 0.07f, 1f);
    private static readonly Color ColorRow      = new Color(0.085f, 0.085f, 0.085f, 1f);
    private static readonly Color ColorRowSel   = new Color(0.14f, 0.11f, 0.04f, 1f);
    private static readonly Color ColorRowOwned = new Color(0.06f, 0.075f, 0.06f, 1f);
    private static readonly Color ColorGold     = new Color(0.82f, 0.67f, 0.16f, 1f);
    private static readonly Color ColorMuted    = new Color(0.45f, 0.45f, 0.45f, 1f);
    private static readonly Color ColorDark     = new Color(0.05f, 0.05f, 0.05f, 1f);
    private static readonly Color ColorAccent   = new Color(0.30f, 0.55f, 0.85f, 1f);

    private enum View { List, Manage, Customize }

    private Font _font;
    private Text _titleText;
    private Text _reachText;
    private GameObject _backButton;

    private GameObject _listPanel;
    private GameObject _managePanel;
    private GameObject _customizePanel;
    private RectTransform _listContent;

    private View _currentView = View.List;
    private string _selectedRowId;
    private string _detailLocationId;

    protected override void OnCreatedUI(GameObject container)
    {
        try
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildBackground(container);
            BuildHeader(container);
            BuildListPanel(container);
            BuildManagePanel(container);
            BuildCustomizePanel(container);

            ShowView(View.List);
            MogulNetwork.OnDataChanged += _ =>
            {
                RefreshHeader();
                if (_currentView == View.List) RefreshList();
            };
        }
        catch (Exception ex)
        {
            MelonLoader.MelonLogger.Error("[Mogul] OnCreatedUI crashed: " + ex);
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
        _backButton.AddComponent<Button>().onClick.AddListener(new Action(() => ShowView(View.List)));
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
        pr.anchorMax = new Vector2(1f, 0.9f);
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

    private void RefreshList()
    {
        if (_listContent == null) return;
        for (int i = _listContent.childCount - 1; i >= 0; i--)
            GameObject.Destroy(_listContent.GetChild(i).gameObject);

        foreach (var location in PropertySystem.Catalog)
        {
            bool owned = PropertySystem.IsOwned(location.Id);
            bool selected = !owned && _selectedRowId == location.Id;
            BuildRow(location, owned, selected);
        }
    }

    private void BuildRow(MogulLocation location, bool owned, bool selected)
    {
        float h = selected ? 110f : 56f;

        var row = new GameObject("Row_" + location.Id);
        row.transform.SetParent(_listContent, false);
        var rowImg = row.AddComponent<Image>();
        rowImg.color = owned ? ColorRowOwned : (selected ? ColorRowSel : ColorRow);
        var le = row.AddComponent<LayoutElement>();
        le.preferredHeight = h;
        le.minHeight = h;

        var accent = new GameObject("Accent");
        accent.transform.SetParent(row.transform, false);
        accent.AddComponent<Image>().color = owned ? ColorMuted : ColorGold;
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
            var priceObj = MakeText(row, "Price", "$" + location.Price.ToString("N0"));
            var priceText = priceObj.GetComponent<Text>();
            priceText.fontSize = 13;
            priceText.color = ColorGold;
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

            var sizeObj = MakeText(row, "Size",
                $"{location.RoomSize.x:F0} × {location.RoomSize.z:F0} m  ·  Door: {location.Door}");
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

    private void BuildButton(GameObject parent, string name, string label,
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
        btn.AddComponent<Button>().onClick.AddListener(onClick);

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

        BuildSection(_managePanel, "INVENTORY",
            new Vector2(0.04f, 0.55f), new Vector2(0.96f, 0.95f),
            "(no items yet)");
        BuildSection(_managePanel, "EMPLOYEES",
            new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.5f),
            "(no employees hired)");
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

    private void BuildSection(GameObject parent, string title,
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
    }

    private void OpenSubview(View view, string locationId)
    {
        _detailLocationId = locationId;
        ShowView(view);
    }

    private void ShowView(View view)
    {
        _currentView = view;
        if (_listPanel != null)      _listPanel.SetActive(view == View.List);
        if (_managePanel != null)    _managePanel.SetActive(view == View.Manage);
        if (_customizePanel != null) _customizePanel.SetActive(view == View.Customize);
        if (_backButton != null)     _backButton.SetActive(view != View.List);

        var loc = string.IsNullOrEmpty(_detailLocationId) ? null : PropertySystem.Find(_detailLocationId);
        switch (view)
        {
            case View.List:
                _titleText.text = "MOGUL";
                _selectedRowId = null;
                RefreshList();
                break;
            case View.Manage:
                _titleText.text = (loc != null ? loc.Name.ToUpper() : "") + "  ·  MANAGE";
                break;
            case View.Customize:
                _titleText.text = (loc != null ? loc.Name.ToUpper() : "") + "  ·  CUSTOMIZE";
                break;
        }
    }

    private GameObject MakeText(GameObject parent, string name, string text)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent.transform, false);
        var t = obj.AddComponent<Text>();
        t.text = text;
        t.font = _font;
        t.supportRichText = false;
        obj.AddComponent<RectTransform>();
        return obj;
    }
}
