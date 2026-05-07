using System.Collections.Generic;
using Il2CppScheduleOne.Product;
using UnityEngine;

namespace Mogul.Systems;

public enum RejectionReason { None, EmptyShelves, TooExpensive, LowAppeal }

public struct CustomerPreferences
{
    public float QualityExpectation; // 0.0–0.75, skewed low
    public float MaxBudgetPerItem;   // per-package hard price ceiling (60% of TotalBudget)
    public float TotalBudget;        // total available this visit (before visit commitment)
    public float WeedAffinity;       // fixed 0.8 for walk-ins
    public string[] PreferredEffectIds; // 3 lowercased ScriptableObject names
}

public class SelectedProduct
{
    public string ProductId;
    public string DisplayName;
    public int    QualityLevel; // 0=Trash … 4=Heavenly
    public float  Price;        // per-package
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

    // Below this appeal score the NPC won't buy anything — nothing in the store
    // is close enough to what they want.
    private const float MinAppealToStay = 0.15f;

    public static CustomerPreferences GeneratePreferences(int seed)
    {
        var rng = new System.Random(seed);

        // Quality expectation — weighted toward low taste (generic walk-ins)
        float qualityExp;
        double roll = rng.NextDouble();
        if      (roll < 0.30) qualityExp = (float)(rng.NextDouble() * 0.12);
        else if (roll < 0.65) qualityExp = 0.13f + (float)(rng.NextDouble() * 0.17);
        else if (roll < 0.90) qualityExp = 0.31f + (float)(rng.NextDouble() * 0.24);
        else                  qualityExp = 0.56f + (float)(rng.NextDouble() * 0.19);

        // Budget derived from current store reach tier.
        // 20% of walk-ins are outliers and roll from the upper band.
        var tier = ReachSystem.GetTier(MogulNetwork.Data.Reach);
        var (budgetMin, budgetMax) = ReachSystem.GetBudgetRange(tier, rng);
        float budget = budgetMin + (float)(rng.NextDouble() * (budgetMax - budgetMin));

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
            WeedAffinity       = 0.8f,
            PreferredEffectIds = new[] { pool[0], pool[1], pool[2] },
        };
    }

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

        // If the best available product doesn't meet minimum appeal, the NPC walks out.
        float bestAppeal = scored[0].appeal;
        if (bestAppeal < MinAppealToStay)
            return (new List<SelectedProduct>(), RejectionReason.LowAppeal);

        // How much of their budget they commit this visit scales with how much they like
        // what's on offer: 25% at the minimum threshold, 100% when appeal is great.
        float visitFraction = Mathf.InverseLerp(MinAppealToStay, 0.80f, bestAppeal);
        float remaining     = prefs.TotalBudget * Mathf.Lerp(0.25f, 1.0f, visitFraction);

        var   rng      = new System.Random(seed ^ 0x4A2F);
        var   selected = new List<SelectedProduct>();

        while (scored.Count > 0 && remaining >= scored[0].prod.Price)
        {
            int pick = rng.NextDouble() < 0.5 ? 0 : rng.Next(0, scored.Count);
            var (prod, appeal) = scored[pick];
            scored.RemoveAt(pick);

            float enjoyScale = Mathf.Lerp(0.66f, 1.5f, appeal);

            // How much of the remaining budget to spend on this item (30–85%).
            // Capped at remaining/price so we never exceed the committed budget.
            float itemFraction = Mathf.Lerp(0.30f, 0.85f, appeal);
            int   maxAffordable = Mathf.FloorToInt(remaining / prod.Price);
            int   qty = Mathf.Max(1, Mathf.FloorToInt(remaining * itemFraction / prod.Price));
            qty = Mathf.Clamp(qty, 1, Mathf.Min(prod.TotalPackages, maxAffordable));

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
