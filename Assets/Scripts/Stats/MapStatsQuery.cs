using System;
using System.Collections.Generic;
using UnityEngine;

public class MapStatsFactionRow
{
    public FactionId Faction;
    public string DisplayName;
    public Color Color;
    public int Influence;
    public float SharePercent;
    public int Clerics;
    public int ControlledZones;
    public int PoolClerics;
    public bool ExpelledAtCap;
}

public class MapStatsSeedRow
{
    public Seed Seed;
    public DistrictZone Zone;
    public string Title;
    public Districts District;
    public string DistrictName;
    public string SectorName;
    public int TurnsRemaining;
    public SeedEventType EventType;
    public SeedDifficulty Difficulty;
}

public class MapStatsNodeRow
{
    public DistrictZone Zone;
    public string SectorName;
    public string ControlLabel;
    public FactionId? Controller;
    public int Influence;
    public int Cap;
    public float LeaderPercent;
    public string SeedTitle;
}

public class MapStatsDistrictRow
{
    public Districts District;
    public string DisplayName;
    public bool IsImperial;
    public FactionId? Controller;
    public string ControlLabel;
    public int ZoneCount;
    public int OccupiedCount;
    public int ControlledCount;
}

public class MapStatsNodeSnapshot
{
    public DistrictZone Zone;
    public string SectorName;
    public Districts District;
    public string DistrictName;
    public string ControlLabel;
    public FactionId? Controller;
    public int TotalInfluence;
    public int Cap;
    public string IntentLine;
    public string TitheLine;
    public List<MapStatsFactionRow> Factions = new List<MapStatsFactionRow>();
    public MapStatsSeedRow PlantedSeed;
}

public class MapStatsDistrictSnapshot
{
    public Districts District;
    public string DisplayName;
    public bool IsImperial;
    public FactionId? Controller;
    public string ControlLabel;
    public bool SpecialEventsUnlocked;
    public string ProductionLine;
    public int ZoneCount;
    public int OccupiedCount;
    public int ControlledCount;
    public List<MapStatsFactionRow> Factions = new List<MapStatsFactionRow>();
    public List<MapStatsNodeRow> Nodes = new List<MapStatsNodeRow>();
    public List<MapStatsSeedRow> Seeds = new List<MapStatsSeedRow>();
}

public class MapStatsGlobalSnapshot
{
    public int PlayableZones;
    public int OccupiedZones;
    public int ControlledZones;
    public int ContestedZones;
    public FactionId? FaithEclipseLeader;
    public int FaithEclipseLeading;
    public string FaithEclipseLeaderLine;
    public List<MapStatsFactionRow> Factions = new List<MapStatsFactionRow>();
    public List<MapStatsDistrictRow> Districts = new List<MapStatsDistrictRow>();
    public List<MapStatsSeedRow> Seeds = new List<MapStatsSeedRow>();
}

/// <summary>
/// Snapshots de stats de mapa (ciudad / distrito / nodo). Sin UI.
/// </summary>
public static class MapStatsQuery
{
    public static string DistrictLabel(Districts district)
    {
        DistrictColorMapping mapping = ResolveColorMapping();
        return DistrictColorMapping.GetDisplayName(district, mapping);
    }

