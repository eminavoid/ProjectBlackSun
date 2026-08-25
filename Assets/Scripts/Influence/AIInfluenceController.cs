using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Vertical Slice: 4 rivales fijos que asignan clérigos a zonas preferidas.
/// Planifica y ejecuta en fases separadas; AIIntentBoard dispara ambas.
/// </summary>
[DefaultExecutionOrder(40)]
public class AIInfluenceController : MonoBehaviour
{
    [SerializeField] private List<AIInfluenceProfile> rivalProfiles = new List<AIInfluenceProfile>();
    [SerializeField] private bool autoCreateDefaultProfiles = true;
    [SerializeField] private bool logActions;

    private readonly Dictionary<DistrictZone, int> plannedByZone = new Dictionary<DistrictZone, int>();

    private bool initialized;

    private void Start()
    {
        EnsureInitialized();
    }

    /// <summary>Decide sin tocar el estado del juego: sólo describe lo que hará al commitear.</summary>
    public void PlanIntents(List<AIIntent> intents)
    {
        EnsureInitialized();
        if (InfluenceManager.IsNull) return;

        for (int i = 0; i < rivalProfiles.Count; i++)
        {
            AIInfluenceProfile profile = rivalProfiles[i];
            if (profile == null) continue;
            PlanFor(profile, intents);
        }
    }

    /// <summary>Ingreso de clérigos del turno. Se hace una vez, antes de ejecutar los intents.</summary>
    public void PrepareCommit()
    {
        EnsureInitialized();
        if (InfluenceManager.IsNull) return;

        for (int i = 0; i < rivalProfiles.Count; i++)
        {
            AIInfluenceProfile profile = rivalProfiles[i];
            if (profile == null) continue;
            InfluenceManager.Get.GetStock(profile.Faction).ReturnClerics(profile.ClericsPerTurn);
        }
    }

    public bool ExecuteIntent(AIIntent intent)
    {
        if (intent == null || intent.Kind != AIIntentKind.AssignClerics || !intent.Faction.HasValue) return false;
        if (InfluenceManager.IsNull) return false;

        AIInfluenceProfile profile = FindProfile(intent.Faction.Value);
        if (profile == null) return false;

        DistrictZone zone = ResolveExecutionTarget(profile, intent.Target);
        if (zone == null) return false;

        InfluenceManager manager = InfluenceManager.Get;
        int available = Mathf.Min(intent.Amount, manager.GetStock(profile.Faction).Clerics);
        if (available <= 0) return false;

        int assigned = 0;
        for (int i = 0; i < available; i++)
        {
            if (!manager.TryAssignClerics(zone, profile.Faction, 1, out string error))
            {
                if (logActions)
                {
                    Debug.Log($"AIInfluence ({profile.DisplayName}): assign failed — {error}", this);
                }

                break;
            }

            assigned++;
        }

        if (assigned > 0 && logActions)
        {
            Debug.Log(
                $"AIInfluence ({profile.DisplayName}): +{assigned} cleric(s) → {zone.SectorName} ({zone.District})",
                this);
        }

        return assigned > 0;
    }

    private void PlanFor(AIInfluenceProfile profile, List<AIIntent> intents)
    {
        FactionStock stock = InfluenceManager.Get.GetStock(profile.Faction);

        // Presupuesto proyectado: el ingreso del turno recién se acredita al commitear.
        int budget = stock.Clerics + profile.ClericsPerTurn;
        int toAssign = Mathf.Min(profile.MaxAssignPerTurn, budget);
        if (toAssign <= 0) return;

        plannedByZone.Clear();

        for (int n = 0; n < toAssign; n++)
        {
            if (!TryPickZone(profile, out DistrictZone zone)) break;
            plannedByZone.TryGetValue(zone, out int planned);
            plannedByZone[zone] = planned + 1;
        }

        DistrictZone origin = FindPowerBase(profile);

        foreach (KeyValuePair<DistrictZone, int> pair in plannedByZone)
        {
            intents.Add(new AIIntent
            {
                Kind = AIIntentKind.AssignClerics,
                Faction = profile.Faction,
                Origin = pair.Key == origin ? null : origin,
                Target = pair.Key,
                Amount = pair.Value,
                Label = profile.DisplayName
            });
        }
    }

    /// <summary>Si el jugador bloqueó la zona planificada, se re-elige en el momento del commit.</summary>
    private DistrictZone ResolveExecutionTarget(AIInfluenceProfile profile, DistrictZone planned)
    {
        if (planned != null &&
            planned.IsPlayable &&
            planned.Influence != null &&
            planned.Influence.CanEnterByNormalMeans(profile.Faction))
        {
            return planned;
        }

        plannedByZone.Clear();
        if (!TryPickZone(profile, out DistrictZone fallback)) return null;

        if (logActions)
        {
            Debug.Log(
                $"AIInfluence ({profile.DisplayName}): zona planificada bloqueada, redirige a {fallback.SectorName}.",
                this);
        }

        return fallback;
    }

