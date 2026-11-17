using System;
using System.Collections.Generic;
using UnityEngine;

public enum RivalDifficulty
{
    Easy,
    Normal,
    Hard
}

[DefaultExecutionOrder(45)]
public class RivalAIDirector : MonoBehaviour
{
    [Serializable]
    public class Agent
    {
        public FactionId faction = FactionId.Rival1;
        public AIPersonality personality = AIPersonality.Opportunist;
    }

    private struct Choice
    {
        public DistrictZone zone;
        public AIIntentKind kind;
        public Seed seed;
        public int weight;
        public int cost;
        public bool slipping;
        public bool secured;
        public bool hunt;
        public int defenders;
        public int opposition;
        public bool full;
        public float pressure;
        public float personality;
    }

    [SerializeField] private List<Agent> agents = new List<Agent>
    {
        new Agent { faction = FactionId.Rival1 },
        new Agent { faction = FactionId.Rival2 },
        new Agent { faction = FactionId.Rival3 }
    };

    [Tooltip("Presupuesto normal. Easy resta 2 (mínimo 1). Hard suma 2.")]
    [Min(0)]
    [SerializeField] private int normalActionPoints = 4;

    [Tooltip("0 reparte la presión. 1 persigue al jugador. Easy usa cerca de 0.1 y Hard cerca de 0.85.")]
    [Range(0f, 1f)]
    [SerializeField] private float normalAggressiveness = 0.35f;

    [Range(0f, 1f)]
    [SerializeField] private float easyMistakeChance = 0.55f;

    [Range(0f, 1f)]
    [SerializeField] private float normalMistakeChance = 0.1f;

    [Range(0f, 1f)]
    [SerializeField] private float hardMistakeChance = 0f;

    [SerializeField] private bool logPlans;

    [Serializable]
    private struct Pace
    {
        [Tooltip("Events each rival plants per turn. 0.5 means one every other turn.")]
        [Min(0f)] public float seedsPerTurn;
        [Tooltip("Most clerics a rival sends out in one turn.")]
        [Min(0)] public int clericsSentPerTurn;
        [Tooltip("Clerics a rival gains per turn. 1.5 means one, then two, then one...")]
        [Min(0f)] public float clericsPerTurn;
        [Tooltip("Clerics kept back to defend zones the rival is losing.")]
        [Min(0)] public int clericReserve;
    }

    [Header("Events")]
    [Tooltip("Most events all rivals together plant in one turn.")]
    [SerializeField, Min(0)] private int maxEventsPerTurn = 2;
    [Tooltip("Chance that no rival plants an event this turn.")]
    [SerializeField, Range(0f, 1f)] private float quietTurnChance = 0.25f;
    [Tooltip("How many recent events stay out of the pool so the same ones don't come back too soon.")]
    [SerializeField, Min(0)] private int recentEventMemory = 8;

    [Header("Personalities")]
    [Tooltip("Most clerics a Cautious rival keeps in one zone, even while defending it.")]
    [SerializeField, Min(1)] private int cautiousClericCap = 5;

    [Header("Pace")]
    [SerializeField] private Pace easyPace = new Pace { seedsPerTurn = 0.5f, clericsSentPerTurn = 2, clericsPerTurn = 0.5f, clericReserve = 2 };
    [SerializeField] private Pace normalPace = new Pace { seedsPerTurn = 1f, clericsSentPerTurn = 3, clericsPerTurn = 1.5f, clericReserve = 2 };
    [SerializeField] private Pace hardPace = new Pace { seedsPerTurn = 2f, clericsSentPerTurn = 4, clericsPerTurn = 2f, clericReserve = 1 };

    private struct Tuning
    {
        public int actions;
        public float aggression;
        public float mistakes;
        public Pace pace;
    }

    private int actionPoints = 4;
    private float aggressiveness = 0.35f;
    private float mistakeChance = 0.1f;
    private Pace pace;
    private bool difficultyApplied;
    private bool usePerRival;
    private readonly Dictionary<FactionId, Tuning> perRival = new Dictionary<FactionId, Tuning>();

