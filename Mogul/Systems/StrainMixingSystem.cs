using System;
using System.Collections.Generic;
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Product;
using MelonLoader;
using Mogul.Data;

namespace Mogul.Systems;

public class BudtenderIngredient
{
    public string IngredientId { get; }
    public string DisplayName { get; }
    public string EffectHint { get; }
    public IReadOnlyList<string> CandidateIds { get; }

    public BudtenderIngredient(string ingredientId, string displayName, string effectHint, params string[] candidateIds)
    {
        IngredientId = ingredientId;
        DisplayName = displayName;
        EffectHint = effectHint;

        var candidates = new List<string> { ingredientId };
        if (candidateIds != null)
        {
            foreach (var candidate in candidateIds)
            {
                if (!string.IsNullOrWhiteSpace(candidate) && !candidates.Contains(candidate))
                    candidates.Add(candidate);
            }
        }
        CandidateIds = candidates;
    }
}

public class BudtenderOrderRequest
{
    public string LocationId { get; set; } = "";
    public string BaseProductId { get; set; } = "";
    public List<string> IngredientIds { get; set; } = new();
}

public static class StrainMixingSystem
{
    public const int MaxIngredientSlots = 4;

    public static readonly BudtenderIngredient[] Ingredients =
    {
        new("cuke", "Cuke", "Refreshing", "Cuke"),
        new("energydrink", "Energy", "Energizing", "energy_drink", "energyDrink", "EnergyDrink"),
        new("gasoline", "Gasoline", "Toxic", "Gasoline"),
        new("horsesemen", "Horse", "Athletic", "horse_semen", "horseSemen", "HorseSemen"),
        new("banana", "Banana", "Calorie", "Banana"),
        new("chili", "Chili", "Spicy", "Chilli", "hot_sauce"),
        new("viagra", "Viagra", "Tropic", "Viagra"),
        new("addy", "Addy", "Focused", "Addy"),
    };

    public static int GetUnlockedIngredientSlots(int reach)
    {
        if (reach >= 20000) return 4;
        if (reach >= 10000) return 3;
        if (reach >= 3500) return 2;
        if (reach >= 750) return 1;
        return 0;
    }

    public static string BuildOrderPayload(string locationId, string baseProductId, IReadOnlyList<string> ingredientIds)
    {
        string ingredients = ingredientIds == null || ingredientIds.Count == 0
            ? ""
            : string.Join(",", ingredientIds);
        return $"{locationId}|{baseProductId}|{ingredients}";
    }

    public static bool TryParseOrderPayload(string payload, out BudtenderOrderRequest request)
    {
        request = null;
        if (string.IsNullOrWhiteSpace(payload)) return false;

        var parts = payload.Split('|');
        if (parts.Length >= 2)
        {
            request = new BudtenderOrderRequest
            {
                LocationId = parts[0],
                BaseProductId = parts[1],
            };
            if (parts.Length >= 3 && !string.IsNullOrWhiteSpace(parts[2]))
                request.IngredientIds.AddRange(parts[2].Split(',', StringSplitOptions.RemoveEmptyEntries));
            return !string.IsNullOrWhiteSpace(request.LocationId) && !string.IsNullOrWhiteSpace(request.BaseProductId);
        }

        int sep = payload.IndexOf(':');
        if (sep <= 0) return false;
        request = new BudtenderOrderRequest
        {
            LocationId = payload.Substring(0, sep),
            BaseProductId = payload.Substring(sep + 1),
        };
        return !string.IsNullOrWhiteSpace(request.LocationId) && !string.IsNullOrWhiteSpace(request.BaseProductId);
    }

    public static bool TryGetIngredient(string ingredientId, out BudtenderIngredient ingredient)
    {
        ingredient = null;
        if (string.IsNullOrWhiteSpace(ingredientId)) return false;
        foreach (var candidate in Ingredients)
        {
            if (!string.Equals(candidate.IngredientId, ingredientId, StringComparison.OrdinalIgnoreCase)) continue;
            ingredient = candidate;
            return true;
        }
        return false;
    }

    public static string BuildRecipeName(string baseProductId, IReadOnlyList<string> ingredientIds)
    {
        var name = GetProductDisplayName(baseProductId);
        if (ingredientIds == null || ingredientIds.Count == 0)
            return name;

        var parts = new List<string>();
        foreach (var id in ingredientIds)
            parts.Add(TryGetIngredient(id, out var ingredient) ? ingredient.DisplayName : id);
        return name + " + " + string.Join(" + ", parts);
    }

    public static string BuildOrderDisplayName(BudtenderOrderData order)
    {
        if (order == null) return "";
        if (!string.IsNullOrWhiteSpace(order.DisplayName)) return order.DisplayName;
        if (!string.IsNullOrWhiteSpace(order.BaseProductId))
            return BuildRecipeName(order.BaseProductId, order.IngredientIds);
        return GetProductDisplayName(order.ProductId);
    }

    public static string GetProductDisplayName(string productId)
    {
        if (EmployeeProduction.TryGetBudtenderProduct(productId, out var product))
            return product.DisplayName;

        try
        {
            var def = Registry.GetItem<ProductDefinition>(productId);
            if (def != null && !string.IsNullOrWhiteSpace(def.Name))
                return def.Name;
        }
        catch
        {
        }

        return string.IsNullOrWhiteSpace(productId) ? "(none)" : productId;
    }

    public static bool TryResolveRecipeProduct(string baseProductId, IReadOnlyList<string> ingredientIds, out string productId, out string displayName, out string error)
    {
        productId = baseProductId;
        displayName = BuildRecipeName(baseProductId, ingredientIds);
        error = null;

        if (string.IsNullOrWhiteSpace(baseProductId))
        {
            error = "missing base product";
            return false;
        }

        if (ingredientIds == null || ingredientIds.Count == 0)
            return true;

        try
        {
            var manager = NetworkSingleton<ProductManager>.Instance;
            if (manager == null)
            {
                error = "product manager unavailable";
                return false;
            }

            string current = baseProductId;
            for (int i = 0; i < ingredientIds.Count; i++)
            {
                string ingredientId = ResolveIngredientId(ingredientIds[i]);
                string stepName = BuildStepName(current, ingredientId, i);
                string output = manager.FinishAndNameMix(current, ingredientId, stepName);
                if (string.IsNullOrWhiteSpace(output))
                {
                    error = $"mix returned no product for {current} + {ingredientId}";
                    return false;
                }

                current = output;
            }

            productId = current;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            MelonLogger.Warning("[Mogul] Strain mix resolution failed: " + ex.Message);
            return false;
        }
    }

    private static string BuildStepName(string productId, string ingredientId, int stepIndex)
    {
        string productName = GetProductDisplayName(productId);
        string ingredientName = TryGetIngredient(ingredientId, out var ingredient) ? ingredient.DisplayName : ingredientId;
        string name = $"{productName} {ingredientName}";
        return name.Length <= 24 ? name : $"{productName} Mix {stepIndex + 1}";
    }

    private static string ResolveIngredientId(string ingredientId)
    {
        if (!TryGetIngredient(ingredientId, out var ingredient))
            return ingredientId;

        foreach (var candidate in ingredient.CandidateIds)
        {
            try
            {
                var def = Registry.GetItem<ItemDefinition>(candidate);
                if (def != null) return def.ID;
            }
            catch
            {
            }
        }

        return ingredient.IngredientId;
    }
}

