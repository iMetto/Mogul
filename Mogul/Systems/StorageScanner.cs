using System.Collections.Generic;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Effects;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.Storage;
using MelonLoader;
using UnityEngine;

namespace Mogul.Systems;

public class StorageProduct
{
    public string ProductId;
    public string DisplayName;
    public int QualityLevel;   // 0=Trash 1=Poor 2=Standard 3=Premium 4=Heavenly
    public int TotalPackages;  // packages available across all slots
    public float Price;        // player-set price per unit, falls back to MarketValue
    public List<string> EffectIds = new List<string>();

    public string QualityName => QualityLevel switch
    {
        0 => "Trash",
        1 => "Poor",
        2 => "Standard",
        3 => "Premium",
        4 => "Heavenly",
        _ => "Unknown",
    };
}

public static class StorageScanner
{
    // Scans all StorageEntity in the building, groups by (ProductId, QualityLevel),
    // returns sorted alphabetically by DisplayName.
    public static List<StorageProduct> Scan(GameObject buildingRoot)
    {
        var aggregated = new Dictionary<string, StorageProduct>();

        var storages = buildingRoot.GetComponentsInChildren<StorageEntity>(true);
        foreach (var storage in storages)
        {
            var slots = storage.ItemSlots;
            if (slots == null) continue;

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot?.ItemInstance == null) continue;

                var product = slot.ItemInstance.TryCast<ProductItemInstance>();
                if (product == null) continue;

                var def = product.Definition?.TryCast<ProductDefinition>();
                if (def?.ID == null) continue;

                int quality = (int)product.Quality;
                string key = $"{def.ID}:{quality}";

                if (aggregated.TryGetValue(key, out var existing))
                {
                    existing.TotalPackages += slot.Quantity;
                }
                else
                {
                    var effectIds = new List<string>();
                    try
                    {
                        if (def.Properties != null)
                            for (int ei = 0; ei < def.Properties.Count; ei++)
                            {
                                var eff = def.Properties[ei];
                                if (eff != null)
                                {
                                    effectIds.Add(eff.name.ToLower());
                                }
                            }
                    }
                    catch (System.Exception ex)
                    {
                        MelonLogger.Warning($"[Mogul:EffectName] failed reading effects for {def.ID}: {ex.Message}");
                    }

                    float price;
                    try { price = NetworkSingleton<ProductManager>.Instance?.GetPrice(def) ?? def.MarketValue; }
                    catch { price = def.MarketValue; }

                    aggregated[key] = new StorageProduct
                    {
                        ProductId     = def.ID,
                        DisplayName   = def.Name ?? def.ID,
                        QualityLevel  = quality,
                        TotalPackages = slot.Quantity,
                        Price         = price,
                        EffectIds     = effectIds,
                    };
                }
            }
        }

        var result = new List<StorageProduct>(aggregated.Values);
        result.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.OrdinalIgnoreCase));
        return result;
    }

    // Removes one package from the first slot matching productId + qualityLevel.
    // Returns true if a package was removed.
    public static bool TakeOne(GameObject buildingRoot, string productId, int qualityLevel)
    {
        var storages = buildingRoot.GetComponentsInChildren<StorageEntity>(true);
        foreach (var storage in storages)
        {
            var slots = storage.ItemSlots;
            if (slots == null) continue;

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot?.ItemInstance == null) continue;

                var product = slot.ItemInstance.TryCast<ProductItemInstance>();
                if (product == null) continue;

                var def = product.Definition?.TryCast<ProductDefinition>();
                if (def?.ID != productId) continue;
                if ((int)product.Quality != qualityLevel) continue;

                try
                {
                    if (slot.Quantity <= 1)
                        slot.ClearStoredInstance(false);
                    else
                        slot.ChangeQuantity(-1);
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Warning($"[Mogul] Storage removal failed for {productId}: {ex.Message}");
                    return false;
                }

                return true;
            }
        }
        return false;
    }
}
