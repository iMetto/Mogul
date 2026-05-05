using System.Collections.Generic;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.Product;
using UnityEngine;

namespace Mogul.Systems;

public enum RejectionReason { None, EmptyShelves, TooExpensive, LowAppeal }

public struct CustomerPreferences
{
    public float QualityExpectation; // 0.0–0.75, skewed low
    public float MaxBudgetPerItem;   // per-package hard price ceiling
    public float TotalBudget;        // total to spend this visit
    public float WeedAffinity;          // fixed 0.8 for walk-ins
    public string[] PreferredEffectIds; // 3 lowercased ScriptableObject names
}

public class SelectedProduct
{
    public string ProductId;
    public string DisplayName;
    public int    QualityLevel; // 0=Trash … 4=Heavenly
    public float  Price;        // per-package (MarketValue for Phase 1)
    public int    Quantity;     // packages requested
    public float  EnjoyScale;   // Lerp(0.66, 1.5, appeal) — base for tip in Phase 2

    public string QualityName => QualityLevel switch
    {
        0 => "Trash", 1 => "Poor", 2 => "Standard", 3 => "Premium", 4 => "Heavenly", _ => "?",
    };

    public float Total => Price * Quantity;
}

public static class CustomerDemand
{
    private static readonly string[] EffectPool =
    {
        "antigravity", "athletic", "balding", "brighteyed", "calming",
        "caloriedense", "cyclopean", "disorienting", "electrifying", "energizing",
        "euphoric", "explosive", "focused", "foggy", "gingeritis",
        "glowie", "jennerising", "laxative", "lethal", "longfaced",
        "munchies", "paranoia", "refreshing", "schizophrenic", "sedating",
        "seizure", "shrinking", "slippery", "smelly", "sneaky",
        "spicy", "thoughtprovoking", "toxic", "tropicthunder", "zombifying"
    };

    public static CustomerPreferences GeneratePreferences(int seed, Customer customerComp = null)
    {
        var rng = new System.Random(seed);
        var data = customerComp?.customerData;

        float qualityExp;
        if (data != null)
        {
            // Map Standards → quality expectation using the same scale as EQuality (0–4 × 0.25)
            int qualLevel = (int)data.Standards.GetCorrespondingQuality();
            qualityExp = Mathf.Clamp(qualLevel * 0.25f + (float)(rng.NextDouble() - 0.5) * 0.08f, 0f, 1f);
        }
        else
        {
            double roll = rng.NextDouble();
            if      (roll < 0.30) qualityExp = (float)(rng.NextDouble() * 0.12);
            else if (roll < 0.65) qualityExp = 0.13f + (float)(rng.NextDouble() * 0.17);
            else if (roll < 0.90) qualityExp = 0.31f + (float)(rng.NextDouble() * 0.24);
            else                  qualityExp = 0.56f + (float)(rng.NextDouble() * 0.19);
        }

        float budget;
        if (data != null && data.MaxWeeklySpend > 0f)
        {
            int ordersPerWeek = System.Math.Max(1, (data.MinOrdersPerWeek + data.MaxOrdersPerWeek) / 2);
            float visitMin = data.MinWeeklySpend / ordersPerWeek;
            float visitMax = data.MaxWeeklySpend / ordersPerWeek;
            budget = visitMin + (float)(rng.NextDouble() * (visitMax - visitMin));
        }
        else
        {
            budget = 50f + (float)(rng.NextDouble() * 150f);
        }

        float weedAffinity = 0.8f;
        if (data?.DefaultAffinityData?.ProductAffinities != null)
        {
            var affinities = data.DefaultAffinityData.ProductAffinities;
            for (int i = 0; i < affinities.Count; i++)
            {
                var pa = affinities[i];
                if (pa != null && pa.DrugType == EDrugType.Marijuana)
                {
                    weedAffinity = Mathf.InverseLerp(-1f, 1f, pa.Affinity);
                    break;
                }
            }
        }

        // Fisher-Yates partial shuffle to pick 3 effect IDs
        var pool = new string[EffectPool.Length];
        System.Array.Copy(EffectPool, pool, EffectPool.Length);
        for (int i = 0; i < 3; i++)
        {
            int j = i + rng.Next(EffectPool.Length - i);
            string tmp = pool[i]; pool[i] = pool[j]; pool[j] = tmp;
        }

        return new CustomerPreferences
        {
            QualityExpectation = qualityExp,
            MaxBudgetPerItem   = budget * 0.6f,
            TotalBudget        = budget,
            WeedAffinity       = weedAffinity,
            PreferredEffectIds = new[] { pool[0], pool[1], pool[2] },
        };
    }