    private readonly HashSet<string> seedTitlesTaken = new HashSet<string>();
    private readonly List<string> recentEvents = new List<string>();
    private readonly List<Agent> turnOrder = new List<Agent>();
    private int eventsLeftThisTurn;
    private readonly HashSet<Districts> playerDistrictThreats = new HashSet<Districts>();
    private readonly List<DistrictZone> plannedSeedZones = new List<DistrictZone>();
    private readonly List<Choice> plannedSeeds = new List<Choice>();
    private readonly Dictionary<DistrictZone, int> plannedClerics = new Dictionary<DistrictZone, int>();
    private readonly Dictionary<FactionId, HashSet<DistrictZone>> controlledMemory =
        new Dictionary<FactionId, HashSet<DistrictZone>>();

    public int ActionsPerTurn => TutorialActionBudget(actionPoints);
    public float Aggressiveness => aggressiveness;

    private void Awake()
    {
        ShufflePersonalities();
        if (difficultyApplied) return;
        if (MatchSettings.UseCustom) ApplyCustomFromMatch();
        else ApplyDifficulty(MatchSettings.Shared);
    }

    private void ShufflePersonalities()
    {
        List<AIPersonality> pool = new List<AIPersonality>();
        for (int i = 0; i < agents.Count; i++)
        {
            if (agents[i] == null) continue;
            if (pool.Count == 0) pool.AddRange((AIPersonality[])Enum.GetValues(typeof(AIPersonality)));
            int pick = UnityEngine.Random.Range(0, pool.Count);
            agents[i].personality = pool[pick];
            pool.RemoveAt(pick);
        }
    }

    public bool TryGetPersonality(FactionId faction, out AIPersonality personality)
    {
        for (int i = 0; i < agents.Count; i++)
        {
            Agent agent = agents[i];
            if (agent == null || agent.faction != faction) continue;
            personality = agent.personality;
            return true;
        }

        personality = default;
        return false;
    }

    public static bool IsActiveRival(FactionId faction)
    {
        RivalAIDirector director = FindAnyObjectByType<RivalAIDirector>();
        if (director == null)
        {
            return faction == FactionId.Rival1 || faction == FactionId.Rival2 || faction == FactionId.Rival3;
        }

        return director.TryGetPersonality(faction, out _);
    }

    public string DescribeBehavior(FactionId faction)
    {
        if (!TryGetPersonality(faction, out AIPersonality personality)) return string.Empty;

        string style = PersonalityLabel(personality);
        AIInfluenceController clerics = FindAnyObjectByType<AIInfluenceController>();
        IReadOnlyList<AIObjective> objectives = clerics != null ? clerics.GetObjectives(faction) : null;
        if (objectives == null || objectives.Count == 0) return style;

        List<AIObjective> ranked = new List<AIObjective>();
        for (int i = 0; i < objectives.Count; i++)
        {
            if (objectives[i] != null) ranked.Add(objectives[i]);
        }

        ranked.Sort((a, b) =>
        {
            int byWeight = b.weight.CompareTo(a.weight);
            return byWeight != 0 ? byWeight : a.district.CompareTo(b.district);
        });

        string text = style + " · ";
        for (int i = 0; i < ranked.Count; i++)
        {
            if (i > 0) text += ", ";
            text += MapStatsQuery.DistrictLabel(ranked[i].district) + " " + ranked[i].weight;
        }

        return text;
    }

    public static string PersonalityLabel(AIPersonality personality)
    {
        switch (personality)
        {
            case AIPersonality.Aggressive: return "Aggressive";
            case AIPersonality.Cautious: return "Cautious";
            default: return "Opportunist";
        }
    }

    public void ApplyDifficulty(RivalDifficulty difficulty)
    {
        usePerRival = false;
        perRival.Clear();
        Tuning tuning = Resolve(difficulty);
        SetDifficulty(tuning.actions, tuning.aggression, tuning.mistakes);
        pace = tuning.pace;
    }

    public int ClericIncomeAt(FactionId faction, int turn) => PerTurn(TuningFor(faction).pace.clericsPerTurn, turn);

    public void ApplyDifficulty(int actions, float aggression)
    {
        usePerRival = false;
        perRival.Clear();
        float mistakes = normalMistakeChance;
        pace = normalPace;
        if (aggression <= 0.2f)
        {
            mistakes = easyMistakeChance;
            pace = easyPace;
        }
        else if (aggression >= 0.7f)
        {
            mistakes = hardMistakeChance;
            pace = hardPace;
        }

        SetDifficulty(actions, aggression, mistakes);
    }

