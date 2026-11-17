using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Orquesta el Sistema de Influencia (GDD): gen clérigos → spill → lucha ideológica → control → tithe.
/// </summary>
[DefaultExecutionOrder(-20)]
public class InfluenceManager : Singleton<InfluenceManager>
{
    [Header("Config")]
    [SerializeField] private int defaultZoneCap = ZoneInfluenceState.DefaultCap;
    [SerializeField] private int playerStartingClerics = 6;
    [SerializeField] private DistrictProductionConfig productionConfig;
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private float adjacencyPadding = 0.35f;

    [Header("Zone Struggle")]
    [Tooltip("Multiplies the influence that changes hands each turn in a full, contested zone. Higher flips zones faster.")]
    [SerializeField, Min(0f)] private float struggleSwing = 2f;
    [Tooltip("Roll multiplier for a sect pushing into a zone another sect rules.")]
    [SerializeField, Min(0f)] private float attackerRollMultiplier = 1.5f;
    [Tooltip("Extra roll for each cleric a sect has over the strongest rival in the zone.")]
    [SerializeField, Min(0)] private int outnumberBonusPerCleric = 2;

    [Header("Bliss")]
    [Tooltip("Extra Wealth your zones yield at 100% Bliss. It scales down to nothing at 0%.")]
    [SerializeField, Range(0f, 1f)] private float blissTitheBonus = 0.25f;
    [SerializeField] private bool logTurnSummary;

    private readonly Dictionary<FactionId, FactionStock> stocks = new Dictionary<FactionId, FactionStock>();
    private readonly Dictionary<FactionId, PlayerStats> rivalStats = new Dictionary<FactionId, PlayerStats>();
    private readonly ZoneAdjacencyGraph adjacency = new ZoneAdjacencyGraph();
    private readonly List<DistrictZone> cachedZones = new List<DistrictZone>();
    private readonly Dictionary<Districts, FactionId?> districtControllers = new Dictionary<Districts, FactionId?>();
    private readonly List<IZoneModifier> modifiers = new List<IZoneModifier>();
    private readonly List<DistrictZone> reachBuffer = new List<DistrictZone>();

    private bool graphBuilt;
    private ZoneTravelGraph travel;

    public DistrictProductionConfig ProductionConfig => productionConfig;
    public PlayerStats Stats => playerStats;
    public ZoneAdjacencyGraph Adjacency => adjacency;

    public static InfluenceManager Get => Instance;

    public event Action OnInfluenceTurnResolved;
    public event Action OnControlChanged;

    public void Configure(DistrictProductionConfig config, PlayerStats stats)
    {
        if (config != null) productionConfig = config;
        if (stats != null) playerStats = stats;
        if (productionConfig == null)
        {
            productionConfig = ScriptableObject.CreateInstance<DistrictProductionConfig>();
        }
    }

    protected override void OnInitialization()
    {
        EnsureStocks();
        if (productionConfig == null)
        {
            productionConfig = ScriptableObject.CreateInstance<DistrictProductionConfig>();
        }
    }

    private void Start()
    {
        EnsureStocks();
        RefreshZoneCache();
        EnsureZonesInitialized();
        RebuildAdjacency();
        GameTime.OnTurnEnded += OnTurnEnded;
    }

    private void OnDestroy()
    {
        GameTime.OnTurnEnded -= OnTurnEnded;
    }

    private void EnsureStocks()
    {
        if (!stocks.ContainsKey(FactionId.Player))
        {
            stocks[FactionId.Player] = new FactionStock(FactionId.Player, playerStartingClerics);
        }

        for (int i = 1; i <= 4; i++)
        {
            FactionId id = (FactionId)i;
            if (!stocks.ContainsKey(id))
            {
                stocks[id] = new FactionStock(id, playerStartingClerics);
            }
        }
    }

    public FactionStock GetStock(FactionId faction)
    {
        EnsureStocks();
        return stocks[faction];
    }

    public int GetClericPool(FactionId faction) => GetStock(faction).Clerics;

    public int CountDeployedClerics(FactionId faction)
    {
        int clerics = 0;
        IReadOnlyList<DistrictZone> zones = GetPlayableZones();
        for (int i = 0; i < zones.Count; i++)
        {
            if (zones[i].Influence != null) clerics += zones[i].Influence.GetClerics(faction);
        }

        return clerics;
    }