    private bool TryPickZone(AIInfluenceProfile profile, out DistrictZone zone)
    {
        zone = null;
        List<DistrictZone> preferred = new List<DistrictZone>();
        List<DistrictZone> fallback = new List<DistrictZone>();

        IReadOnlyList<DistrictZone> zones = InfluenceManager.Get.GetPlayableZones();
        for (int i = 0; i < zones.Count; i++)
        {
            DistrictZone candidate = zones[i];
            if (candidate == null || candidate.Influence == null) continue;
            if (!candidate.Influence.CanEnterByNormalMeans(profile.Faction)) continue;

            if (profile.Prefers(candidate.District)) preferred.Add(candidate);
            else fallback.Add(candidate);
        }

        List<DistrictZone> pool = preferred.Count > 0 ? preferred : fallback;
        if (pool.Count == 0) return false;

        pool.Sort((a, b) =>
        {
            int presenceA = Presence(a, profile.Faction);
            int presenceB = Presence(b, profile.Faction);
            if (presenceA != presenceB) return presenceB.CompareTo(presenceA);
            return a.Influence.TotalInfluence.CompareTo(b.Influence.TotalInfluence);
        });

        zone = pool[0];
        return zone != null;
    }

    private int Presence(DistrictZone zone, FactionId faction)
    {
        int presence = zone.Influence.GetClerics(faction) + zone.Influence.GetShare(faction);
        if (plannedByZone.TryGetValue(zone, out int planned)) presence += planned;
        return presence;
    }

    /// <summary>Zona con mayor presencia de la secta: origen visual de la flecha de intención.</summary>
    private DistrictZone FindPowerBase(AIInfluenceProfile profile)
    {
        IReadOnlyList<DistrictZone> zones = InfluenceManager.Get.GetPlayableZones();

        DistrictZone best = null;
        int bestPresence = 0;

        for (int i = 0; i < zones.Count; i++)
        {
            DistrictZone candidate = zones[i];
            if (candidate == null || candidate.Influence == null) continue;

            int presence = candidate.Influence.GetClerics(profile.Faction)
                + candidate.Influence.GetShare(profile.Faction);

            if (presence <= bestPresence) continue;
            bestPresence = presence;
            best = candidate;
        }

        return best;
    }

    /// <summary>Secta que “dueña” una plantación: la que prefiere el distrito, o la de más presencia.</summary>
    public FactionId PickPlanter(DistrictZone zone)
    {
        EnsureInitialized();

        AIInfluenceProfile preferred = null;
        AIInfluenceProfile present = null;
        int preferredPresence = int.MinValue;
        int presentScore = int.MinValue;

        for (int i = 0; i < rivalProfiles.Count; i++)
        {
            AIInfluenceProfile profile = rivalProfiles[i];
            if (profile == null) continue;

            int presence = zone != null && zone.Influence != null
                ? zone.Influence.GetClerics(profile.Faction) + zone.Influence.GetShare(profile.Faction)
                : 0;

            if (presence > presentScore)
            {
                presentScore = presence;
                present = profile;
            }

            if (zone != null && profile.Prefers(zone.District) && presence >= preferredPresence)
            {
                preferredPresence = presence;
                preferred = profile;
            }
        }

        if (preferred != null) return preferred.Faction;
        if (present != null) return present.Faction;
        return FactionId.Rival1;
    }

    private AIInfluenceProfile FindProfile(FactionId faction)
    {
        for (int i = 0; i < rivalProfiles.Count; i++)
        {
            if (rivalProfiles[i] != null && rivalProfiles[i].Faction == faction) return rivalProfiles[i];
        }

        return null;
    }

    private void EnsureInitialized()
    {
        if (initialized) return;
        initialized = true;

        EnsureDefaultProfiles();
        ApplyStartingStocks();
    }

    private void EnsureDefaultProfiles()
    {
        if (!autoCreateDefaultProfiles) return;
        if (rivalProfiles != null && rivalProfiles.Count >= 4) return;

        rivalProfiles = new List<AIInfluenceProfile>
        {
            AIInfluenceProfile.CreateRuntime(FactionId.Rival1, "Crimson Choir", Districts.District1, Districts.District4),
            AIInfluenceProfile.CreateRuntime(FactionId.Rival2, "Azure Ledger", Districts.District2, Districts.District6),
            AIInfluenceProfile.CreateRuntime(FactionId.Rival3, "Verdant Flock", Districts.District3, Districts.District5),
            AIInfluenceProfile.CreateRuntime(FactionId.Rival4, "Violet Veil", Districts.District5, Districts.District1)
        };
    }

    private void ApplyStartingStocks()
    {
        if (InfluenceManager.IsNull) return;

        for (int i = 0; i < rivalProfiles.Count; i++)
        {
            AIInfluenceProfile profile = rivalProfiles[i];
            if (profile == null) continue;
            InfluenceManager.Get.GetStock(profile.Faction).Clerics = profile.StartingClerics;
        }
    }
}