    public void PlanIntents(List<AIIntent> intents)
    {
        if (intents == null || InfluenceManager.IsNull) return;

        seedTitlesTaken.Clear();
        SeedEventManager.CollectPendingTitles(seedTitlesTaken);
        IReadOnlyList<DistrictZone> zones = InfluenceManager.Get.GetPlayableZones();
        var zonesPerDistrict = new Dictionary<Districts, int>();
        var playerPerDistrict = new Dictionary<Districts, int>();
        for (int i = 0; i < zones.Count; i++)
        {
            DistrictZone zone = zones[i];
            if (zone == null) continue;
            if (zone.PlantedSeed != null) seedTitlesTaken.Add(zone.PlantedSeed.Title);
            zonesPerDistrict.TryGetValue(zone.District, out int total);
            zonesPerDistrict[zone.District] = total + 1;
            if (zone.Influence != null && zone.Influence.Status == ZoneControlStatus.Controlled && zone.Influence.Controller == FactionId.Player)
            {
                playerPerDistrict.TryGetValue(zone.District, out int held);
                playerPerDistrict[zone.District] = held + 1;
            }
        }

        playerDistrictThreats.Clear();
        foreach (KeyValuePair<Districts, int> pair in playerPerDistrict)
        {
            if (pair.Value >= 2 && pair.Value * 2 >= zonesPerDistrict[pair.Key]) playerDistrictThreats.Add(pair.Key);
        }

        for (int i = 0; i < recentEvents.Count; i++) seedTitlesTaken.Add(recentEvents[i]);
        eventsLeftThisTurn = UnityEngine.Random.value < quietTurnChance ? 0 : UnityEngine.Random.Range(1, maxEventsPerTurn + 1);

        turnOrder.Clear();
        turnOrder.AddRange(agents);
        for (int i = turnOrder.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (turnOrder[i], turnOrder[j]) = (turnOrder[j], turnOrder[i]);
        }

        for (int i = 0; i < turnOrder.Count; i++)
        {
            Agent agent = turnOrder[i];
            if (agent == null) continue;
            PlanAgent(agent, intents);
        }
    }

    private void RememberEvent(string title)
    {
        if (recentEventMemory <= 0 || string.IsNullOrEmpty(title)) return;
        recentEvents.Remove(title);
        recentEvents.Add(title);
        while (recentEvents.Count > recentEventMemory) recentEvents.RemoveAt(0);
    }

    private void SetDifficulty(int actions, float aggression, float mistakes)
    {
        difficultyApplied = true;
        actionPoints = Mathf.Max(0, actions);
        aggressiveness = Mathf.Clamp01(aggression);
        mistakeChance = Mathf.Clamp01(mistakes);
    }

    private void ApplyCustomFromMatch()
    {
        perRival.Clear();
        FactionId[] rivals = MatchSettings.Rivals;
        for (int i = 0; i < rivals.Length; i++)
            perRival[rivals[i]] = Resolve(MatchSettings.DifficultyFor(rivals[i]));

        usePerRival = true;
        Tuning baseline = Resolve(RivalDifficulty.Normal);
        actionPoints = baseline.actions;
        aggressiveness = baseline.aggression;
        mistakeChance = baseline.mistakes;
        pace = baseline.pace;
        difficultyApplied = true;
    }

    private Tuning Resolve(RivalDifficulty difficulty)
    {
        switch (difficulty)
        {
            case RivalDifficulty.Easy:
                return new Tuning
                {
                    actions = Mathf.Max(1, normalActionPoints - 2),
                    aggression = 0.1f,
                    mistakes = easyMistakeChance,
                    pace = easyPace
                };
            case RivalDifficulty.Hard:
                return new Tuning
                {
                    actions = normalActionPoints + 2,
                    aggression = 0.85f,
                    mistakes = hardMistakeChance,
                    pace = hardPace
                };
            default:
                return new Tuning
                {
                    actions = normalActionPoints,
                    aggression = normalAggressiveness,
                    mistakes = normalMistakeChance,
                    pace = normalPace
                };
        }
    }

