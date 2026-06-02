using System.Collections.Generic;
using UnityEngine;
using System;

[CreateAssetMenu(fileName = "New Option", menuName = "Options/New Option", order = 1)]
public class Option : ScriptableObject
{
    [SerializeField] private PlayerStats playerStats;

    [Header("Display")]
    [SerializeField] private string title;
    [SerializeField, TextArea(3, 6)] private string description;
    [SerializeField] private Sprite icon;

    [Header("Logic")]
    [SerializeReferenceDropdown, SerializeReference] private List<OptionModule> modules;
    [SerializeField] private bool endsQuestline = true;
    [SerializeField] private List<FollowUpSeedConfig> followUpSeeds = new List<FollowUpSeedConfig>();

    public PlayerStats PlayerStats => playerStats;

    private Seed seed;

    public string Title => string.IsNullOrWhiteSpace(title) ? name : title;
    public string Description => description;
    public Sprite Icon => icon;

    public void Initialize(Seed seed)
    {
        this.seed = seed;
    }

    public bool CanExecute()
    {
        for (int i = 0; i < modules.Count; i++)
        {
            if (!modules[i].CanExecute())
            {
                return false;
            }
        }

        return true;
    }

    public void ExecuteOption()
    {
        for (int i = 0; i < modules.Count; i++)
        {
            modules[i]?.Execute(this, seed);
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

            if (!TryResolveTargetZone(config, out DistrictZone targetZone))
            {
                Debug.LogWarning($"Option '{name}': could not resolve target sector for follow-up seed '{config.followUpSeed.Title}'.");
                continue;
            }

            bool planted = targetZone.AddSeed(config.followUpSeed);
            if (planted)
            {
                Debug.Log($"Option '{name}': planted follow-up seed '{config.followUpSeed.Title}' on sector '{targetZone.SectorName}' (district: {targetZone.District}).");
            }
            else
            {
                Debug.LogWarning($"Option '{name}': skipped follow-up seed '{config.followUpSeed.Title}' because sector '{targetZone.SectorName}' is already occupied.");
            }
        }
    }

    private bool TryResolveTargetZone(FollowUpSeedConfig config, out DistrictZone targetZone)
    {
        targetZone = null;

        if (config.useCurrentSeedZone)
        {
            targetZone = seed?.CurrentZone;
            return targetZone != null;
        }

        if (config.useSpecificZone)
        {
            targetZone = config.specificZone;
            return targetZone != null;
        }

        if (config.randomInSameDistrict)
        {
            Districts? district = seed?.CurrentDistrict;
            if (!district.HasValue) return false;
            return DistrictsManager.TryGetRandomFreeZoneInDistrict(district.Value, out targetZone);
        }

        return DistrictsManager.TryGetRandomFreeZoneAnyDistrict(out targetZone);
    }

    [Serializable]
    private class FollowUpSeedConfig
    {
        [Header("Follow-up")]
        public Seed followUpSeed;

        [Header("Target selection")]
        public bool useCurrentSeedZone;
        public bool useSpecificZone;
        public DistrictZone specificZone;
        public bool randomInSameDistrict = true;
    }
}