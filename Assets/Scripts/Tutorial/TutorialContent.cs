using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Seeds, doctrina e intenciones de los tres turnos guionados.
/// </summary>
public static class TutorialContent
{
    public static DistrictZone ScriptedClericZone { get; private set; }
    public static DistrictZone ScriptedSeedZone { get; private set; }

    private static Seed choirSeed;

    public static void FillIntents(List<AIIntent> intents, int turn)
    {
        if (intents == null) return;

        if (turn <= 1) AddClericIntent(intents);
        else if (turn == 2) AddSeedIntent(intents);
    }

    public static Seed CreateWhisper(DistrictZone zone)
    {
        return Tray(
            "A Whisper in the Street",
            "Someone asks what your cult wants. Answer, and the matter is settled.",
            "Speak",
            "A short answer is enough. The street remembers you.",
            zone,
            Region.Warrens);
    }

    public static Seed CreateRivalReply(DistrictZone zone)
    {
        return Tray(
            "Crimson Choir",
            "The Choir is moving on a street. Rival plays arrive in this tray. Answer them here.",
            "Answer",
            "You answer the Choir. Their move still lands unless you hold the street.",
            zone,
            Region.Agora);
    }

    public static Seed CreateReward(DistrictZone zone)
    {
        return Tray(
            "A Seed Returns",
            "A seed of your own is waiting in this tray. Rewards and rival plays both arrive here.",
            "Take it",
            "The seed is yours to plant.",
            zone,
            Region.Ocularium);
    }

    public static Seed CreatePlantable(string title, string description)
    {
        return Seed.CreateRuntime(
            title,
            description,
            "Wait",
            "It takes root.",
            3,
            Region.Warrens);
    }

    public static Doctrine CreateVigil()
    {
        return Doctrine.CreateRuntime(
            "First Vigil",
            "The cult studies the eclipse. Learning makes White districts yield more Zeal.",
            PlayerStats.PlayerStat.Learning,
            1);
    }

    public static Unlockeable Wrap(Doctrine doctrine)
    {
        return doctrine == null ? null : Unlockeable.CreateRuntime(doctrine.Name, doctrine);
    }

    public static string DistrictLine(Districts district)
    {
        string name = MapStatsQuery.DistrictLabel(district);
        if (InfluenceManager.IsNull || InfluenceManager.Get.ProductionConfig == null)
            return $"{name} district.";

        if (!InfluenceManager.Get.ProductionConfig.TryGetEntry(district, out DistrictProductionConfig.ProductionEntry entry))
            return $"{name} district.";

        return $"{name} district yields {entry.primaryResource}.";
    }

    public static DistrictZone FindPlayerZone()
    {
        if (InfluenceManager.IsNull) return null;

        IReadOnlyList<DistrictZone> zones = InfluenceManager.Get.GetPlayableZones();
        DistrictZone named = null;
        for (int i = 0; i < zones.Count; i++)
        {
            DistrictZone zone = zones[i];
            if (zone == null || !zone.IsPlayable) continue;
            if (zone.SectorName == "ZONE_GREEN_7") named = zone;

            zone.EnsureInfluenceState();
            if (zone.Influence != null &&
                zone.Influence.Status == ZoneControlStatus.Controlled &&
                zone.Influence.Controller == FactionId.Player)
            {
                return zone;
            }
        }

        return named != null ? named : FirstPlayable(zones);
    }

    public static DistrictZone FirstInDistrict(Districts district)
    {
        if (InfluenceManager.IsNull) return null;

        IReadOnlyList<DistrictZone> zones = InfluenceManager.Get.GetPlayableZones();
        for (int i = 0; i < zones.Count; i++)
        {
            DistrictZone zone = zones[i];
            if (zone != null && zone.IsPlayable && zone.District == district) return zone;
        }

        return null;
    }

    private static Seed Tray(
        string title,
        string description,
        string optionTitle,
        string optionBody,
        DistrictZone zone,
        Region region)
    {
        Seed seed = Seed.CreateRuntime(title, description, optionTitle, optionBody, 1, region);
        if (zone != null) seed.Initialize(zone);
        return seed;
    }

    private static void AddClericIntent(List<AIIntent> intents)
    {
        DistrictZone zone = ClericZone();
        ScriptedClericZone = zone;
        if (zone == null) return;

        intents.Add(new AIIntent
        {
            Kind = AIIntentKind.AssignClerics,
            Faction = FactionId.Rival1,
            Target = zone,
            Amount = 1,
            Label = FactionIdUtil.DisplayName(FactionId.Rival1)
        });
    }

    private static void AddSeedIntent(List<AIIntent> intents)
    {
        if (!DistrictsManager.TryGetRandomFreeZoneAnyDistrict(out DistrictZone zone) || zone == null)
        {
            ScriptedSeedZone = null;
            return;
        }

        ScriptedSeedZone = zone;
        if (choirSeed == null)
        {
            choirSeed = Seed.CreateRuntime(
                "Choir Sermon",
                "The Crimson Choir plants a sermon.",
                "Listen",
                "The street hears them.",
                2,
                Region.Agora);
        }

        intents.Add(new AIIntent
        {
            Kind = AIIntentKind.PlantSeed,
            Faction = FactionId.Rival1,
            Target = zone,
            Seed = choirSeed,
            Amount = 1,
            Label = choirSeed.Title
        });
    }

    private static DistrictZone ClericZone()
    {
        if (InfluenceManager.IsNull) return null;

        IReadOnlyList<DistrictZone> zones = InfluenceManager.Get.GetPlayableZones();
        DistrictZone fallback = null;
        for (int i = 0; i < zones.Count; i++)
        {
            DistrictZone zone = zones[i];
            if (zone == null || !zone.IsPlayable) continue;
            zone.EnsureInfluenceState();
            if (zone.Influence == null || !zone.Influence.CanEnterByNormalMeans(FactionId.Rival1)) continue;
            if (fallback == null) fallback = zone;
            if (zone.District == Districts.District1) return zone;
        }

        return fallback != null ? fallback : FirstPlayable(zones);
    }

    private static DistrictZone FirstPlayable(IReadOnlyList<DistrictZone> zones)
    {
        for (int i = 0; i < zones.Count; i++)
        {
            if (zones[i] != null && zones[i].IsPlayable) return zones[i];
        }

        return null;
    }
}
