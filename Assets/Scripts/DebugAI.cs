using System.Collections.Generic;
using UnityEngine;

public class DebugAI : MonoBehaviour
{
    [SerializeField] private SeedsPool seedsPool;
    [SerializeField] private AIProfile aiProfile;

    [Space]

    [SerializeField] private int minTickCooldown = 3;
    [SerializeField] private float spawnTickChanceRatio = 0.25f;

    private bool inCooldown = false;
    private int tickTimer = 0;

    private void Start()
    {
        GameTime.OnTurnStarted += OnTurnStarted;
    }

    private void OnTurnStarted()
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

        bool rollSucess = spawnTickChanceRatio > Random.Range(0f, 1f - Mathf.Epsilon);

        if (rollSucess)
        {
            DistrictData district = DistrictsManager.GetRandomDistrict();

            if (district == null) return;

            Node node = district.GetRandomNode();
            Seed seed = GetRandomAllowedSeed();
            if (seed == null) return;

            node.AddSeed(seed);

            inCooldown = true;
            tickTimer = 0;

            Debug.Log($"AI planted {seed} at {node} in {node.District}");
        }
    }

    private Seed GetRandomAllowedSeed()
    {
        if (seedsPool == null || seedsPool.EvilSeeds == null || seedsPool.EvilSeeds.Count == 0)
        {
            Debug.LogWarning("DebugAI has no evil seeds pool configured.");
            return null;
        }

        if (aiProfile == null)
        {
            Debug.LogWarning("DebugAI has no AIProfile assigned. Falling back to unfiltered seed pool.");
        }

        List<Seed> candidates = new List<Seed>();

        for (int i = 0; i < seedsPool.EvilSeeds.Count; i++)
        {
            Seed seed = seedsPool.EvilSeeds[i];
            if (seed == null) continue;

            bool allowed = aiProfile == null || aiProfile.CanUse(seed);
            if (allowed) candidates.Add(seed);
        }

        if (candidates.Count == 0)
        {
            if (aiProfile == null)
            {
                Debug.LogWarning("DebugAI found no non-null seeds in the unfiltered evil seed pool.");
            }
            else
            {
                Debug.LogWarning(
                    $"DebugAI found no candidate seeds for profile '{aiProfile.name}' " +
                    $"(type: {aiProfile.PrimaryType}, max difficulty: {aiProfile.MaxDifficulty})."
                );
            }
            return null;
        }

        return candidates[Random.Range(0, candidates.Count)];
    }
}