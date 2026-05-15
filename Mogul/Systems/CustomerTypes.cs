using System.Collections.Generic;

namespace Mogul.Systems;

public enum MogulCustomerType
{
    WalkIn,
    OnlineBuyer,
    BulkBuyer,
    GangLeader,
    Importer,
}

public class MogulCustomerTypeProfile
{
    public MogulCustomerType Type;
    public string Id = "";
    public string DisplayName = "";
    public string Flavor = "";
    public int MinReach;
    public float BudgetMultiplier = 1f;
    public float QuantityMultiplier = 1f;
    public float QualityFloor;
    public float WeedAffinity = 0.8f;
    public float TipMultiplier = 1f;
    public int DeadlineHours = 12;
    public string[] PreferredEffectBias = System.Array.Empty<string>();
}

public static class CustomerTypes
{
    private static readonly MogulCustomerTypeProfile[] Profiles =
    {
        new()
        {
            Type = MogulCustomerType.OnlineBuyer,
            Id = "online_buyer",
            DisplayName = "Online Buyer",
            Flavor = "Standard app traffic. Flexible taste, normal spend.",
            MinReach = 0,
            BudgetMultiplier = 1.1f,
            QuantityMultiplier = 1.0f,
            QualityFloor = 0.15f,
            TipMultiplier = 1.0f,
            DeadlineHours = 12,
        },
        new()
        {
            Type = MogulCustomerType.BulkBuyer,
            Id = "bulk_buyer",
            DisplayName = "Bulk Buyer",
            Flavor = "Wants volume and predictable pricing. Less picky about effect matching.",
            MinReach = 5_000,
            BudgetMultiplier = 2.6f,
            QuantityMultiplier = 2.8f,
            QualityFloor = 0.25f,
            TipMultiplier = 0.8f,
            DeadlineHours = 18,
        },
        new()
        {
            Type = MogulCustomerType.GangLeader,
            Id = "gang_leader",
            DisplayName = "Gang Leader",
            Flavor = "Pays for strong reputation product. Likes aggressive, energetic effects.",
            MinReach = 50_000,
            BudgetMultiplier = 4.0f,
            QuantityMultiplier = 2.0f,
            QualityFloor = 0.50f,
            WeedAffinity = 0.95f,
            TipMultiplier = 1.4f,
            DeadlineHours = 8,
            PreferredEffectBias = new[] { "energizing", "focused", "explosive", "athletic", "euphoric" },
        },
        new()
        {
            Type = MogulCustomerType.Importer,
            Id = "importer",
            DisplayName = "Importer",
            Flavor = "High-value buyer. Expects premium quality and clean fulfillment.",
            MinReach = 250_000,
            BudgetMultiplier = 7.0f,
            QuantityMultiplier = 1.6f,
            QualityFloor = 0.70f,
            WeedAffinity = 1.0f,
            TipMultiplier = 2.0f,
            DeadlineHours = 24,
            PreferredEffectBias = new[] { "euphoric", "calming", "sedating", "refreshing", "thoughtprovoking" },
        },
    };

    public static IReadOnlyList<MogulCustomerTypeProfile> All => Profiles;

    public static MogulCustomerTypeProfile Get(string id)
    {
        foreach (var profile in Profiles)
            if (string.Equals(profile.Id, id, System.StringComparison.OrdinalIgnoreCase))
                return profile;
        return Profiles[0];
    }

    public static MogulCustomerTypeProfile PickForReach(int reach, System.Random rng)
    {
        var eligible = new List<MogulCustomerTypeProfile>();
        foreach (var profile in Profiles)
            if (reach >= profile.MinReach)
                eligible.Add(profile);

        if (eligible.Count == 0) return Profiles[0];

        int roll = rng.Next(100);
        for (int i = eligible.Count - 1; i >= 0; i--)
        {
            var profile = eligible[i];
            if (profile.Type == MogulCustomerType.Importer && roll >= 92) return profile;
            if (profile.Type == MogulCustomerType.GangLeader && roll >= 78) return profile;
            if (profile.Type == MogulCustomerType.BulkBuyer && roll >= 52) return profile;
        }

        return eligible[0];
    }

    public static CustomerPreferences ApplyToPreferences(CustomerPreferences prefs, MogulCustomerTypeProfile profile, System.Random rng)
    {
        prefs.TotalBudget *= profile.BudgetMultiplier;
        prefs.MaxBudgetPerItem *= profile.BudgetMultiplier;
        prefs.QualityExpectation = System.Math.Max(prefs.QualityExpectation, profile.QualityFloor);
        prefs.WeedAffinity = profile.WeedAffinity;

        if (profile.PreferredEffectBias != null && profile.PreferredEffectBias.Length > 0)
        {
            var preferred = new string[3];
            for (int i = 0; i < preferred.Length; i++)
                preferred[i] = profile.PreferredEffectBias[rng.Next(profile.PreferredEffectBias.Length)];
            prefs.PreferredEffectIds = preferred;
        }

        return prefs;
    }
}