    public void SetRivalStats(FactionId faction, PlayerStats stats)
    {
        if (FactionIdUtil.IsPlayer(faction) || stats == null) return;
        rivalStats[faction] = stats;
    }

    public void RegisterModifier(IZoneModifier modifier)
    {
        if (modifier == null || modifiers.Contains(modifier)) return;
        modifiers.Add(modifier);
    }

    public void RefreshZoneCache()
    {
        cachedZones.Clear();
        DistrictZone[] found = FindObjectsByType<DistrictZone>(FindObjectsSortMode.None);
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null && found[i].IsPlayable)
            {
                cachedZones.Add(found[i]);
            }
        }
    }

    public IReadOnlyList<DistrictZone> GetPlayableZones()
    {
        if (cachedZones.Count == 0) RefreshZoneCache();
        return cachedZones;
    }

    public void RebuildAdjacency()
    {
        RefreshZoneCache();
        adjacency.Rebuild(cachedZones, adjacencyPadding);
        graphBuilt = true;
    }

    public void EnsureZonesInitialized()
    {
        IReadOnlyList<DistrictZone> zones = GetPlayableZones();
        for (int i = 0; i < zones.Count; i++)
        {
            DistrictZone zone = zones[i];
            if (zone.Influence == null)
            {
                zone.EnsureInfluenceState(defaultZoneCap);
            }
            else
            {
                zone.Influence.Cap = defaultZoneCap;
            }

            zone.EnsureControlMarker();
            zone.RefreshControlVisual();
        }
    }

    public bool TryAssignClerics(DistrictZone zone, FactionId faction, int amount, out string error)
    {
        error = null;
        if (zone == null || !zone.IsPlayable)
        {
            error = "That zone can't be used.";
            return false;
        }

        if (amount == 0) return true;

        zone.EnsureInfluenceState(defaultZoneCap);
        ZoneInfluenceState state = zone.Influence;

        if (amount > 0)
        {
            if (!state.CanEnterByNormalMeans(faction))
            {
                error = "This zone is full and your faith has no foothold here.";
                return false;
            }

            if (FactionIdUtil.IsPlayer(faction) && !InClericReach(zone, faction))
            {
                error = "Too far. Clerics can only go to zones next to one you rule.";
                return false;
            }

            FactionStock stock = GetStock(faction);
            if (!stock.TrySpendClerics(amount))
            {
                error = "You have no clerics in reserve.";
                return false;
            }

            state.AddClerics(faction, amount);
        }
        else
        {
            int remove = -amount;
            int assigned = state.GetClerics(faction);
            int actual = Mathf.Min(remove, assigned);
            if (actual <= 0)
            {
                error = "You have no clerics here to withdraw.";
                return false;
            }

            state.AddClerics(faction, -actual);
            GetStock(faction).ReturnClerics(actual);
        }

        zone.RefreshControlVisual();
        OnControlChanged?.Invoke();
        return true;
    }

    public bool InClericReach(DistrictZone zone, FactionId faction)
    {
        if (zone == null || !zone.IsPlayable) return false;

        zone.EnsureInfluenceState(defaultZoneCap);
        if (zone.Influence.GetShare(faction) > 0 || zone.Influence.GetClerics(faction) > 0) return true;
        if (!RulesAnyZone(faction)) return true;

        if (travel == null) travel = FindAnyObjectByType<ZoneTravelGraph>();
        if (travel == null) return true;

        travel.CollectNeighbors(zone, reachBuffer);
        for (int i = 0; i < reachBuffer.Count; i++)
        {
            ZoneInfluenceState neighbor = reachBuffer[i].Influence;
            if (neighbor != null && neighbor.Status == ZoneControlStatus.Controlled && neighbor.Controller == faction) return true;
        }

        return false;
    }

    private bool RulesAnyZone(FactionId faction)
    {
        IReadOnlyList<DistrictZone> zones = GetPlayableZones();
        for (int i = 0; i < zones.Count; i++)
        {
            ZoneInfluenceState state = zones[i] != null ? zones[i].Influence : null;
            if (state != null && state.Status == ZoneControlStatus.Controlled && state.Controller == faction) return true;
        }

        return false;
    }

    public bool TryAssignClerics(DistrictZone zone, FactionId faction, int amount)
    {
        return TryAssignClerics(zone, faction, amount, out _);
    }

    public void ApplyEventInfluence(DistrictZone zone, FactionId faction, int delta, string source = null)
    {
        if (zone == null) return;
        zone.EnsureInfluenceState(defaultZoneCap);
        string from = string.IsNullOrWhiteSpace(source) ? "an event" : $"the event \"{source}\"";
        if (FactionIdUtil.IsPlayer(faction) && delta < 0)
            zone.Influence.NotePlayerSetback($"{from} cost you {-delta} influence");
        else if (!FactionIdUtil.IsPlayer(faction) && delta > 0 && zone.Influence.GetShare(FactionId.Player) > 0)
            zone.Influence.NotePlayerSetback($"{from} gave {FactionIdUtil.DisplayName(faction)} {delta} influence");
        zone.Influence.ApplyDirectInfluenceDelta(faction, delta);
        zone.RefreshControlVisual();
        OnControlChanged?.Invoke();
    }

    private void OnTurnEnded()
    {
        if (!graphBuilt) RebuildAdjacency();
        EnsureZonesInitialized();

        ResolveInfluenceTurn();
        ResolveDistrictControl();
        ResolveDistrictTithe();

        OnInfluenceTurnResolved?.Invoke();
        OnControlChanged?.Invoke();
    }

    private void ResolveInfluenceTurn()
    {
        IReadOnlyList<DistrictZone> zones = GetPlayableZones();
        var pendingSpill = new List<(DistrictZone source, FactionId faction, int excess)>();
        var generationsByZone = new Dictionary<DistrictZone, Dictionary<FactionId, int>>();

        for (int i = 0; i < zones.Count; i++)
        {
            DistrictZone zone = zones[i];
            ZoneInfluenceState state = zone.Influence;
            if (state == null) continue;

            Dictionary<FactionId, int> generation = ComputeGeneration(zone, state);
            generationsByZone[zone] = generation;

            Dictionary<FactionId, int> excess = new Dictionary<FactionId, int>();
            foreach (KeyValuePair<FactionId, int> pair in generation)
            {
                int gen = pair.Value;
                if (gen <= 0) continue;

                int added = state.TryAddShareClamped(pair.Key, gen);
                int leftover = gen - added;
                if (leftover > 0) excess[pair.Key] = leftover;
            }

            foreach (KeyValuePair<FactionId, int> pair in excess)
            {
                if (!state.HasAbsoluteControl(pair.Key)) continue;
                if (pair.Value <= 0) continue;
                pendingSpill.Add((zone, pair.Key, pair.Value));
            }
        }

        ApplyPendingSpill(pendingSpill);

        for (int i = 0; i < zones.Count; i++)
        {
            DistrictZone zone = zones[i];
            ZoneInfluenceState state = zone.Influence;
            if (state == null) continue;

            generationsByZone.TryGetValue(zone, out Dictionary<FactionId, int> generation);
            if (generation == null) generation = new Dictionary<FactionId, int>();

            if (state.IsAtCap)
            {
                ApplyIdeologicalStruggle(state, generation);
            }

            state.ClearExpelFlagsIfNotAtCap();
            state.RecalculateControl();
            zone.RefreshControlVisual();
        }

        if (logTurnSummary)
        {
            Debug.Log($"InfluenceManager: turn resolved on {zones.Count} zone(s).", this);
        }
    }

    private Dictionary<FactionId, int> ComputeGeneration(DistrictZone zone, ZoneInfluenceState state)
    {
        var generation = new Dictionary<FactionId, int>();

        foreach (FactionId faction in FactionIdUtil.All)
        {
            int clerics = state.GetClerics(faction);
            if (clerics <= 0) continue;

            int roll = ClericInfluenceTable.Roll(clerics) + OutnumberBonus(state, faction, clerics, outnumberBonusPerCleric);
            float mult = GetInfluenceGenerationMultiplier(faction, zone) * AttackMultiplier(state, faction);
            int gen = Mathf.Max(0, Mathf.RoundToInt(roll * mult));
            generation[faction] = gen;
        }

        return generation;
    }

    public int OutnumberBonusPerCleric => outnumberBonusPerCleric;
    public float StruggleSwing => struggleSwing;
    public float BlissTitheMultiplier => 1f + blissTitheBonus * Mathf.Clamp01((ResourceManager.IsNull ? 0 : ResourceManager.Resources.GetResourceAmount(Resource.Happiness)) / 100f);

    public float AttackMultiplier(ZoneInfluenceState state, FactionId faction)
    {
        if (state == null || state.Status != ZoneControlStatus.Controlled || !state.Controller.HasValue) return 1f;
        return state.Controller.Value == faction ? 1f : attackerRollMultiplier;
    }

    public static int OutnumberBonus(ZoneInfluenceState state, FactionId faction, int clerics, int perCleric = 1)
    {
        if (state == null || clerics <= 0) return 0;
        int strongest = 0;
        foreach (FactionId other in FactionIdUtil.All)
        {
            if (other != faction) strongest = Mathf.Max(strongest, state.GetClerics(other));
        }

        return strongest > 0 ? Mathf.Max(0, clerics - strongest) * perCleric : 0;
    }

    public float GetInfluenceGenerationMultiplier(FactionId faction, DistrictZone zone)
    {
        float mult = 1f;

        if (productionConfig != null &&
            productionConfig.TryGetEntry(zone.District, out DistrictProductionConfig.ProductionEntry entry) &&
            !entry.isImperial &&
            entry.influenceStat != PlayerStats.PlayerStat.None)
        {
            int effectiveStat = 10 + GetFactionStat(faction, entry.influenceStat);
            mult *= 1f + (effectiveStat - 10) * productionConfig.InfluenceStatScalePerPoint;
        }

        for (int i = 0; i < modifiers.Count; i++)
        {
            mult *= modifiers[i].GetInfluenceGenerationMultiplier(faction, zone);
        }

        return Mathf.Max(0f, mult);
    }

    private int GetFactionStat(FactionId faction, PlayerStats.PlayerStat stat)
    {
        if (FactionIdUtil.IsPlayer(faction))
        {
            if (playerStats == null) return 0;
            return playerStats.GetStat(stat);
        }

        if (rivalStats.TryGetValue(faction, out PlayerStats stats) && stats != null)
        {
            return stats.GetStat(stat);
        }

        return 0;
    }

    private void ApplyIdeologicalStruggle(
        ZoneInfluenceState state,
        Dictionary<FactionId, int> generation)
    {
        List<FactionId> present = new List<FactionId>();
        foreach (FactionId faction in state.FactionsWithShare())
        {
            present.Add(faction);
        }

        foreach (FactionId faction in generation.Keys)
        {
            if (!present.Contains(faction)) present.Add(faction);
        }

        if (present.Count < 2) return;

        int peak = 0;
        for (int i = 0; i < present.Count; i++)
        {
            generation.TryGetValue(present[i], out int gen);
            if (gen > peak) peak = gen;
        }

        if (peak <= 0) return;

        List<FactionId> winners = new List<FactionId>();
        List<FactionId> toExpel = new List<FactionId>();
        int pool = 0;
        for (int i = 0; i < present.Count; i++)
        {
            FactionId faction = present[i];
            generation.TryGetValue(faction, out int gen);
            if (gen >= peak)
            {
                winners.Add(faction);
                continue;
            }

            int had = state.GetShare(faction);
            int loss = Mathf.Min(Mathf.RoundToInt((peak - gen) * struggleSwing), had);
            if (loss <= 0) continue;

            state.AddShare(faction, -loss);
            pool += loss;
            if (FactionIdUtil.IsPlayer(faction)) state.NotePlayerSetback(StruggleCause(state, loss, generation, peak));
            if (state.GetShare(faction) <= 0) toExpel.Add(faction);
        }

        for (int i = 0; i < winners.Count && pool > 0; i++)
        {
            int part = pool / winners.Count + (i < pool % winners.Count ? 1 : 0);
            state.AddShare(winners[i], part);
        }

        for (int i = 0; i < toExpel.Count; i++)
        {
            int returned = state.Expel(toExpel[i], returnClerics: true);
            if (returned > 0) GetStock(toExpel[i]).ReturnClerics(returned);
        }
    }

    private static string StruggleCause(ZoneInfluenceState state, int loss, Dictionary<FactionId, int> generation, int peak)
    {
        FactionId? top = null;
        foreach (KeyValuePair<FactionId, int> pair in generation)
        {
            if (pair.Value >= peak && !FactionIdUtil.IsPlayer(pair.Key)) top = pair.Key;
        }

        if (!top.HasValue) return $"you lost {loss} influence in the struggle for the zone";
        return $"{FactionIdUtil.DisplayName(top.Value)} won the struggle and took {loss} influence ({state.GetClerics(top.Value)} of their clerics against {state.GetClerics(FactionId.Player)} of yours)";
    }

    private void ApplyPendingSpill(List<(DistrictZone source, FactionId faction, int excess)> pendingSpill)
    {
        for (int i = 0; i < pendingSpill.Count; i++)
        {
            DistrictZone source = pendingSpill[i].source;
            FactionId faction = pendingSpill[i].faction;
            int excess = pendingSpill[i].excess;
            if (excess <= 0 || source == null) continue;

            IReadOnlyList<DistrictZone> neighbors = adjacency.GetNeighbors(source);
            if (neighbors.Count == 0) continue;

            int perNeighbor = excess / neighbors.Count;
            if (perNeighbor <= 0) continue;

            for (int n = 0; n < neighbors.Count; n++)
            {
                DistrictZone neighbor = neighbors[n];
                if (neighbor == null || neighbor.Influence == null) continue;
                if (neighbor.Influence.IsAtCap) continue; // spill lost

                neighbor.Influence.TryAddShareClamped(faction, perNeighbor);
            }
        }
    }

    private void ResolveDistrictControl()
    {
        districtControllers.Clear();

        foreach (Districts district in Enum.GetValues(typeof(Districts)))
        {
            if (productionConfig != null && productionConfig.IsImperial(district))
            {
                districtControllers[district] = null;
                continue;
            }

            List<DistrictZone> zones = DistrictsManager.GetDistrictZones(district);
            int playable = 0;
            var controlledCounts = new Dictionary<FactionId, int>();

            for (int i = 0; i < zones.Count; i++)
            {
                DistrictZone zone = zones[i];
                if (zone == null || !zone.IsPlayable || zone.Influence == null) continue;
                playable++;

                if (zone.Influence.Status != ZoneControlStatus.Controlled || !zone.Influence.Controller.HasValue)
                {
                    continue;
                }

                FactionId controller = zone.Influence.Controller.Value;
                controlledCounts.TryGetValue(controller, out int count);
                controlledCounts[controller] = count + 1;
            }

            FactionId? districtOwner = null;
            if (playable > 0)
            {
                foreach (KeyValuePair<FactionId, int> pair in controlledCounts)
                {
                    // More than half of the district's zones.
                    if (pair.Value * 2 > playable)
                    {
                        districtOwner = pair.Key;
                        break;
                    }
                }
            }

            districtControllers[district] = districtOwner;
        }
    }

    public bool IsDistrictControlledBy(Districts district, FactionId faction)
    {
        return districtControllers.TryGetValue(district, out FactionId? owner) && owner == faction;
    }

    public FactionId? GetDistrictController(Districts district)
    {
        return districtControllers.TryGetValue(district, out FactionId? owner) ? owner : null;
    }

    /// <summary>Technical flag for special events when a district is Controlled.</summary>
    public bool DistrictSpecialEventsUnlocked(Districts district)
    {
        return GetDistrictController(district).HasValue;
    }

    private void ResolveDistrictTithe()
    {
        if (productionConfig == null) return;

        IReadOnlyList<DistrictZone> zones = GetPlayableZones();
        for (int i = 0; i < zones.Count; i++)
        {
            DistrictZone zone = zones[i];
            if (!productionConfig.TryGetEntry(zone.District, out DistrictProductionConfig.ProductionEntry entry))
            {
                continue;
            }

            if (entry.isImperial) continue;

            if (entry.usesPerZoneAmount)
            {
                List<DistrictProductionConfig.ScaledYield> yields = new List<DistrictProductionConfig.ScaledYield>();
                DistrictProductionConfig.ScaleYields(entry, zone.ProductionAmount, yields);
                for (int y = 0; y < yields.Count; y++)
                {
                    DistributeProductionByShare(zone, yields[y].resource, yields[y].amount);
                }

                continue;
            }

            float districtMult = 1f;
            FactionId? districtOwner = GetDistrictController(zone.District);
            // Multiplier applies to production of zones in a controlled district (GDD).
            if (districtOwner.HasValue)
            {
                districtMult = productionConfig.DistrictControlProductionMultiplier;
            }

            DistributeZoneProduction(zone, entry.primaryResource, ScaleAmount(entry.primaryAmountPerZone, districtMult));
            if (entry.secondaryAmountPerZone > 0)
            {
                DistributeZoneProduction(zone, entry.secondaryResource, ScaleAmount(entry.secondaryAmountPerZone, districtMult));
            }
        }
    }

    private static int ScaleAmount(int baseAmount, float mult)
    {
        return Mathf.Max(0, Mathf.FloorToInt(baseAmount * mult));
    }

    /// <summary>
    /// Cada facción recibe floor(producción * su influencia / total). El resto se pierde.
    /// </summary>
    private void DistributeProductionByShare(DistrictZone zone, Resource resource, int production)
    {
        if (production <= 0 || zone.Influence == null) return;

        ZoneInfluenceState state = zone.Influence;
        int total = state.TotalInfluence;
        if (total <= 0) return;

        foreach (FactionId faction in state.FactionsWithShare())
        {
            int amount = DistrictProductionConfig.FloorShare(production, state.GetShare(faction), total);
            if (amount > 0) GrantResource(faction, resource, amount);
        }
    }

    private void DistributeZoneProduction(DistrictZone zone, Resource resource, int baseAmount)
    {
        if (baseAmount <= 0 || zone.Influence == null) return;

        ZoneInfluenceState state = zone.Influence;
        int total = state.TotalInfluence;

        if (state.Status == ZoneControlStatus.Controlled && state.Controller.HasValue)
        {
            GrantResource(state.Controller.Value, resource, baseAmount);
            return;
        }

        // Contested: proportional to share percent; unowned remainder is lost.
        if (total <= 0) return;

        foreach (FactionId faction in state.FactionsWithShare())
        {
            int share = state.GetShare(faction);
            int amount = Mathf.FloorToInt(baseAmount * (share / (float)total));
            if (amount > 0) GrantResource(faction, resource, amount);
        }
    }

    private void GrantResource(FactionId faction, Resource resource, int amount)
    {
        if (amount <= 0) return;

        if (FactionIdUtil.IsPlayer(faction))
        {
            if (!ResourceManager.IsNull)
            {
                if (resource == Resource.Wealth) amount = Mathf.RoundToInt(amount * BlissTitheMultiplier);
                ResourceManager.Resources.AddResource(resource, amount);
            }
            else
            {
                GetStock(faction).AddResource(resource, amount);
            }

            return;
        }

        GetStock(faction).AddResource(resource, amount);
    }

    public int CountControlledZones(FactionId faction)
    {
        int count = 0;
        IReadOnlyList<DistrictZone> zones = GetPlayableZones();
        for (int i = 0; i < zones.Count; i++)
        {
            ZoneInfluenceState state = zones[i].Influence;
            if (state != null &&
                state.Status == ZoneControlStatus.Controlled &&
                state.Controller == faction)
            {
                count++;
            }
        }

        return count;
    }

    public FactionId? GetFaithEclipseLeader(out int leadingZones)
    {
        leadingZones = -1;
        FactionId? leader = null;
        bool tie = false;

        foreach (FactionId faction in FactionIdUtil.All)
        {
            int count = CountControlledZones(faction);
            if (count > leadingZones)
            {
                leadingZones = count;
                leader = faction;
                tie = false;
            }
            else if (count == leadingZones)
            {
                tie = true;
            }
        }

        if (leadingZones <= 0 || tie) return null;
        return leader;
    }
}
