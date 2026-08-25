using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Antagonista que planta seeds a nombre de una de las cuatro sectas.
/// El color de la flecha es el de esa IA, igual que con los clérigos.
/// </summary>
[DefaultExecutionOrder(50)]
public class DebugAI : MonoBehaviour
{
    [SerializeField] private SeedsPool seedsPool;
    [SerializeField] private AIProfile aiProfile;

    [Space]
    [SerializeField] private bool showStartupReport = true;
    [SerializeField] private bool showPlantLogs = true;

    [SerializeField] private int minTickCooldown = 3;
    [SerializeField] private float spawnTickChanceRatio = 0.25f;

    private bool inCooldown;
    private int tickTimer;
    private bool warnedNoZones;
    private bool warnedNoSeeds;

    private void Start()
    {
        if (showStartupReport)
        {
            LogStartupReport();
        }
    }

    private void LogStartupReport()
    {
        int totalZones = 0;
        foreach (Districts district in System.Enum.GetValues(typeof(Districts)))
        {
            int count = DistrictsManager.GetDistrictZones(district).Count;
            totalZones += count;
            Debug.Log($"DebugAI setup: {district} → {count} sector(es).", this);
        }

        if (totalZones == 0)
        {
            Debug.LogWarning(
                "DebugAI: no hay sectores (DistrictZone) en escena. Comprueba map bootstrap en DistrictSelectionController.",
                this);
        }

        Seed probe = GetRandomAllowedSeed(null);
        if (probe == null && !warnedNoSeeds)
        {
            warnedNoSeeds = true;
            Debug.LogWarning(
                aiProfile != null
                    ? $"DebugAI: ninguna seed del pool coincide con el perfil '{aiProfile.name}'."
                    : "DebugAI: pool de seeds vacío o sin entradas válidas.",
                this);
        }
        else if (probe != null)
        {
            Debug.Log($"DebugAI: listo (perfil: {(aiProfile != null ? aiProfile.name : "sin filtro")}, seed ejemplo: '{probe.Title}').", this);
        }
    }

    /// <summary>Tira el dado y elige zona + seed sin plantar todavía.</summary>
    public void PlanIntents(List<AIIntent> intents)
    {
        if (inCooldown)
        {
            tickTimer += 1;

            if (tickTimer > minTickCooldown)
            {
                inCooldown = false;
            }

            return;
        }

        bool rollSuccess = spawnTickChanceRatio > Random.Range(0f, 1f - Mathf.Epsilon);
        if (!rollSuccess) return;

        if (!DistrictsManager.TryGetRandomFreeZoneAnyDistrict(out DistrictZone zone) || zone == null)
        {
            if (!warnedNoZones)
            {
                warnedNoZones = true;
                Debug.LogWarning("DebugAI: no hay sectores libres para plantar.", this);
            }

            return;
        }

        Seed seed = GetRandomAllowedSeed(zone.District);
        if (seed == null) return;
        if (!seed.CanPlantInDistrict(zone.District)) return;

        intents.Add(new AIIntent
        {
            Kind = AIIntentKind.PlantSeed,
            Faction = ResolvePlanter(zone),
            Target = zone,
            Seed = seed,
            Amount = 1,
            Label = seed.Title
        });

        // El cooldown arranca al decidir: la tirada del turno ya se consumió.
        inCooldown = true;
        tickTimer = 0;
    }

    public bool ExecuteIntent(AIIntent intent)
    {
        if (intent == null || intent.Kind != AIIntentKind.PlantSeed) return false;

        DistrictZone zone = intent.Target;
        Seed seed = intent.Seed;
        if (zone == null || seed == null) return false;

        if (zone.IsOccupied)
        {
            if (showPlantLogs)
            {
                Debug.Log($"DebugAI: sector '{zone.SectorName}' ocupado en {zone.District}, se omite plantación.", this);
            }

            return false;
        }

        if (!zone.AddSeed(seed)) return false;

        if (showPlantLogs)
        {
            Debug.Log($"DebugAI: plantó '{seed.Title}' en sector '{zone.SectorName}' ({zone.District}).", this);
        }

        return true;
    }

    private Seed GetRandomAllowedSeed(Districts? districtFilter)
    {
        if (seedsPool == null || seedsPool.EvilSeeds == null || seedsPool.EvilSeeds.Count == 0)
        {
            if (!warnedNoSeeds)
            {
                warnedNoSeeds = true;
                Debug.LogWarning("DebugAI: Seeds Pool no configurado.", this);
            }

            return null;
        }

        List<Seed> candidates = new List<Seed>();

        for (int i = 0; i < seedsPool.EvilSeeds.Count; i++)
        {
            Seed seed = seedsPool.EvilSeeds[i];
            if (seed == null) continue;

            if (aiProfile != null && !aiProfile.CanUse(seed)) continue;

            if (districtFilter.HasValue && !seed.CanPlantInDistrict(districtFilter.Value)) continue;

            candidates.Add(seed);
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        return candidates[Random.Range(0, candidates.Count)];
    }

    private static FactionId ResolvePlanter(DistrictZone zone)
    {
        AIInfluenceController controller = FindAnyObjectByType<AIInfluenceController>();
        return controller != null ? controller.PickPlanter(zone) : FactionId.Rival1;
    }
}