    public static MapStatsGlobalSnapshot CaptureGlobal()
    {
        MapStatsGlobalSnapshot snapshot = new MapStatsGlobalSnapshot();
        List<DistrictZone> zones = GetPlayableZones();

        snapshot.PlayableZones = zones.Count;
        for (int i = 0; i < zones.Count; i++)
        {
            DistrictZone zone = zones[i];
            if (zone == null) continue;
            if (zone.IsOccupied) snapshot.OccupiedZones++;

            ZoneInfluenceState state = GetState(zone);
            if (state != null && state.Status == ZoneControlStatus.Controlled && state.Controller.HasValue)
            {
                snapshot.ControlledZones++;
            }
            else
            {
                snapshot.ContestedZones++;
            }

            TryAddSeed(snapshot.Seeds, zone);
        }

        snapshot.Seeds.Sort(CompareSeeds);

        foreach (Districts district in Enum.GetValues(typeof(Districts)))
        {
            snapshot.Districts.Add(CaptureDistrictRow(district, GetDistrictZones(district, zones)));
        }

        foreach (FactionId faction in FactionIdUtil.All)
        {
            MapStatsFactionRow row = new MapStatsFactionRow
            {
                Faction = faction,
                DisplayName = FactionIdUtil.DisplayName(faction),
                Color = FactionPalette.For(faction),
                ControlledZones = CountControlledZones(zones, faction),
                Clerics = CountAssignedClerics(zones, faction),
                PoolClerics = GetClericPool(faction)
            };
            snapshot.Factions.Add(row);
        }

        snapshot.FaithEclipseLeader = ResolveFaithEclipseLeader(snapshot.Factions, out int leading, out bool tie);
        snapshot.FaithEclipseLeading = leading;
        if (snapshot.FaithEclipseLeader.HasValue && !tie)
        {
            snapshot.FaithEclipseLeaderLine =
                $"Líder: {FactionIdUtil.DisplayName(snapshot.FaithEclipseLeader.Value)} ({leading} zonas)";
        }
        else
        {
            snapshot.FaithEclipseLeaderLine = "Líder: empate / nadie";
        }

        return snapshot;
    }

    public static MapStatsDistrictSnapshot CaptureDistrict(Districts district)
    {
        List<DistrictZone> zones = GetDistrictZones(district, GetPlayableZones());
        MapStatsDistrictRow header = CaptureDistrictRow(district, zones);

        MapStatsDistrictSnapshot snapshot = new MapStatsDistrictSnapshot
        {
            District = district,
            DisplayName = header.DisplayName,
            IsImperial = header.IsImperial,
            Controller = header.Controller,
            ControlLabel = header.ControlLabel,
            SpecialEventsUnlocked = header.Controller.HasValue,
            ProductionLine = FormatProductionLine(district, header.Controller.HasValue),
            ZoneCount = header.ZoneCount,
            OccupiedCount = header.OccupiedCount,
            ControlledCount = header.ControlledCount
        };

        Dictionary<FactionId, MapStatsFactionRow> factions = CreateEmptyFactionRows();
        for (int i = 0; i < zones.Count; i++)
        {
            DistrictZone zone = zones[i];
            if (zone == null) continue;

            snapshot.Nodes.Add(CaptureNodeRow(zone));
            TryAddSeed(snapshot.Seeds, zone);

            ZoneInfluenceState state = GetState(zone);
            if (state == null) continue;

            foreach (FactionId faction in FactionIdUtil.All)
            {
                MapStatsFactionRow row = factions[faction];
                row.Influence += state.GetShare(faction);
                row.Clerics += state.GetClerics(faction);
                if (state.Status == ZoneControlStatus.Controlled && state.Controller == faction)
                {
                    row.ControlledZones++;
                }
            }
        }

        snapshot.Nodes.Sort((a, b) => string.CompareOrdinal(a.SectorName, b.SectorName));
        snapshot.Seeds.Sort(CompareSeeds);

        int districtInfluence = 0;
        foreach (KeyValuePair<FactionId, MapStatsFactionRow> pair in factions)
        {
            districtInfluence += pair.Value.Influence;
        }

        foreach (FactionId faction in FactionIdUtil.All)
        {
            MapStatsFactionRow row = factions[faction];
            row.SharePercent = districtInfluence > 0 ? row.Influence * 100f / districtInfluence : 0f;
            snapshot.Factions.Add(row);
        }

        return snapshot;
    }

