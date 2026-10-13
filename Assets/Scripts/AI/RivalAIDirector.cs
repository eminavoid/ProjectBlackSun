using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tres rivales con personalidad. La dificultad es cuántas acciones hacen por turno
/// y qué tan agresivos son contra el jugador.
/// Agresividad baja: reparte presión entre el jugador y las otras IA.
/// Agresividad alta: se concentra en el jugador.
/// </summary>
[DefaultExecutionOrder(45)]
public class RivalAIDirector : MonoBehaviour
{
    [System.Serializable]
    public class Agent
    {
        public FactionId faction = FactionId.Rival1;
        public AIPersonality personality = AIPersonality.Clerics;
    }

    [SerializeField] private List<Agent> agents = new List<Agent>
    {
        new Agent { faction = FactionId.Rival1, personality = AIPersonality.Seeds },
        new Agent { faction = FactionId.Rival2, personality = AIPersonality.Balanced },
        new Agent { faction = FactionId.Rival3, personality = AIPersonality.Clerics }
    };

    [Tooltip("Acciones de cada IA por turno. Subir esto es subir la dificultad.")]
    [Min(0)]
    [SerializeField] private int actionsPerTurn = 2;

    [Tooltip("0 equilibra al jugador y a las otras IA. 1 se enfoca en el jugador.")]
    [Range(0f, 1f)]
    [SerializeField] private float aggressiveness = 0.35f;

    [Tooltip("Turnos en los que cada IA solo juega dentro del distrito donde arrancó, salvo que ya lo domine.")]
    [Min(0)]
    [SerializeField] private int homeDistrictTurns = 4;

    [SerializeField] private bool logPlans;

    private int turnsPlanned;

    private readonly List<DistrictZone> plannedSeedZones = new List<DistrictZone>();
    private readonly Dictionary<DistrictZone, int> plannedClerics = new Dictionary<DistrictZone, int>();

    public int ActionsPerTurn => actionsPerTurn;
    public float Aggressiveness => aggressiveness;

    /// <summary>Ata la dificultad: pocas acciones y poca agresividad, o al revés.</summary>
    public void ApplyDifficulty(int actions, float aggression)
    {
        actionsPerTurn = Mathf.Max(0, actions);
        aggressiveness = Mathf.Clamp01(aggression);
    }

    public void PlanIntents(List<AIIntent> intents)
    {
        if (intents == null || InfluenceManager.IsNull || actionsPerTurn <= 0) return;

        turnsPlanned++;

        for (int i = 0; i < agents.Count; i++)
        {
            Agent agent = agents[i];
            if (agent == null) continue;
            PlanAgent(agent, intents);
        }
    }

    private void PlanAgent(Agent agent, List<AIIntent> intents)
    {
        SplitActions(agent.personality, actionsPerTurn, out int clericActions, out int seedActions);

        AIInfluenceController clerics = FindAnyObjectByType<AIInfluenceController>();
        int budget = clerics != null ? clerics.ProjectedClericBudget(agent.faction) : 0;
        clericActions = Mathf.Min(clericActions, budget);

        plannedClerics.Clear();
        for (int i = 0; i < clericActions; i++)
        {
            if (!TryPickZone(agent.faction, forSeed: false, out DistrictZone zone)) break;
            plannedClerics.TryGetValue(zone, out int amount);
            plannedClerics[zone] = amount + 1;
        }

        DistrictZone origin = FindPowerBase(agent.faction);
        string name = FactionIdUtil.DisplayName(agent.faction);

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

            if (logPlans)
            {
                Debug.Log($"RivalAI ({name}/{agent.personality}): +{pair.Value} clérigo(s) → {pair.Key.SectorName}", this);
            }
        }

