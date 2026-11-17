using UnityEngine;

public enum TheologyRewardKind
{
    Curse,
    Blessing,
    Seed,
    Doctrine
}

public struct TheologyChances
{
    public float Curse;
    public float Blessing;
    public float Seed;
    public float Doctrine;
}

/// <summary>
/// Chances del ojo. Los puntos de un tier incluyen los tiers anteriores.
/// </summary>
public static class TheologyOdds
{
    public static readonly int[] Costs = { 10, 12, 15, 18, 21, 24, 27, 30, 33, 36, 40, 45, 50, 55, 60, 65, 70, 80, 90, 100 };

    private static readonly float[] OfferingPoints = { 2.5f, 5f, 10f, 20f };
    private static readonly float[] SacrificePoints = { 5f, 10f, 20f, 40f };

    public static int NextCost(int tiersPaid)
    {
        if (tiersPaid < 0 || tiersPaid >= Costs.Length) return 0;
        return Costs[tiersPaid];
    }

    public static float PointsFor(int tiersPaid, bool sacrifice)
    {
        float[] table = sacrifice ? SacrificePoints : OfferingPoints;
        int count = Mathf.Clamp(tiersPaid, 0, table.Length);
        float sum = 0f;
        for (int i = 0; i < count; i++) sum += table[i];
        return sum;
    }

    public static TheologyChances GuaranteedResolve(int offeringTiers, int sacrificeTiers)
    {
        float curse = 0f;
        float blessing = 0f;
        float seed = 0f;
        float doctrine = 100f;
        float points = PointsFor(offeringTiers, false) + PointsFor(sacrificeTiers, true);

        float fromCurse = Mathf.Min(points, curse);
        curse -= fromCurse;
        blessing += fromCurse * 0.5f;
        seed += fromCurse * 0.3f;
        doctrine += fromCurse * 0.2f;
        points -= fromCurse;

        float fromBlessing = Mathf.Min(points, Mathf.Max(0f, blessing));
        blessing -= fromBlessing;
        seed += fromBlessing * 0.5f;
        doctrine += fromBlessing * 0.5f;
        points -= fromBlessing;

        float fromSeed = Mathf.Min(points, Mathf.Max(0f, seed));
        seed -= fromSeed;
        doctrine += fromSeed;

        return new TheologyChances
        {
            Curse = Mathf.Max(0f, curse),
            Blessing = Mathf.Max(0f, blessing),
            Seed = Mathf.Max(0f, seed),
            Doctrine = Mathf.Max(0f, doctrine)
        };
    }

    public static TheologyChances Resolve(int offeringTiers, int sacrificeTiers)
    {
        float curse = 35f;
        float blessing = 20f;
        float seed = 30f;
        float doctrine = 15f;
        float points = PointsFor(offeringTiers, false) + PointsFor(sacrificeTiers, true);

        float fromCurse = Mathf.Min(points, curse);
        curse -= fromCurse;
        blessing += fromCurse * 0.5f;
        seed += fromCurse * 0.3f;
        doctrine += fromCurse * 0.2f;
        points -= fromCurse;

        float fromBlessing = Mathf.Min(points, Mathf.Max(0f, blessing));
        blessing -= fromBlessing;
        seed += fromBlessing * 0.5f;
        doctrine += fromBlessing * 0.5f;
        points -= fromBlessing;

        float fromSeed = Mathf.Min(points, Mathf.Max(0f, seed));
        seed -= fromSeed;
        doctrine += fromSeed;

        return new TheologyChances
        {
            Curse = Mathf.Max(0f, curse),
            Blessing = Mathf.Max(0f, blessing),
            Seed = Mathf.Max(0f, seed),
            Doctrine = Mathf.Max(0f, doctrine)
        };
    }

    public static TheologyRewardKind Roll(TheologyChances chances)
    {
        float total = chances.Curse + chances.Blessing + chances.Seed + chances.Doctrine;
        if (total <= 0f) return TheologyRewardKind.Curse;

        float roll = Random.value * total;
        if (roll < chances.Curse) return TheologyRewardKind.Curse;
        roll -= chances.Curse;
        if (roll < chances.Blessing) return TheologyRewardKind.Blessing;
        roll -= chances.Blessing;
        if (roll < chances.Seed) return TheologyRewardKind.Seed;
        return TheologyRewardKind.Doctrine;
    }
}
