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

public partial class MogulApp
{
    private void BuildListPanel(GameObject container)
    {
        _listPanel = new GameObject("ListPanel");
        _listPanel.transform.SetParent(container.transform, false);
        var pr = _listPanel.AddComponent<RectTransform>();
        pr.anchorMin = new Vector2(0f, 0f);
        pr.anchorMax = new Vector2(1f, 0.82f);
        pr.sizeDelta = Vector2.zero;
        _listPanel.AddComponent<Image>().color = new Color(ColorGreen.r * 0.18f, ColorGreen.g * 0.18f, ColorGreen.b * 0.18f, 1f);

        var texProp = LoadModTexture("propertyLandscape.png");
        if (texProp != null)
        {
            var bgImg = new GameObject("BgImage");
            bgImg.transform.SetParent(_listPanel.transform, false);
            var ri = bgImg.AddComponent<RawImage>();
            ri.texture = texProp;
            ri.color = new Color(1f, 1f, 1f, 0.40f);
            var bir = EnsureRect(bgImg);
            bir.anchorMin = Vector2.zero;
            bir.anchorMax = Vector2.one;
            bir.sizeDelta = Vector2.zero;
        }

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
            if (!PropertySystem.IsVisible(location.Id)) continue;
            bool owned = PropertySystem.IsOwned(location.Id);
            bool selected = !owned && _selectedRowId == location.Id;
            BuildRow(location, owned, selected);
        }
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
                ColorGreen, ColorDark,
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
}