    private Tuning TuningFor(FactionId faction)
    {
        Tuning tuning = usePerRival && perRival.TryGetValue(faction, out Tuning stored)
            ? stored
            : new Tuning { actions = actionPoints, aggression = aggressiveness, mistakes = mistakeChance, pace = pace };

        tuning.actions = TutorialActionBudget(tuning.actions);
        return tuning;
    }

    private static int SeedsThisTurn(float perTurn) => PerTurn(perTurn, GameTime.CurrentTurn);

    private static int PerTurn(float perTurn, int turn)
    {
        if (perTurn <= 0f) return 0;
        turn = Mathf.Max(1, turn);
        return Mathf.FloorToInt(turn * perTurn + 0.0001f) - Mathf.FloorToInt((turn - 1) * perTurn + 0.0001f);
    }

    private static int TutorialActionBudget(int actions)
    {
        if (!TutorialSession.SuppressAmbientEvents || actions <= 0) return actions;
        return Mathf.Max(1, actions / 2);
    }

    private void PlanAgent(Agent agent, List<AIIntent> intents)
    {
        HashSet<DistrictZone> slipping = SlippingZones(agent.faction);
        Tuning tuning = TuningFor(agent.faction);
        if (tuning.actions <= 0) return;

        ZoneTravelGraph travel = Travel();
        Dictionary<DistrictZone, int> costs = new Dictionary<DistrictZone, int>();
        travel.FillActionCosts(agent.faction, costs);

        AIInfluenceController influence = FindAnyObjectByType<AIInfluenceController>();
        DebugAI seeds = FindAnyObjectByType<DebugAI>();
        int clericsLeft = influence != null ? influence.ProjectedClericBudget(agent.faction) : 0;

        plannedClerics.Clear();
        plannedSeedZones.Clear();
        plannedSeeds.Clear();

        int points = tuning.actions;
        int seedCap = SeedsThisTurn(tuning.pace.seedsPerTurn);
        int sent = 0;
        string name = FactionIdUtil.DisplayName(agent.faction);
        while (points > 0)
        {
            int seedsAllowed = Mathf.Min(seedCap - plannedSeeds.Count, eventsLeftThisTurn);
            int sendsAllowed = tuning.pace.clericsSentPerTurn - sent;
            if (!TryPick(agent, influence, seeds, costs, slipping, points, clericsLeft, seedsAllowed, sendsAllowed, tuning, out Choice choice, out bool misplayed))
            {
                break;
            }

            points -= choice.cost;
            if (choice.kind == AIIntentKind.AssignClerics)
            {
                clericsLeft--;
                sent++;
                plannedClerics.TryGetValue(choice.zone, out int amount);
                plannedClerics[choice.zone] = amount + 1;
            }
            else
            {
                plannedSeedZones.Add(choice.zone);
                plannedSeeds.Add(choice);
                eventsLeftThisTurn--;
                if (choice.seed != null)
                {
                    seedTitlesTaken.Add(choice.seed.Title);
                    RememberEvent(choice.seed.Title);
                }
            }

            if (logPlans)
            {
                string action = choice.kind == AIIntentKind.AssignClerics ? "clérigo" : "seed";
                Debug.Log(
                    $"RivalAI ({name}/{agent.personality}): {(misplayed ? "misplay " : "")}{action} cost {choice.cost} → {choice.zone.SectorName}",
                    this);
            }
        }

        DistrictZone origin = FindPowerBase(agent.faction);
        foreach (KeyValuePair<DistrictZone, int> pair in plannedClerics)
        {
            intents.Add(new AIIntent
            {
                Kind = AIIntentKind.AssignClerics,
                Faction = agent.faction,
                Origin = pair.Key == origin ? null : origin,
                Target = pair.Key,
                Amount = pair.Value,
                Label = name
            });
        }

        for (int i = 0; i < plannedSeeds.Count; i++)
        {
            Choice choice = plannedSeeds[i];
            intents.Add(new AIIntent
            {
                Kind = AIIntentKind.PlantSeed,
                Faction = agent.faction,
                Target = choice.zone,
                Seed = choice.seed,
                Amount = 1,
                Label = choice.seed != null ? choice.seed.Title : name
            });
        }
    }

