using S1API.PhoneApp;
using UnityEngine;
using UnityEngine.UI;
using Mogul.Systems;
using System;
using Mogul.Data;
using System.Collections.Generic;

namespace Mogul.Apps;

public class MogulApp : PhoneApp
{
    protected override string AppName => "MogulApp";
    protected override string AppTitle => "Mogul";
    protected override string IconLabel => "MGR";
    protected override string IconFileName => "imetto-Mogul/mogul_icon.png";
    protected override EOrientation Orientation => EOrientation.Horizontal;

    private static readonly Color ColorBg = new Color(0.04f, 0.04f, 0.04f, 1f);
    private static readonly Color ColorHeader = new Color(0.07f, 0.07f, 0.07f, 1f);
    private static readonly Color ColorRowEven = new Color(0.08f, 0.08f, 0.08f, 1f);
    private static readonly Color ColorRowOdd = new Color(0.06f, 0.06f, 0.06f, 1f);
    private static readonly Color ColorGold = new Color(0.82f, 0.67f, 0.16f, 1f);
    private static readonly Color ColorOwned = new Color(0.13f, 0.13f, 0.13f, 1f);
    private static readonly Color ColorMuted = new Color(0.38f, 0.38f, 0.38f, 1f);
    private static readonly Color ColorDark  = new Color(0.05f, 0.05f, 0.05f, 1f);

    private Font _font;
    private GameObject _propertiesPanel;
    private GameObject _pickerPanel;
    private string _pendingLocationId;
    private string _selectedDesignId;

    protected override void OnCreatedUI(GameObject container)
    {
        try
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            BuildBackground(container);
            BuildHeader(container);

            _propertiesPanel = new GameObject("PropertiesPanel");
            _propertiesPanel.transform.SetParent(container.transform, false);

            _pickerPanel = new GameObject("PickerPanel");
            _pickerPanel.transform.SetParent(container.transform, false);
            _pickerPanel.SetActive(false);
            BuildPickerPanel();
            BuildPropertiesPanel();
            MogulNetwork.OnDataChanged += _ => RefreshPropertiesPanel();
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
        hr.anchorMin = new Vector2(0f, 0.88f);
        hr.anchorMax = new Vector2(1f, 1f);
        hr.sizeDelta = Vector2.zero;

        var title = MakeText(header, "Title", "MOGUL");
        var titleText = title.GetComponent<Text>();
        titleText.fontSize = 26;
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = ColorGold;
        titleText.alignment = TextAnchor.MiddleLeft;
        var tr = title.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0.03f, 0f);
        tr.anchorMax = new Vector2(0.5f, 1f);
        tr.sizeDelta = Vector2.zero;

