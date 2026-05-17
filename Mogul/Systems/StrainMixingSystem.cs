using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppScheduleOne;
using Il2CppScheduleOne.Core.Items.Framework;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Effects;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.UI;
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
    public const int MaxIngredientSlots = 8;

    private static List<BudtenderIngredient> _cachedIngredients;

    private static readonly BudtenderIngredient[] FallbackIngredients =
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

    public static IReadOnlyList<BudtenderIngredient> Ingredients => GetIngredients();

    public static IReadOnlyList<BudtenderIngredient> GetIngredients()
    {
        if (_cachedIngredients != null && _cachedIngredients.Count > 0)
            return _cachedIngredients;

        var result = new List<BudtenderIngredient>(FallbackIngredients);
        try
        {
            var registry = UnityEngine.Object.FindObjectOfType<Registry>();
            var items = registry?.GetAllItems();
            if (items != null)
            {
                result.Clear();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i]?.TryCast<ItemDefinition>();
                    if (!IsValidMixerDefinition(item)) continue;
                    if (!seen.Add(item.ID)) continue;
                    result.Add(new BudtenderIngredient(item.ID, string.IsNullOrWhiteSpace(item.Name) ? item.ID : item.Name, "", item.ID));
                }
            }
        }
        catch
        {
        }

        if (result.Count == 0)
            result.AddRange(FallbackIngredients);

        result.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
        _cachedIngredients = result;
        return _cachedIngredients;
    }

    private static bool IsValidMixerDefinition(ItemDefinition item)
    {
        if (item == null || item.Category != EItemCategory.Ingredient || string.IsNullOrWhiteSpace(item.ID)) return false;
        if (IsExcludedSpecialIngredient(item.ID) || IsExcludedSpecialIngredient(item.Name)) return false;

        var propertyItem = item.TryCast<PropertyItemDefinition>();
        if (propertyItem?.Properties == null || propertyItem.Properties.Count == 0) return false;

        try
        {
            var instance = item.GetDefaultInstance(1);
            return instance != null && new ItemFilter_MixingIngredient().DoesItemMatchFilter(instance);
        }
        catch
        {
            return true;
        }
    }

    private static bool IsExcludedSpecialIngredient(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        string text = value.Replace("_", "").Replace("-", "").Replace(" ", "").ToLowerInvariant();
        return text.Contains("sporesyringe")
            || text.Contains("bikecrank")
            || text.Contains("rdx")
            || text.Contains("babyblue");
    }

    public static int GetUnlockedIngredientSlots(int reach)
    {
        if (reach >= 35000) return 8;
        if (reach >= 25000) return 7;
        if (reach >= 20000) return 6;
        if (reach >= 14000) return 5;
        if (reach >= 10000) return 4;
        if (reach >= 6000) return 3;
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

    public static bool TryAddIngredient(List<string> ingredientIds, string ingredientId, int maxIngredients)
    {
        if (ingredientIds == null || string.IsNullOrWhiteSpace(ingredientId)) return false;
        if (maxIngredients <= 0 || ingredientIds.Count >= maxIngredients) return false;
        ingredientIds.Add(ingredientId);
        return true;
    }

    public static bool TryRemoveIngredientAt(List<string> ingredientIds, int index)
    {
        if (ingredientIds == null || index < 0 || index >= ingredientIds.Count) return false;
        ingredientIds.RemoveAt(index);
        return true;
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
        foreach (var candidate in GetIngredients())
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
                string output = TryGetKnownRecipeOutput(manager, current, ingredientId);
                if (string.IsNullOrWhiteSpace(output))
                {
                    string stepName = BuildStepName(current, ingredientId);
                    output = manager.FinishAndNameMix(current, ingredientId, stepName);
                }
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

    private static string TryGetKnownRecipeOutput(ProductManager manager, string productId, string ingredientId)
    {
        try
        {
            var recipe = manager.GetRecipe(productId, ingredientId);
            var item = recipe?.Product?.Item;
            return item != null && !string.IsNullOrWhiteSpace(item.ID) ? item.ID : null;
        }
        catch
        {
            return null;
        }
    }

    private static string BuildStepName(string productId, string ingredientId)
    {
        try
        {
            var product = Registry.GetItem<ProductDefinition>(productId);
            var mixer = Registry.GetItem<PropertyItemDefinition>(ingredientId);
            var newProperty = mixer?.Properties != null && mixer.Properties.Count > 0 ? mixer.Properties[0] : null;
            var outputProperties = product?.Properties != null && newProperty != null
                ? EffectMixCalculator.MixProperties(product.Properties, newProperty, product.DrugType)
                : product?.Properties;

            var screen = NewMixScreen.Instance;
            string generated = screen?.GenerateUniqueName(ToEffectArray(outputProperties), product?.DrugType ?? EDrugType.Marijuana);
            if (!string.IsNullOrWhiteSpace(generated))
                return generated;
        }
        catch (Exception ex)
        {
            MelonLogger.Warning("[Mogul] Vanilla mix name generation failed: " + ex.Message);
        }

        return BuildRecipeName(productId, new[] { ingredientId });
    }

    private static Il2CppReferenceArray<Effect> ToEffectArray(Il2CppSystem.Collections.Generic.List<Effect> properties)
    {
        if (properties == null || properties.Count == 0)
            return null;

        var managed = new Effect[properties.Count];
        for (int i = 0; i < properties.Count; i++)
            managed[i] = properties[i];
        return new Il2CppReferenceArray<Effect>(managed);
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