    private bool TryPick(
        Agent agent,
        AIInfluenceController influence,
        DebugAI seeds,
        Dictionary<DistrictZone, int> costs,
        HashSet<DistrictZone> slipping,
        int points,
        int clericsLeft,
        int seedsAllowed,
        int sendsAllowed,
        Tuning tuning,
        out Choice picked,
        out bool misplayed)
    {
        picked = default;
        misplayed = false;

        IReadOnlyList<DistrictZone> zones = InfluenceManager.Get.GetPlayableZones();
        FactionId? leader = FaithEclipseTracker.GetLeader(out _);
        if (leader == agent.faction) leader = null;
        List<Choice> choices = new List<Choice>();
        for (int i = 0; i < zones.Count; i++)
        {
            DistrictZone zone = zones[i];
            if (zone == null || !zone.IsPlayable || zone.Influence == null) continue;
            if (!costs.TryGetValue(zone, out int cost)) continue;

            bool lost = slipping != null && slipping.Contains(zone);
            FactionId? owner = zone.Influence.Status == ZoneControlStatus.Controlled ? zone.Influence.Controller : null;
            bool hunt = owner.HasValue && owner != agent.faction && Hunts(agent.personality, owner.Value, leader, zone.District);
            int weight = influence != null ? influence.ObjectiveWeight(agent.faction, zone.District) : 0;
            if (!lost && !hunt && weight <= 0) continue;

            float pressure = PressureOn(zone, agent.faction, tuning.aggression);
            bool secured = zone.Influence.Status == ZoneControlStatus.Controlled
                && zone.Influence.Controller == agent.faction
                && !EnemyShare(zone, agent.faction);
            int reserve = ReserveFor(agent.personality, tuning.pace.clericReserve);
            bool canSend = sendsAllowed > 0 && (clericsLeft > reserve || (lost && clericsLeft > 0));
            plannedClerics.TryGetValue(zone, out int planned);
            int stack = zone.Influence.GetClerics(agent.faction) + planned;
            int opposition = Opposition(zone, agent.faction);
            if (canSend && zone.Influence.CanEnterByNormalMeans(agent.faction)
                && WillingToSend(agent.personality, lost, opposition, EnemyShare(zone, agent.faction), stack, sendsAllowed))
            {
                Choice clerics = MakeChoice(zone, AIIntentKind.AssignClerics, null, weight, cost, lost, secured, pressure, agent.personality);
                clerics.hunt = hunt;
                clerics.defenders = hunt ? zone.Influence.GetClerics(owner.Value) : 0;
                clerics.opposition = opposition;
                clerics.full = IsFull(agent.personality, secured, stack, opposition);
                choices.Add(clerics);
            }

            if (seedsAllowed > 0 && seeds != null && !zone.IsOccupied && !plannedSeedZones.Contains(zone) &&
                seeds.TryPickSeed(zone.District, out Seed seed, seedTitlesTaken) && seed != null)
            {
                choices.Add(MakeChoice(zone, AIIntentKind.PlantSeed, seed, weight, cost, lost, secured, pressure, agent.personality));
            }
        }

        int bestIndex = -1;
        int cheapest = int.MaxValue;
        List<int> cheap = new List<int>();
        for (int i = 0; i < choices.Count; i++)
        {
            Choice choice = choices[i];
            if (choice.cost > points) continue;
            if (choice.kind == AIIntentKind.AssignClerics && clericsLeft <= 0) continue;

            if (bestIndex < 0 || CompareChoice(choice, choices[bestIndex], agent.personality) < 0) bestIndex = i;
            if (choice.cost < cheapest)
            {
                cheapest = choice.cost;
                cheap.Clear();
                cheap.Add(i);
            }
            else if (choice.cost == cheapest)
            {
                cheap.Add(i);
            }
        }

        if (bestIndex < 0) return false;

        int index = bestIndex;
        if (tuning.mistakes > 0f && cheap.Count > 0 && UnityEngine.Random.value < tuning.mistakes)
        {
            index = cheap[UnityEngine.Random.Range(0, cheap.Count)];
        }

        misplayed = index != bestIndex;
        picked = choices[index];
        return true;
    }

