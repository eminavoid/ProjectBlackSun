using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Seeds, doctrina e intenciones de los tres turnos guionados.
/// </summary>
public static class TutorialContent
{
    public static DistrictZone ScriptedClericZone { get; private set; }

    private static readonly HashSet<Seed> tutorialSeeds = new HashSet<Seed>();
    private static Seed firstSeed;

    public static Seed FirstSeed => firstSeed != null ? firstSeed : (firstSeed = CreatePlantable(
        "First Seed",
        "Plant this on a free zone. Its name and the turns it still needs are how you read a seed."));

    public static bool IsTutorialSeed(Seed seed) =>
        seed != null && (seed.IsGuideSeed || tutorialSeeds.Contains(seed));

    private static bool firstSeedGranted;

    public static void MarkFirstSeedGranted() => firstSeedGranted = true;

    public static void GiveTutorialSeeds()
    {
        DistrictSeedPlantPanel.ReleaseStandardSeeds();
        if (!firstSeedGranted) EnsureOwned(FirstSeed);
    }

    public static void FillIntents(List<AIIntent> intents, int turn)
    {
        if (intents == null || turn > 1) return;
        AddClericIntent(intents);
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

    public static Seed CreatePlantable(string title, string description)
    {
        return Remember(Seed.CreateRuntime(
            title,
            description,
            "Wait",
            "It takes root.",
            3,
            Region.Warrens));
    }

    private static void EnsureOwned(Seed seed)
    {
        if (seed == null) return;
        if (seed == firstSeed) firstSeedGranted = true;
        DistrictSeedPlantPanel panel = Object.FindAnyObjectByType<DistrictSeedPlantPanel>();
        if (panel == null || panel.Owned(seed) > 0) return;
        panel.Grant(seed, 1);
    }

    private static Seed Remember(Seed seed)
    {
        if (seed == null) return null;
        seed.MarkGuideSeed();
        tutorialSeeds.Add(seed);
        return seed;
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

    private static Seed Tray(
        string title,
        string description,
        string optionTitle,
        string optionBody,
        DistrictZone zone,
        Region region)
    {
        Seed seed = Remember(Seed.CreateRuntime(title, description, optionTitle, optionBody, 1, region));
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
