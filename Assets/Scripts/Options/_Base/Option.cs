using System.Collections.Generic;
using UnityEngine;
using System;

[CreateAssetMenu(fileName = "New Option", menuName = "Options/New Option", order = 1)]
public class Option : ScriptableObject
{
    [Header("Display")]
    [SerializeField] private string title;
    [SerializeField] private string description;
    [SerializeField] private Sprite icon;

    [Header("Logic")]
    [SerializeReferenceDropdown, SerializeReference] private List<OptionModule> modules;
    [SerializeField] private bool endsQuestline = true;
    [SerializeField] private List<FollowUpSeedConfig> followUpSeeds = new List<FollowUpSeedConfig>();

    private Seed seed;

    public string Title => string.IsNullOrWhiteSpace(title) ? name : title;
    public string Description => description;
    public Sprite Icon => icon;

    public void Initialize(Seed seed)
    {
        this.seed = seed;
    }

    public void ExecuteOption()
    {
        for (int i = 0; i < modules.Count; i++)
        {
            modules[i]?.Execute(seed);
        }

        ExecuteFollowUpSeeds();

        if (endsQuestline)
        {
            Debug.Log($"Option '{name}' ended the questline for seed '{seed?.Title ?? "Unknown"}'.");
        }
    }

    private void ExecuteFollowUpSeeds()
    {
        if (followUpSeeds == null || followUpSeeds.Count == 0) return;

        for (int i = 0; i < followUpSeeds.Count; i++)
        {
            FollowUpSeedConfig config = followUpSeeds[i];
            if (config.followUpSeed == null) continue;

            if (!TryResolveTargetNode(config, out Node targetNode))
            {
                Debug.LogWarning($"Option '{name}': could not resolve target node for follow-up seed '{config.followUpSeed.Title}'.");
                continue;
            }

            bool planted = targetNode.AddSeed(config.followUpSeed);
            if (planted)
            {
                Debug.Log($"Option '{name}': planted follow-up seed '{config.followUpSeed.Title}' on node '{targetNode.name}' (district: {targetNode.District}).");
            }
            else
            {
                Debug.LogWarning($"Option '{name}': skipped follow-up seed '{config.followUpSeed.Title}' because node '{targetNode.name}' is already occupied.");
            }
        }
    }

    private bool TryResolveTargetNode(FollowUpSeedConfig config, out Node targetNode)
    {
        targetNode = null;

        if (config.useCurrentSeedNode)
        {
            targetNode = seed?.CurrentNode;
            return targetNode != null;
        }

        if (config.useSpecificNode)
        {
            targetNode = config.specificNode;
            return targetNode != null;
        }

        if (config.randomInSameDistrict)
        {
            Districts? district = seed?.CurrentDistrict;
            if (!district.HasValue) return false;
            return DistrictsManager.TryGetRandomNodeInDistrict(district.Value, out targetNode);
        }

        return DistrictsManager.TryGetRandomNodeAnyDistrict(out targetNode);
    }

    [Serializable]
    private class FollowUpSeedConfig
    {
        [Header("Follow-up")]
        public Seed followUpSeed;

        [Header("Target selection")]
        public bool useCurrentSeedNode;
        public bool useSpecificNode;
        public Node specificNode;
        public bool randomInSameDistrict = true;
    }
}