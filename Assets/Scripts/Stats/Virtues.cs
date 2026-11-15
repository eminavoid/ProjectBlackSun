using System.Collections.Generic;

public static class Virtues
{
    public static readonly PlayerStats.PlayerStat[] All =
    {
        PlayerStats.PlayerStat.Diplomacy,
        PlayerStats.PlayerStat.Aggresion,
        PlayerStats.PlayerStat.Stewardship,
        PlayerStats.PlayerStat.Intrigue,
        PlayerStats.PlayerStat.Learning
    };

    public static string Name(PlayerStats.PlayerStat virtue)
    {
        return virtue == PlayerStats.PlayerStat.Aggresion ? "Aggression" : virtue.ToString();
    }

    public static string Names(IReadOnlyList<PlayerStats.PlayerStat> virtues)
    {
        var names = new List<string>();
        foreach (PlayerStats.PlayerStat virtue in virtues) names.Add(Name(virtue));
        return JoinAnd(names);
    }

    public static List<Districts> DistrictsOf(PlayerStats.PlayerStat virtue)
    {
        var districts = new List<Districts>();
        DistrictProductionConfig config = Config;
        if (config == null) return districts;

        foreach (Districts district in System.Enum.GetValues(typeof(Districts)))
        {
            if (config.TryGetEntry(district, out DistrictProductionConfig.ProductionEntry entry) && !entry.isImperial && entry.influenceStat == virtue)
            {
                districts.Add(district);
            }
        }

        return districts;
    }

    public static float InfluencePercentPerPoint => Config != null ? Config.InfluenceStatScalePerPoint * 100f : 0f;

    public static string DistrictList(List<Districts> districts)
    {
        var names = new List<string>();
        foreach (Districts district in districts) names.Add(MapStatsQuery.DistrictLabel(district));
        return names.Count == 0 ? string.Empty : "the " + JoinAnd(names);
    }

    private static string JoinAnd(List<string> names)
    {
        if (names.Count <= 1) return names.Count == 1 ? names[0] : string.Empty;
        return $"{string.Join(", ", names.GetRange(0, names.Count - 1))} and {names[names.Count - 1]}";
    }

    private static DistrictProductionConfig Config => InfluenceManager.IsNull ? null : InfluenceManager.Get.ProductionConfig;
}
