using System;
using System.Collections.Generic;
using UnityEngine;

public enum RivalDifficulty
{
    Easy,
    Normal,
    Hard
}

/// <summary>
/// Tres rivales con objetivos y un presupuesto de puntos.
/// La dificultad cambia los puntos, la agresividad y cuántas jugadas desperdician.
/// </summary>
[DefaultExecutionOrder(45)]
public class RivalAIDirector : MonoBehaviour
{
    [Serializable]
    public class Agent
    {
        public FactionId faction = FactionId.Rival1;
        public AIPersonality personality = AIPersonality.Clerics;
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
        public float pressure;
        public float personality;
    }

    [SerializeField] private List<Agent> agents = new List<Agent>
    {
        new Agent { faction = FactionId.Rival1, personality = AIPersonality.Seeds },
        new Agent { faction = FactionId.Rival2, personality = AIPersonality.Balanced },
        new Agent { faction = FactionId.Rival3, personality = AIPersonality.Clerics }
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

    private int actionPoints = 4;
    private float aggressiveness = 0.35f;
    private float mistakeChance = 0.1f;
    private bool difficultyApplied;

    private readonly List<DistrictZone> plannedSeedZones = new List<DistrictZone>();
    private readonly List<Choice> plannedSeeds = new List<Choice>();
    private readonly Dictionary<DistrictZone, int> plannedClerics = new Dictionary<DistrictZone, int>();
    private readonly Dictionary<FactionId, HashSet<DistrictZone>> controlledMemory =
        new Dictionary<FactionId, HashSet<DistrictZone>>();

    public int ActionsPerTurn => actionPoints;
    public float Aggressiveness => aggressiveness;

    private void Awake()
    {
        if (!difficultyApplied) ApplyDifficulty(RivalDifficulty.Normal);
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
            case AIPersonality.Clerics: return "Clerics";
            case AIPersonality.Seeds: return "Seeds";
            default: return "Balanced";
        }
    }

    /// <summary>Easy baja puntos y agresividad. Hard hace lo contrario. El plan de objetivos no cambia.</summary>
    public void ApplyDifficulty(RivalDifficulty difficulty)
    {
        switch (difficulty)
        {
            case RivalDifficulty.Easy:
                SetDifficulty(Mathf.Max(1, normalActionPoints - 2), 0.1f, easyMistakeChance);
                break;
            case RivalDifficulty.Hard:
                SetDifficulty(normalActionPoints + 2, 0.85f, hardMistakeChance);
                break;
            default:
                SetDifficulty(normalActionPoints, normalAggressiveness, normalMistakeChance);
                break;
        }
    }

    /// <summary>Ata la dificultad: pocas acciones y poca agresividad, o al revés.</summary>
    public void ApplyDifficulty(int actions, float aggression)
    {
        float mistakes = normalMistakeChance;
        if (aggression <= 0.2f) mistakes = easyMistakeChance;
        else if (aggression >= 0.7f) mistakes = hardMistakeChance;
        SetDifficulty(actions, aggression, mistakes);
    }

    public void PlanIntents(List<AIIntent> intents)
    {
        if (intents == null || InfluenceManager.IsNull) return;

        for (int i = 0; i < agents.Count; i++)
        {
            Agent agent = agents[i];
            if (agent == null) continue;
            PlanAgent(agent, intents);
        }
    }

    private void SetDifficulty(int actions, float aggression, float mistakes)
    {
        difficultyApplied = true;
        actionPoints = Mathf.Max(0, actions);
        aggressiveness = Mathf.Clamp01(aggression);
        mistakeChance = Mathf.Clamp01(mistakes);
    }

    private void PlanAgent(Agent agent, List<AIIntent> intents)
    {
        HashSet<DistrictZone> slipping = SlippingZones(agent.faction);
        if (actionPoints <= 0) return;

        ZoneTravelGraph travel = Travel();
        Dictionary<DistrictZone, int> costs = new Dictionary<DistrictZone, int>();
        // Cost comes only from the saved touch paths: 1 hop = 1, 2 = 4, 3 = 9, plus the unclaimed border.
        travel.FillActionCosts(agent.faction, costs);

        AIInfluenceController influence = FindAnyObjectByType<AIInfluenceController>();
        DebugAI seeds = FindAnyObjectByType<DebugAI>();
        int clericsLeft = influence != null ? influence.ProjectedClericBudget(agent.faction) : 0;

        plannedClerics.Clear();
        plannedSeedZones.Clear();
        plannedSeeds.Clear();

        int points = actionPoints;
        string name = FactionIdUtil.DisplayName(agent.faction);
        while (points > 0)
        {
            if (!TryPick(agent, influence, seeds, costs, slipping, points, clericsLeft, out Choice choice, out bool misplayed))
            {
                break;
            }

            points -= choice.cost;
            if (choice.kind == AIIntentKind.AssignClerics)
            {
                clericsLeft--;
                plannedClerics.TryGetValue(choice.zone, out int amount);
                plannedClerics[choice.zone] = amount + 1;
            }
            else
            {
                plannedSeedZones.Add(choice.zone);
                plannedSeeds.Add(choice);
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
        out Choice picked,
        out bool misplayed)
    {
        picked = default;
        misplayed = false;

        IReadOnlyList<DistrictZone> zones = InfluenceManager.Get.GetPlayableZones();
        List<Choice> choices = new List<Choice>();
        for (int i = 0; i < zones.Count; i++)
        {
            DistrictZone zone = zones[i];
            if (zone == null || !zone.IsPlayable || zone.Influence == null) continue;
            if (!costs.TryGetValue(zone, out int cost)) continue;

            bool lost = slipping != null && slipping.Contains(zone);
            int weight = influence != null ? influence.ObjectiveWeight(agent.faction, zone.District) : 0;
            if (!lost && weight <= 0) continue;

            float pressure = PressureOn(zone, agent.faction);
            bool secured = zone.Influence.Status == ZoneControlStatus.Controlled
                && zone.Influence.Controller == agent.faction
                && !EnemyShare(zone, agent.faction);
            if (clericsLeft > 0 && zone.Influence.CanEnterByNormalMeans(agent.faction))
            {
                choices.Add(MakeChoice(zone, AIIntentKind.AssignClerics, null, weight, cost, lost, secured, pressure, agent.personality));
            }

            if (seeds != null && !zone.IsOccupied && !plannedSeedZones.Contains(zone) &&
                seeds.TryPickSeed(zone.District, out Seed seed) && seed != null)
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

            if (bestIndex < 0 || CompareChoice(choice, choices[bestIndex]) < 0) bestIndex = i;
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
        if (mistakeChance > 0f && cheap.Count > 0 && UnityEngine.Random.value < mistakeChance)
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

    /// <summary>Mayor peso, luego reconquista, luego menor coste. Un nodo ya seguro cede ante uno que todavía no lo está.</summary>
    private static int CompareChoice(Choice a, Choice b)
    {
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
            case AIPersonality.Clerics:
                return kind == AIIntentKind.AssignClerics ? 1f : 0f;
            case AIPersonality.Seeds:
                return kind == AIIntentKind.PlantSeed ? 1f : 0f;
            default:
                return 0.5f;
        }
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

    private float PressureOn(DistrictZone zone, FactionId self)
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
        return Mathf.Lerp(balanced, focused, aggressiveness);
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
