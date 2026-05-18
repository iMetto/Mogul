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
    private void BuildOrdersPanel(GameObject container)
    {
        _ordersPanel = new GameObject("OrdersPanel");
        _ordersPanel.transform.SetParent(container.transform, false);
        var pr = _ordersPanel.AddComponent<RectTransform>();
        pr.anchorMin = new Vector2(0f, 0f);
        pr.anchorMax = new Vector2(1f, 0.82f);
        pr.sizeDelta = Vector2.zero;
        _ordersPanel.AddComponent<Image>().color = new Color(ColorAccent.r * 0.18f, ColorAccent.g * 0.18f, ColorAccent.b * 0.18f, 1f);

        var texOrders = LoadModTexture("ordersLandscape.png");
        if (texOrders != null)
        {
            var bgImg = new GameObject("BgImage");
            bgImg.transform.SetParent(_ordersPanel.transform, false);
            var ri = bgImg.AddComponent<RawImage>();
            ri.texture = texOrders;
            ri.color = new Color(1f, 1f, 1f, 0.40f);
            var bir = EnsureRect(bgImg);
            bir.anchorMin = Vector2.zero;
            bir.anchorMax = Vector2.one;
            bir.sizeDelta = Vector2.zero;
        }

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
}