        plannedSeedZones.Clear();
        DebugAI seeds = FindAnyObjectByType<DebugAI>();
        for (int i = 0; i < seedActions; i++)
        {
            if (seeds == null) break;
            if (!TryPickZone(agent.faction, forSeed: true, out DistrictZone zone)) break;
            if (!seeds.TryPickSeed(zone.District, out Seed seed) || seed == null) break;

            plannedSeedZones.Add(zone);
            intents.Add(new AIIntent
            {
                Kind = AIIntentKind.PlantSeed,
                Faction = agent.faction,
                Target = zone,
                Seed = seed,
                Amount = 1,
                Label = seed.Title
            });

            if (logPlans)
            {
                Debug.Log($"RivalAI ({name}/{agent.personality}): seed '{seed.Title}' → {zone.SectorName}", this);
            }
        }
    }

    private static void SplitActions(AIPersonality personality, int actions, out int clerics, out int seeds)
    {
        switch (personality)
        {
            case AIPersonality.Clerics:
                clerics = actions;
                seeds = 0;
                break;
            case AIPersonality.Seeds:
                clerics = 0;
                seeds = actions;
                break;
            default:
                clerics = (actions + 1) / 2;
                seeds = actions / 2;
                break;
        }
    }

    private bool TryPickZone(FactionId self, bool forSeed, out DistrictZone best)
    {
        best = null;
        float bestScore = float.MinValue;

        IReadOnlyList<DistrictZone> zones = InfluenceManager.Get.GetPlayableZones();
        for (int i = 0; i < zones.Count; i++)
        {
            DistrictZone zone = zones[i];
            if (!IsValidTarget(zone, self, forSeed)) continue;

            float score = TargetScore(zone, self) + Random.Range(0f, 0.04f);
            if (score <= bestScore) continue;
            bestScore = score;
            best = zone;
        }

        return best != null;
    }

    /// <summary>Distrito del nodo donde esa IA empieza la partida.</summary>
    private static Districts HomeDistrict(FactionId faction)
    {
        switch (faction)
        {
            case FactionId.Rival1: return Districts.District1;
            case FactionId.Rival2: return Districts.District6;
            case FactionId.Rival3: return Districts.District3;
            default: return Districts.District1;
        }
    }

    private bool IsConsolidating(FactionId faction)
    {
        if (turnsPlanned > homeDistrictTurns) return false;
        if (InfluenceManager.IsNull) return true;
        return !InfluenceManager.Get.IsDistrictControlledBy(HomeDistrict(faction), faction);
    }

    private bool IsValidTarget(DistrictZone zone, FactionId self, bool forSeed)
    {
        if (zone == null || !zone.IsPlayable || zone.Influence == null) return false;
        if (IsConsolidating(self) && zone.District != HomeDistrict(self)) return false;

        if (forSeed)
        {
            if (zone.IsOccupied) return false;
            for (int i = 0; i < plannedSeedZones.Count; i++)
            {
                if (plannedSeedZones[i] == zone) return false;
            }

            return true;
        }

        return zone.Influence.CanEnterByNormalMeans(self);
    }

    /// <summary>
    /// Agresividad baja: el mejor entre presionar al jugador o a otra IA.
    /// Agresividad alta: casi sólo el jugador. Si no hay blanco, queda la expansión neutra.
    /// </summary>
    private float TargetScore(DistrictZone zone, FactionId self)
    {
        float againstPlayer = Pressure(zone, FactionId.Player);
        float againstRivals = 0f;

        for (int i = 0; i < agents.Count; i++)
        {
            Agent agent = agents[i];
            if (agent == null || agent.faction == self) continue;
            againstRivals = Mathf.Max(againstRivals, Pressure(zone, agent.faction));
        }

        float balanced = Mathf.Max(againstPlayer, againstRivals);
        float focused = againstPlayer;

        if (balanced < 0.05f) balanced = 0.3f;
        if (focused < 0.05f) focused = 0.12f;

        float score = Mathf.Lerp(balanced, focused, aggressiveness);
        score += DistrictBias(zone.District, self);

        int own = zone.Influence.GetShare(self) + zone.Influence.GetClerics(self);
        if (own > 0) score += 0.12f;

        return score;
    }

    /// <summary>El primer distrito de la lista es la base. Los siguientes son la expansión.</summary>
    private float DistrictBias(Districts district, FactionId self)
    {
        AIInfluenceController clerics = FindAnyObjectByType<AIInfluenceController>();
        IReadOnlyList<Districts> preferred = clerics != null ? clerics.GetPreferredDistricts(self) : null;
        if (preferred == null) return 0f;

        for (int i = 0; i < preferred.Count; i++)
        {
            if (preferred[i] != district) continue;
            float weight = i == 0 ? 0.7f : 0.35f;
            return weight * Mathf.Lerp(1f, 0.4f, aggressiveness);
        }

        return 0f;
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
