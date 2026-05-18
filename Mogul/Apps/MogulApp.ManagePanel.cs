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
    private void BuildManagePanel(GameObject container)
    {
        _managePanel = new GameObject("ManagePanel");
        _managePanel.transform.SetParent(container.transform, false);
        var r = _managePanel.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(0f, 0f);
        r.anchorMax = new Vector2(1f, 0.9f);
        r.sizeDelta = Vector2.zero;
        _managePanel.AddComponent<Image>().color = new Color(ColorGreen.r * 0.18f, ColorGreen.g * 0.18f, ColorGreen.b * 0.18f, 1f);
        var tex = LoadModTexture("propertyLandscape.png");
        if (tex != null)
        {
            var bgImg = new GameObject("BgImage");
            bgImg.transform.SetParent(_managePanel.transform, false);
            var ri = bgImg.AddComponent<RawImage>();
            ri.texture = tex;
            ri.color = new Color(1f, 1f, 1f, 0.25f);
            var bir = EnsureRect(bgImg);
            bir.anchorMin = Vector2.zero;
            bir.anchorMax = Vector2.one;
            bir.sizeDelta = Vector2.zero;
        }
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

    private void RefreshManagePanel()
    {
        if (_managePanel == null) return;
        for (int i = _managePanel.transform.childCount - 1; i >= 0; i--)
        {
            var child = _managePanel.transform.GetChild(i).gameObject;
            if (child.name == "BgImage") continue;
            GameObject.Destroy(child);
        }

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
            manual >= 0f ? ColorGreen : ColorRowOwned,
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
            ColorGreen,
            ColorDark,
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
            MogulPlacementSystem.IsActive ? ColorGreen : ColorRow,
            MogulPlacementSystem.IsActive ? ColorDark : ColorGold,
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
}