    public static MapStatsNodeSnapshot CaptureNode(DistrictZone zone)
    {
        MapStatsNodeSnapshot snapshot = new MapStatsNodeSnapshot();
        if (zone == null) return snapshot;

        zone.EnsureInfluenceState();
        ZoneInfluenceState state = zone.Influence;

        snapshot.Zone = zone;
        snapshot.SectorName = zone.SectorName;
        snapshot.District = zone.District;
        snapshot.DistrictName = DistrictLabel(zone.District);
        snapshot.ControlLabel = FormatZoneControl(state);
        snapshot.Controller = state != null && state.Status == ZoneControlStatus.Controlled
            ? state.Controller
            : null;
        snapshot.TotalInfluence = state != null ? state.TotalInfluence : 0;
        snapshot.Cap = state != null ? state.Cap : ZoneInfluenceState.DefaultCap;
        snapshot.IntentLine = FormatZoneIntents(zone);
        snapshot.TitheLine = FormatZoneTithe(zone, state);

        int total = snapshot.TotalInfluence;
        foreach (FactionId faction in FactionIdUtil.All)
        {
            int share = state != null ? state.GetShare(faction) : 0;
            int clerics = state != null ? state.GetClerics(faction) : 0;
            bool expelled = state != null && state.IsExpelledWhileAtCap(faction);
            if (share <= 0 && clerics <= 0 && !expelled) continue;

            snapshot.Factions.Add(new MapStatsFactionRow
            {
                Faction = faction,
                DisplayName = FactionIdUtil.DisplayName(faction),
                Color = FactionPalette.For(faction),
                Influence = share,
                SharePercent = total > 0 ? share * 100f / total : 0f,
                Clerics = clerics,
                ExpelledAtCap = expelled
            });
        }

        if (zone.PlantedSeed != null)
        {
            snapshot.PlantedSeed = CreateSeedRow(zone, zone.PlantedSeed);
        }

        return snapshot;
    }

    public static MapStatsNodeRow CaptureNodeRow(DistrictZone zone)
    {
        ZoneInfluenceState state = GetState(zone);
        FactionId? leader = state != null && state.Status == ZoneControlStatus.Controlled
            ? state.Controller
            : LeadingFaction(state);

        return new MapStatsNodeRow
        {
            Zone = zone,
            SectorName = zone != null ? zone.SectorName : string.Empty,
            ControlLabel = FormatZoneControl(state),
            Controller = leader,
            Influence = state != null ? state.TotalInfluence : 0,
            Cap = state != null ? state.Cap : ZoneInfluenceState.DefaultCap,
            LeaderPercent = leader.HasValue && state != null ? state.GetSharePercent(leader.Value) : 0f,
            SeedTitle = zone != null && zone.PlantedSeed != null ? zone.PlantedSeed.Title : "Libre"
        };
    }

    private static MapStatsDistrictRow CaptureDistrictRow(Districts district, List<DistrictZone> zones)
    {
        bool imperial = IsImperial(district);
        int occupied = 0;
        int controlled = 0;
        Dictionary<FactionId, int> controlledCounts = new Dictionary<FactionId, int>();

        for (int i = 0; i < zones.Count; i++)
        {
            DistrictZone zone = zones[i];
            if (zone == null) continue;
            if (zone.IsOccupied) occupied++;

            ZoneInfluenceState state = GetState(zone);
            if (state == null || state.Status != ZoneControlStatus.Controlled || !state.Controller.HasValue)
            {
                continue;
            }

            controlled++;
            FactionId owner = state.Controller.Value;
            controlledCounts.TryGetValue(owner, out int count);
            controlledCounts[owner] = count + 1;
        }

        FactionId? districtOwner = null;
        if (!imperial && zones.Count > 0)
        {
            foreach (KeyValuePair<FactionId, int> pair in controlledCounts)
            {
                if (pair.Value * 2 > zones.Count)
                {
                    districtOwner = pair.Key;
                    break;
                }
            }
        }

        string controlLabel;
        if (imperial) controlLabel = "Imperial (sin control)";
        else if (districtOwner.HasValue) controlLabel = "Domina: " + FactionIdUtil.DisplayName(districtOwner.Value);
        else controlLabel = "En disputa";

        return new MapStatsDistrictRow
        {
            District = district,
            DisplayName = DistrictLabel(district),
            IsImperial = imperial,
            Controller = districtOwner,
            ControlLabel = controlLabel,
            ZoneCount = zones.Count,
            OccupiedCount = occupied,
            ControlledCount = controlled
        };
    }

    private static string FormatZoneControl(ZoneInfluenceState state)
    {
        if (state == null || !state.HasAnyPresence) return "Libre";

        if (state.Status == ZoneControlStatus.Controlled && state.Controller.HasValue)
        {
            return "Domina: " + FactionIdUtil.DisplayName(state.Controller.Value);
        }

        FactionId? leader = LeadingFaction(state);
        if (leader.HasValue)
        {
            return "Lidera: " + FactionIdUtil.DisplayName(leader.Value) + " (en disputa)";
        }

        return "En disputa";
    }