        var reach = MakeText(header, "Reach", "REACH  " + MogulNetwork.Data.Reach);
        var reachText = reach.GetComponent<Text>();
        reachText.fontSize = 15;
        reachText.color = ColorMuted;
        reachText.alignment = TextAnchor.MiddleRight;
        var rr = reach.GetComponent<RectTransform>();
        rr.anchorMin = new Vector2(0.5f, 0f);
        rr.anchorMax = new Vector2(0.97f, 1f);
        rr.sizeDelta = Vector2.zero;
    }

    private void BuildPropertiesPanel()
    {
        var r = _propertiesPanel.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(0f, 0f);
        r.anchorMax = new Vector2(1f, 0.88f);
        r.sizeDelta = Vector2.zero;

        RefreshPropertiesPanel();
    }
    private void BuildPickerPanel()
    {
        var r = _pickerPanel.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(0f, 0f);
        r.anchorMax = new Vector2(1f, 0.88f);
        r.sizeDelta = Vector2.zero;

        var cardImages = new Dictionary<string, Image>();
        Button confirmComp = null;
        Image confirmImg  = null;
        Text  confirmText = null;

        int cardIndex = 0;
        foreach (var design in DesignCatalog.All)
        {
            float xMin = cardIndex * 0.5f;
            float xMax = xMin + 0.5f;

            var card = new GameObject("Card_" + design.Id);
            card.transform.SetParent(_pickerPanel.transform, false);
            var cardRect = card.AddComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(xMin, 0.15f); // y stops at 0.15 — buttons live below
            cardRect.anchorMax = new Vector2(xMax, 1f);
            cardRect.sizeDelta = Vector2.zero;
            var cardImg = card.AddComponent<Image>();
            cardImg.color = ColorRowEven;
            cardImages[design.Id] = cardImg;

            var nameObj = MakeText(card, "Name", design.Name.ToUpper());
            var nameText = nameObj.GetComponent<Text>();
            nameText.fontSize = 20;
            nameText.fontStyle = FontStyle.Bold;
            nameText.color = ColorGold;
            nameText.alignment = TextAnchor.MiddleCenter;
            var nameRect = nameObj.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0.08f, 0.72f);
            nameRect.anchorMax = new Vector2(0.92f, 0.9f);
            nameRect.sizeDelta = Vector2.zero;

            var descObj = MakeText(card, "Description", design.Description);
            var descText = descObj.GetComponent<Text>();
            descText.fontSize = 13;
            descText.color = Color.white;
            descText.alignment = TextAnchor.UpperCenter;
            var descRect = descObj.GetComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0.1f, 0.35f);
            descRect.anchorMax = new Vector2(0.9f, 0.7f);
            descRect.sizeDelta = Vector2.zero;

            var selectBtn = new GameObject("SelectButton");
            selectBtn.transform.SetParent(card.transform, false);
            var selectRect = selectBtn.AddComponent<RectTransform>();
            selectRect.anchorMin = new Vector2(0.25f, 0.08f);
            selectRect.anchorMax = new Vector2(0.75f, 0.28f);
            selectRect.sizeDelta = Vector2.zero;
            selectBtn.AddComponent<Image>().color = ColorGold;
            selectBtn.AddComponent<Button>().onClick.AddListener(new Action(() =>
            {
                _selectedDesignId = design.Id;
                foreach (var kv in cardImages)
                    kv.Value.color = kv.Key == design.Id ? ColorOwned : ColorRowEven;
                if (confirmComp != null) confirmComp.interactable = true;
                if (confirmImg  != null) confirmImg.color  = ColorGold;
                if (confirmText != null) confirmText.color = ColorDark;
            }));
            var selectLabelObj = MakeText(selectBtn, "Label", "SELECT");
            var selectLabel = selectLabelObj.GetComponent<Text>();
            selectLabel.fontSize = 15;
            selectLabel.fontStyle = FontStyle.Bold;
            selectLabel.color = ColorDark;
            selectLabel.alignment = TextAnchor.MiddleCenter;
            var selectLabelRect = selectLabelObj.GetComponent<RectTransform>();
            selectLabelRect.anchorMin = Vector2.zero;
            selectLabelRect.anchorMax = Vector2.one;
            selectLabelRect.sizeDelta = Vector2.zero;

            cardIndex++;
        }

        // Cancel
        var cancelBtn = new GameObject("CancelButton");
        cancelBtn.transform.SetParent(_pickerPanel.transform, false);
        var cancelRect = cancelBtn.AddComponent<RectTransform>();
        cancelRect.anchorMin = new Vector2(0.02f, 0.02f);
        cancelRect.anchorMax = new Vector2(0.48f, 0.12f);
        cancelRect.sizeDelta = Vector2.zero;
        cancelBtn.AddComponent<Image>().color = ColorMuted;
        cancelBtn.AddComponent<Button>().onClick.AddListener(new Action(HideDesignPicker));
        var cancelLabelObj = MakeText(cancelBtn, "Label", "CANCEL");
        var cancelLabel = cancelLabelObj.GetComponent<Text>();
        cancelLabel.fontSize = 15;
        cancelLabel.fontStyle = FontStyle.Bold;
        cancelLabel.color = Color.white;
        cancelLabel.alignment = TextAnchor.MiddleCenter;
        var cancelLabelRect = cancelLabelObj.GetComponent<RectTransform>();
        cancelLabelRect.anchorMin = Vector2.zero;
        cancelLabelRect.anchorMax = Vector2.one;
        cancelLabelRect.sizeDelta = Vector2.zero;

        // Confirm — starts muted/disabled; SELECT listener enables it
        var confirmBtn = new GameObject("ConfirmButton");
        confirmBtn.transform.SetParent(_pickerPanel.transform, false);
        var confirmRect = confirmBtn.AddComponent<RectTransform>();
        confirmRect.anchorMin = new Vector2(0.52f, 0.02f);
        confirmRect.anchorMax = new Vector2(0.98f, 0.12f);
        confirmRect.sizeDelta = Vector2.zero;
        confirmImg = confirmBtn.AddComponent<Image>();
        confirmImg.color = ColorMuted;
        confirmComp = confirmBtn.AddComponent<Button>();
        confirmComp.interactable = false;
        confirmComp.onClick.AddListener(new Action(() =>
        {
            if (string.IsNullOrEmpty(_selectedDesignId) || string.IsNullOrEmpty(_pendingLocationId))
                return;
            string error;
            try   { error = PropertySystem.TryPurchaseWithDesign(_pendingLocationId, _selectedDesignId); }
            catch (Exception ex) { MelonLoader.MelonLogger.Error("[Mogul] TryPurchaseWithDesign threw: " + ex.Message); return; }
            if (error != null) { MelonLoader.MelonLogger.Warning("[Mogul] Purchase failed: " + error); return; }
            HideDesignPicker();
        }));
        var confirmLabelObj = MakeText(confirmBtn, "Label", "CONFIRM");
        confirmText = confirmLabelObj.GetComponent<Text>();
        confirmText.fontSize = 15;
        confirmText.fontStyle = FontStyle.Bold;
        confirmText.color = ColorMuted; // matches disabled state; SELECT listener sets it to ColorDark
        confirmText.alignment = TextAnchor.MiddleCenter;
        var confirmLabelRect = confirmLabelObj.GetComponent<RectTransform>();
        confirmLabelRect.anchorMin = Vector2.zero;
        confirmLabelRect.anchorMax = Vector2.one;
        confirmLabelRect.sizeDelta = Vector2.zero;
    }
    private void ShowDesignPicker(string locationId)
    {
        _pendingLocationId = locationId;
        _selectedDesignId = null;
        _pickerPanel.SetActive(true);
        _propertiesPanel.SetActive(false);
    }
    private void HideDesignPicker()
    {
        _pendingLocationId = null;
        _selectedDesignId = null;
        _pickerPanel.SetActive(false);
        _propertiesPanel.SetActive(true);
    }

    private void RefreshPropertiesPanel()
    {
        for (int i = _propertiesPanel.transform.childCount - 1; i >= 0; i--)
            GameObject.Destroy(_propertiesPanel.transform.GetChild(i).gameObject);

        const float rowH = 0.16f;
        int index = 0;

        foreach (var location in PropertySystem.Catalog)
        {
            bool owned = PropertySystem.IsOwned(location.Id);
            float yMin = 1f - (index + 1) * rowH;
            float yMax = 1f - index * rowH;

            var row = new GameObject("Row_" + location.Id);
            row.transform.SetParent(_propertiesPanel.transform, false);
            row.AddComponent<Image>().color = index % 2 == 0 ? ColorRowEven : ColorRowOdd;
            var rowRect = row.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, yMin);
            rowRect.anchorMax = new Vector2(1f, yMax);
            rowRect.sizeDelta = Vector2.zero;

            // Left accent bar — gold if available, muted if owned
            var accent = new GameObject("Accent");
            accent.transform.SetParent(row.transform, false);
            accent.AddComponent<Image>().color = owned ? ColorMuted : ColorGold;
            var ar = accent.GetComponent<RectTransform>();
            ar.anchorMin = new Vector2(0f, 0.1f);
            ar.anchorMax = new Vector2(0.007f, 0.9f);
            ar.sizeDelta = Vector2.zero;

            // Location name
            var nameObj = MakeText(row, "Name", location.Name.ToUpper());
            var nameText = nameObj.GetComponent<Text>();
            nameText.fontSize = 17;
            nameText.fontStyle = FontStyle.Bold;
            nameText.color = owned ? ColorMuted : Color.white;
            nameText.alignment = TextAnchor.MiddleLeft;
            var nr = nameObj.GetComponent<RectTransform>();
            nr.anchorMin = new Vector2(0.03f, 0.48f);
            nr.anchorMax = new Vector2(0.68f, 1f);
            nr.sizeDelta = Vector2.zero;

            // Price
            var priceObj = MakeText(row, "Price", "$" + location.Price.ToString("N0"));
            var priceText = priceObj.GetComponent<Text>();
            priceText.fontSize = 13;
            priceText.color = owned ? ColorMuted : ColorGold;
            priceText.alignment = TextAnchor.MiddleLeft;
            var pr = priceObj.GetComponent<RectTransform>();
            pr.anchorMin = new Vector2(0.03f, 0f);
            pr.anchorMax = new Vector2(0.68f, 0.52f);
            pr.sizeDelta = Vector2.zero;

            // Button
            var btn = new GameObject("Button");
            btn.transform.SetParent(row.transform, false);
            btn.AddComponent<Image>().color = owned ? ColorOwned : ColorGold;
            var br = btn.GetComponent<RectTransform>();
            br.anchorMin = new Vector2(0.73f, 0.15f);
            br.anchorMax = new Vector2(0.97f, 0.85f);
            br.sizeDelta = Vector2.zero;

            var btnLabel = MakeText(btn, "Label", owned ? "OWNED" : "BUY");
            var btnText = btnLabel.GetComponent<Text>();
            btnText.fontSize = 15;
            btnText.fontStyle = FontStyle.Bold;
            btnText.color = owned ? ColorMuted : ColorDark;
            btnText.alignment = TextAnchor.MiddleCenter;
            var blr = btnLabel.GetComponent<RectTransform>();
            blr.anchorMin = Vector2.zero;
            blr.anchorMax = Vector2.one;
            blr.sizeDelta = Vector2.zero;

            var btnComp = btn.AddComponent<Button>();
            if (!owned)
            {
                btnComp.onClick.AddListener(new Action(() =>
                {
                    ShowDesignPicker(location.Id);
                }));
            }
            else
            {
                btnComp.interactable = false;
            }

            index++;
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