    // Scores available stock against customer preferences and returns what the customer
    // decided to buy. stock is package-granularity (from StorageScanner.Scan).
    // seed is the same seed used for GeneratePreferences so randomness is consistent.
    public static (List<SelectedProduct> selected, RejectionReason rejection) DecidePurchases(
        CustomerPreferences prefs, List<StorageProduct> stock, int seed)
    {
        if (stock.Count == 0)
            return (new List<SelectedProduct>(), RejectionReason.EmptyShelves);

        int overBudget = 0;
        var scored = new List<(StorageProduct prod, float appeal)>(stock.Count);

        foreach (var s in stock)
        {
            if (s.Price > prefs.MaxBudgetPerItem) { overBudget++; continue; }

            float qualScalar = s.QualityLevel * 0.25f;
            float delta      = qualScalar - prefs.QualityExpectation;
            float step       = delta >= 0.25f ? 1.0f : delta >= 0f ? 0.5f : delta >= -0.25f ? -0.5f : -1.0f;

            int matchCount = 0;
            if (prefs.PreferredEffectIds != null && s.EffectIds != null)
                foreach (var e in s.EffectIds)
                    foreach (var p in prefs.PreferredEffectIds)
                        if (string.Equals(e, p, System.StringComparison.Ordinal)) { matchCount++; break; }
            float effectScore = (matchCount / 3f) * 0.4f;

            float raw    = prefs.WeedAffinity * 0.3f + effectScore + step * 0.3f;
            float appeal = Mathf.InverseLerp(-0.6f, 1.0f, raw);
            scored.Add((s, appeal));
        }

        if (scored.Count == 0)
            return (new List<SelectedProduct>(), overBudget > 0 ? RejectionReason.TooExpensive : RejectionReason.LowAppeal);

        scored.Sort((a, b) => b.appeal.CompareTo(a.appeal));

        var   rng       = new System.Random(seed ^ 0x4A2F);
        float remaining = prefs.TotalBudget;
        var   selected  = new List<SelectedProduct>();

        while (scored.Count > 0 && remaining >= scored[0].prod.Price)
        {
            // 50% pick top-ranked, 50% random — mirrors OTC selection loop
            int pick = rng.NextDouble() < 0.5 ? 0 : rng.Next(0, scored.Count);
            var (prod, appeal) = scored[pick];
            scored.RemoveAt(pick);

            float enjoyScale = Mathf.Lerp(0.66f, 1.5f, appeal);
            int   qty        = Mathf.RoundToInt(remaining * enjoyScale / prod.Price);
            qty = Mathf.Clamp(qty, 1, prod.TotalPackages);

            selected.Add(new SelectedProduct
            {
                ProductId    = prod.ProductId,
                DisplayName  = prod.DisplayName,
                QualityLevel = prod.QualityLevel,
                Price        = prod.Price,
                Quantity     = qty,
                EnjoyScale   = enjoyScale,
            });

            remaining -= prod.Price * qty;
        }

        if (selected.Count == 0)
            return (new List<SelectedProduct>(), overBudget > 0 ? RejectionReason.TooExpensive : RejectionReason.LowAppeal);

        return (selected, RejectionReason.None);
    }
}