    private static Choice MakeChoice(
        DistrictZone zone,
        AIIntentKind kind,
        Seed seed,
        int weight,
        int cost,
        bool slipping,
        bool secured,
        float pressure,
        AIPersonality personality)
    {
        return new Choice
        {
            zone = zone,
            kind = kind,
            seed = seed,
            weight = weight,
            cost = Mathf.Max(1, cost),
            slipping = slipping,
            secured = secured,
            pressure = pressure,
            personality = PersonalityBias(personality, kind)
        };
    }

    private static int CompareChoice(Choice a, Choice b, AIPersonality style)
    {
        if (style != AIPersonality.Aggressive && a.slipping != b.slipping) return a.slipping ? -1 : 1;
        if (a.full != b.full) return a.full ? 1 : -1;
        if (a.hunt != b.hunt) return a.hunt ? -1 : 1;
        if (a.hunt && a.defenders != b.defenders) return a.defenders.CompareTo(b.defenders);
        if (style == AIPersonality.Aggressive && (a.opposition > 0) != (b.opposition > 0)) return a.opposition > 0 ? -1 : 1;
        if (style == AIPersonality.Opportunist && a.opposition != b.opposition) return a.opposition.CompareTo(b.opposition);
        if (a.weight != b.weight) return b.weight.CompareTo(a.weight);
        if (a.slipping != b.slipping) return a.slipping ? -1 : 1;
        if (a.cost != b.cost) return a.cost.CompareTo(b.cost);
        if (a.secured != b.secured) return a.secured ? 1 : -1;
        if (!Mathf.Approximately(a.pressure, b.pressure)) return b.pressure.CompareTo(a.pressure);
        if (!Mathf.Approximately(a.personality, b.personality)) return b.personality.CompareTo(a.personality);
        return string.Compare(a.zone.SectorName, b.zone.SectorName, StringComparison.Ordinal);
    }

    private static float PersonalityBias(AIPersonality personality, AIIntentKind kind)
    {
        switch (personality)
        {
            case AIPersonality.Aggressive:
                return kind == AIIntentKind.AssignClerics ? 1f : 0f;
            case AIPersonality.Cautious:
                return kind == AIIntentKind.PlantSeed ? 1f : 0f;
            default:
                return 0.5f;
        }
    }

    private bool Hunts(AIPersonality personality, FactionId owner, FactionId? leader, Districts district)
    {
        switch (personality)
        {
            case AIPersonality.Aggressive: return true;
            case AIPersonality.Cautious: return false;
            default: return owner == leader || (owner == FactionId.Player && playerDistrictThreats.Contains(district));
        }
    }

    private bool WillingToSend(AIPersonality personality, bool slipping, int opposition, bool enemyShare, int stack, int sendsAllowed)
    {
        if (personality == AIPersonality.Cautious && stack >= cautiousClericCap) return false;
        if (slipping) return true;
        switch (personality)
        {
            case AIPersonality.Cautious: return opposition == 0 && !enemyShare;
            case AIPersonality.Opportunist: return opposition <= stack + sendsAllowed;
            default: return true;
        }
    }

    private static bool IsFull(AIPersonality personality, bool secured, int stack, int opposition)
    {
        switch (personality)
        {
            case AIPersonality.Cautious: return stack >= 3;
            case AIPersonality.Aggressive: return secured || stack >= 1;
            default: return secured || stack >= Mathf.Max(2, opposition + 1);
        }
    }

    private static int ReserveFor(AIPersonality personality, int reserve)
    {
        switch (personality)
        {
            case AIPersonality.Aggressive: return Mathf.Max(0, reserve - 1);
            case AIPersonality.Cautious: return reserve + 1;
            default: return reserve;
        }
    }

    private static int Opposition(DistrictZone zone, FactionId self)
    {
        ZoneInfluenceState state = zone.Influence;
        int clerics = 0;
        foreach (FactionId faction in (FactionId[])Enum.GetValues(typeof(FactionId)))
        {
            if (faction != self) clerics += state.GetClerics(faction);
        }

        return clerics;
    }

