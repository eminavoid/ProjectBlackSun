using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Vertical Slice: 4 rivales fijos que asignan clérigos a zonas preferidas.
/// </summary>
[DefaultExecutionOrder(40)]
public class AIInfluenceController : MonoBehaviour
{
    [SerializeField] private List<AIInfluenceProfile> rivalProfiles = new List<AIInfluenceProfile>();
    [SerializeField] private bool autoCreateDefaultProfiles = true;
    [SerializeField] private bool logActions;

    private void Start()
    {
        EnsureDefaultProfiles();
        ApplyStartingStocks();
        GameTime.OnTurnStarted += OnTurnStarted;
    }

    private void OnDestroy()
    {
        GameTime.OnTurnStarted -= OnTurnStarted;
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

    private void OnTurnStarted()
    {
        if (InfluenceManager.IsNull) return;

        for (int i = 0; i < rivalProfiles.Count; i++)
        {
            AIInfluenceProfile profile = rivalProfiles[i];
            if (profile == null) continue;
            Act(profile);
        }
    }

    private void Act(AIInfluenceProfile profile)
    {
        FactionStock stock = InfluenceManager.Get.GetStock(profile.Faction);
        stock.ReturnClerics(profile.ClericsPerTurn);

        int toAssign = Mathf.Min(profile.MaxAssignPerTurn, stock.Clerics);
        if (toAssign <= 0) return;

        for (int n = 0; n < toAssign; n++)
        {
            if (!TryPickZone(profile, out DistrictZone zone)) break;
            if (!InfluenceManager.Get.TryAssignClerics(zone, profile.Faction, 1, out string error))
            {
                if (logActions)
                {
                    Debug.Log($"AIInfluence ({profile.DisplayName}): assign failed — {error}", this);
                }

                break;
            }

            if (logActions)
            {
                Debug.Log(
                    $"AIInfluence ({profile.DisplayName}): +1 cleric → {zone.SectorName} ({zone.District})",
                    this);
            }
        }
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
            int presenceA = a.Influence.GetClerics(profile.Faction) + a.Influence.GetShare(profile.Faction);
            int presenceB = b.Influence.GetClerics(profile.Faction) + b.Influence.GetShare(profile.Faction);
            if (presenceA != presenceB) return presenceB.CompareTo(presenceA);
            return a.Influence.TotalInfluence.CompareTo(b.Influence.TotalInfluence);
        });

        zone = pool[0];
        return zone != null;
    }
}
