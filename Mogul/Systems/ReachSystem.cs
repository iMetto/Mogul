namespace Mogul.Systems;

public enum ReachTier
{
    Micro       = 0,  // 0 – 10,000
    Local       = 1,  // 10,001 – 250,000
    Rising      = 2,  // 250,001 – 1,000,000
    Established = 3,  // 1,000,001 – 10,000,000
    Popular     = 4,  // 10,000,001 – 50,000,000
    Famous      = 5,  // 50,000,001 – 100,000,000
    Viral       = 6,  // 100,000,001 – 500,000,000
    Legend      = 7,  // 500,000,001+
}

public static class ReachSystem
{
    // Minimum reach required to enter each tier (index matches ReachTier int value).
    private static readonly int[] TierFloors =
    {
        0,           // Micro
        10_001,      // Local
        250_001,     // Rising
        1_000_001,   // Established
        10_000_001,  // Popular
        50_000_001,  // Famous
        100_000_001, // Viral
        500_000_001, // Legend
    };

    // (normalMin, normalMax, outlierMax) per tier.
    // Outlier = 20% of walk-ins roll from (normalMax, outlierMax) instead.
    private static readonly (float normalMin, float normalMax, float outlierMax)[] TierBudgets =
    {
        (60f,    200f,    400f),    // Micro
        (100f,   350f,    700f),    // Local
        (200f,   600f,    1_200f),  // Rising
        (400f,   1_000f,  2_000f),  // Established
        (800f,   2_000f,  4_000f),  // Popular
        (1_500f, 4_000f,  8_000f),  // Famous
        (3_000f, 8_000f,  16_000f), // Viral
        (6_000f, 20_000f, 50_000f), // Legend
    };

    private static readonly string[] TierNames =
    {
        "Nobody", "Local", "Rising", "Established",
        "Popular", "Famous", "Viral", "Legend",
    };

    // 20% of spawning walk-ins are outliers (higher budget).
    private const float OutlierChance = 0.20f;

    public static ReachTier GetTier(int reach)
    {
        for (int i = TierFloors.Length - 1; i >= 0; i--)
            if (reach >= TierFloors[i]) return (ReachTier)i;
        return ReachTier.Micro;
    }

    public static string GetTierName(ReachTier tier) => TierNames[(int)tier];

    // Returns the budget range for a walk-in NPC at this tier.
    // Pass the NPC's rng to consistently decide outlier vs normal.
    public static (float min, float max) GetBudgetRange(ReachTier tier, System.Random rng)
    {
        var (normalMin, normalMax, outlierMax) = TierBudgets[(int)tier];
        bool isOutlier = rng.NextDouble() < OutlierChance;
        return isOutlier ? (normalMax, outlierMax) : (normalMin, normalMax);
    }

    public static string FormatReach(int reach)
    {
        if (reach >= 1_000_000_000) return $"{reach / 1_000_000_000.0:0.##}B";
        if (reach >= 1_000_000)     return $"{reach / 1_000_000.0:0.##}M";
        if (reach >= 1_000)         return $"{reach / 1_000.0:0.##}K";
        return reach.ToString();
    }
}