    private HashSet<DistrictZone> SlippingZones(FactionId faction)
    {
        HashSet<DistrictZone> current = CollectControlled(faction);
        HashSet<DistrictZone> slipping = new HashSet<DistrictZone>();

        if (controlledMemory.TryGetValue(faction, out HashSet<DistrictZone> previous))
        {
            foreach (DistrictZone zone in previous)
            {
                if (zone != null && !current.Contains(zone)) slipping.Add(zone);
            }
        }

        foreach (DistrictZone zone in current)
        {
            if (EnemyShare(zone, faction)) slipping.Add(zone);
        }

        controlledMemory[faction] = current;
        return slipping;
    }

    private static HashSet<DistrictZone> CollectControlled(FactionId faction)
    {
        HashSet<DistrictZone> owned = new HashSet<DistrictZone>();
        if (InfluenceManager.IsNull) return owned;

        IReadOnlyList<DistrictZone> zones = InfluenceManager.Get.GetPlayableZones();
        for (int i = 0; i < zones.Count; i++)
        {
            DistrictZone zone = zones[i];
            ZoneInfluenceState state = zone != null ? zone.Influence : null;
            if (state == null) continue;
            if (state.Status == ZoneControlStatus.Controlled && state.Controller == faction) owned.Add(zone);
        }

        return owned;
    }

    private static bool EnemyShare(DistrictZone zone, FactionId self)
    {
        ZoneInfluenceState state = zone != null ? zone.Influence : null;
        if (state == null) return false;

        foreach (FactionId faction in state.FactionsWithShare())
        {
            if (faction != self && state.GetShare(faction) > 0) return true;
        }

        return false;
    }

    private ZoneTravelGraph Travel()
    {
        ZoneTravelGraph graph = FindAnyObjectByType<ZoneTravelGraph>();
        if (graph == null) graph = gameObject.AddComponent<ZoneTravelGraph>();
        graph.EnsureReady();
        return graph;
    }

    private float PressureOn(DistrictZone zone, FactionId self, float aggression)
    {
        float againstPlayer = Pressure(zone, FactionId.Player);
        float againstRivals = 0f;

        for (int i = 0; i < agents.Count; i++)
        {
            Agent other = agents[i];
            if (other == null || other.faction == self) continue;
            againstRivals = Mathf.Max(againstRivals, Pressure(zone, other.faction));
        }

        float balanced = Mathf.Max(againstPlayer, againstRivals);
        float focused = againstPlayer;
        if (balanced < 0.05f) balanced = 0.3f;
        if (focused < 0.05f) focused = 0.12f;
        return Mathf.Lerp(balanced, focused, aggression);
    }

    private static float Pressure(DistrictZone zone, FactionId faction)
    {
        ZoneInfluenceState state = zone.Influence;
        if (state == null) return 0f;

        if (state.Status == ZoneControlStatus.Controlled && state.Controller == faction) return 1f;
        if (state.GetShare(faction) > 0 || state.GetClerics(faction) > 0) return 0.65f;
        if (NeighborControlledBy(zone, faction)) return 0.4f;
        return 0f;
    }

    private static bool NeighborControlledBy(DistrictZone zone, FactionId faction)
    {
        if (InfluenceManager.IsNull) return false;

        IReadOnlyList<DistrictZone> neighbors = InfluenceManager.Get.Adjacency.GetNeighbors(zone);
        for (int i = 0; i < neighbors.Count; i++)
        {
            DistrictZone neighbor = neighbors[i];
            if (neighbor == null || neighbor.Influence == null) continue;
            if (neighbor.Influence.Status == ZoneControlStatus.Controlled &&
                neighbor.Influence.Controller == faction)
            {
                return true;
            }
        }

        return false;
    }

    private static DistrictZone FindPowerBase(FactionId faction)
    {
        IReadOnlyList<DistrictZone> zones = InfluenceManager.Get.GetPlayableZones();
        DistrictZone best = null;
        int bestPresence = 0;

        for (int i = 0; i < zones.Count; i++)
        {
            DistrictZone candidate = zones[i];
            if (candidate == null || candidate.Influence == null) continue;

            int presence = candidate.Influence.GetClerics(faction) + candidate.Influence.GetShare(faction);
            if (presence <= bestPresence) continue;
            bestPresence = presence;
            best = candidate;
        }

        return best;
    }
}