    private static string FormatZoneIntents(DistrictZone zone)
    {
        if (zone == null || AIIntentBoard.IsNull) return "Este turno: —";

        IReadOnlyList<AIIntent> intents = AIIntentBoard.Get.Intents;
        List<string> lines = new List<string>();
        for (int i = 0; i < intents.Count; i++)
        {
            AIIntent intent = intents[i];
            if (intent == null || intent.Target != zone) continue;
            lines.Add(DescribeIntent(intent));
        }

        if (lines.Count == 0) return "Este turno: —";
        return "Este turno: " + string.Join(" · ", lines);
    }

    private static string DescribeIntent(AIIntent intent)
    {
        string who = intent.Faction.HasValue
            ? FactionIdUtil.DisplayName(intent.Faction.Value)
            : "IA";

        if (intent.Kind == AIIntentKind.PlantSeed)
        {
            string seed = string.IsNullOrEmpty(intent.Label) ? "una seed" : intent.Label;
            return who + " plantan " + seed;
        }

        string unit = intent.Amount == 1 ? "clérigo" : "clérigos";
        return who + " +" + intent.Amount + " " + unit;
    }

    private static string FormatZoneTithe(DistrictZone zone, ZoneInfluenceState state)
    {
        if (zone == null || InfluenceManager.IsNull) return "Diezmo: —";

        DistrictProductionConfig config = InfluenceManager.Get.ProductionConfig;
        if (config == null || !config.TryGetEntry(zone.District, out DistrictProductionConfig.ProductionEntry entry))
        {
            return "Diezmo: —";
        }

        if (entry.isImperial) return "Diezmo: — (imperial)";

        string resources = FormatResourceAmounts(entry);
        if (state != null && state.Status == ZoneControlStatus.Controlled && state.Controller.HasValue)
        {
            return "Diezmo: " + resources + " → " + FactionIdUtil.DisplayName(state.Controller.Value);
        }

        if (state != null && state.TotalInfluence > 0)
        {
            return "Diezmo: " + resources + " proporcional a influencia";
        }

        return "Diezmo: " + resources + " (sin dueño, se pierde)";
    }

    private static string FormatProductionLine(Districts district, bool districtControlled)
    {
        if (InfluenceManager.IsNull) return "Producción: —";

        DistrictProductionConfig config = InfluenceManager.Get.ProductionConfig;
        if (config == null || !config.TryGetEntry(district, out DistrictProductionConfig.ProductionEntry entry))
        {
            return "Producción: —";
        }

        if (entry.isImperial) return "Producción: sin diezmo (imperial)";

        string resources = FormatResourceAmounts(entry);
        if (districtControlled)
        {
            return "Producción: " + resources + " por nodo (x"
                + config.DistrictControlProductionMultiplier.ToString("0.##") + " por control)";
        }

        return "Producción: " + resources + " por nodo";
    }

    private static string FormatResourceAmounts(DistrictProductionConfig.ProductionEntry entry)
    {
        string line = entry.primaryAmountPerZone + " " + entry.primaryResource;
        if (entry.secondaryAmountPerZone > 0)
        {
            line += " + " + entry.secondaryAmountPerZone + " " + entry.secondaryResource;
        }

        return line;
    }

    private static FactionId? LeadingFaction(ZoneInfluenceState state)
    {
        if (state == null) return null;

        FactionId? best = null;
        int bestShare = 0;
        bool tie = false;

        foreach (FactionId faction in state.FactionsWithShare())
        {
            int share = state.GetShare(faction);
            if (share > bestShare)
            {
                bestShare = share;
                best = faction;
                tie = false;
            }
            else if (share == bestShare)
            {
                tie = true;
            }
        }

        return tie ? null : best;
    }

