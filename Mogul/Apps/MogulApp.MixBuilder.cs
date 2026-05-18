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
}