    private static FactionId? ResolveFaithEclipseLeader(
        List<MapStatsFactionRow> factions,
        out int leading,
        out bool tie)
    {
        leading = -1;
        tie = false;
        FactionId? leader = null;

        for (int i = 0; i < factions.Count; i++)
        {
            int count = factions[i].ControlledZones;
            if (count > leading)
            {
                leading = count;
                leader = factions[i].Faction;
                tie = false;
            }
            else if (count == leading)
            {
                tie = true;
            }
        }

        if (leading <= 0 || tie) return null;
        return leader;
    }

    private static Dictionary<FactionId, MapStatsFactionRow> CreateEmptyFactionRows()
    {
        Dictionary<FactionId, MapStatsFactionRow> rows = new Dictionary<FactionId, MapStatsFactionRow>();
        foreach (FactionId faction in FactionIdUtil.All)
        {
            rows[faction] = new MapStatsFactionRow
            {
                Faction = faction,
                DisplayName = FactionIdUtil.DisplayName(faction),
                Color = FactionPalette.For(faction)
            };
        }

        return rows;
    }

    private static void TryAddSeed(List<MapStatsSeedRow> seeds, DistrictZone zone)
    {
        if (zone == null || zone.PlantedSeed == null) return;
        seeds.Add(CreateSeedRow(zone, zone.PlantedSeed));
    }

    private static MapStatsSeedRow CreateSeedRow(DistrictZone zone, Seed seed)
    {
        return new MapStatsSeedRow
        {
            Seed = seed,
            Zone = zone,
            Title = seed.Title,
            District = zone.District,
            DistrictName = DistrictLabel(zone.District),
            SectorName = zone.SectorName,
            TurnsRemaining = seed.TurnsRemaining,
            EventType = seed.EventType,
            Difficulty = seed.Difficulty
        };
    }

    private static int CompareSeeds(MapStatsSeedRow a, MapStatsSeedRow b)
    {
        int district = a.District.CompareTo(b.District);
        if (district != 0) return district;
        return string.CompareOrdinal(a.SectorName, b.SectorName);
    }

    private static int CountControlledZones(List<DistrictZone> zones, FactionId faction)
    {
        int count = 0;
        for (int i = 0; i < zones.Count; i++)
        {
            ZoneInfluenceState state = GetState(zones[i]);
            if (state != null &&
                state.Status == ZoneControlStatus.Controlled &&
                state.Controller == faction)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountAssignedClerics(List<DistrictZone> zones, FactionId faction)
    {
        int count = 0;
        for (int i = 0; i < zones.Count; i++)
        {
            ZoneInfluenceState state = GetState(zones[i]);
            if (state != null) count += state.GetClerics(faction);
        }

        return count;
    }

    private static int GetClericPool(FactionId faction)
    {
        if (InfluenceManager.IsNull) return 0;
        return InfluenceManager.Get.GetClericPool(faction);
    }

    private static bool IsImperial(Districts district)
    {
        if (InfluenceManager.IsNull) return false;
        DistrictProductionConfig config = InfluenceManager.Get.ProductionConfig;
        return config != null && config.IsImperial(district);
    }

    private static ZoneInfluenceState GetState(DistrictZone zone)
    {
        if (zone == null) return null;
        zone.EnsureInfluenceState();
        return zone.Influence;
    }

    private static List<DistrictZone> GetPlayableZones()
    {
        if (!InfluenceManager.IsNull)
        {
            return new List<DistrictZone>(InfluenceManager.Get.GetPlayableZones());
        }

        DistrictZone[] found = UnityEngine.Object.FindObjectsByType<DistrictZone>(FindObjectsSortMode.None);
        List<DistrictZone> zones = new List<DistrictZone>();
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null && found[i].IsPlayable) zones.Add(found[i]);
        }

        return zones;
    }

    private static List<DistrictZone> GetDistrictZones(Districts district, List<DistrictZone> all)
    {
        if (!DistrictsManager.IsNull)
        {
            return DistrictsManager.GetDistrictZones(district);
        }

        List<DistrictZone> zones = new List<DistrictZone>();
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i] != null && all[i].District == district) zones.Add(all[i]);
        }

        return zones;
    }

    private static DistrictColorMapping ResolveColorMapping()
    {
        DistrictSelectionController controller = UnityEngine.Object.FindAnyObjectByType<DistrictSelectionController>();
        return controller != null ? controller.ColorMapping : null;
    }
}
