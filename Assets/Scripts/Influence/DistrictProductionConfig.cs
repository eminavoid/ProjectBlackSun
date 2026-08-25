using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "District Production Config", menuName = "Influence/District Production Config", order = 1)]
public class DistrictProductionConfig : ScriptableObject
{
    [Serializable]
    public struct ProductionEntry
    {
        public string partColorName;
        public Districts district;
        public bool isImperial;
        public Resource primaryResource;
        public int primaryAmountPerZone;
        public Resource secondaryResource;
        public int secondaryAmountPerZone;
        public PlayerStats.PlayerStat influenceStat;
    }

    [SerializeField] private float districtControlProductionMultiplier = 1.25f;
    [SerializeField] private float influenceStatScalePerPoint = 0.05f;
    [SerializeField] private List<ProductionEntry> entries = new List<ProductionEntry>
    {
        new ProductionEntry
        {
            partColorName = "Yellow",
            district = Districts.District4,
            primaryResource = Resource.Wealth,
            primaryAmountPerZone = 4,
            influenceStat = PlayerStats.PlayerStat.Stewardship
        },
        new ProductionEntry
        {
            partColorName = "Green",
            district = Districts.District3,
            primaryResource = Resource.Flock,
            primaryAmountPerZone = 3,
            influenceStat = PlayerStats.PlayerStat.Diplomacy
        },
        new ProductionEntry
        {
            partColorName = "White",
            district = Districts.District6,
            primaryResource = Resource.Zeal,
            primaryAmountPerZone = 3,
            influenceStat = PlayerStats.PlayerStat.Learning
        },
        new ProductionEntry
        {
            partColorName = "Red",
            district = Districts.District1,
            // Material pending → Flock + Wealth menor proporción
            primaryResource = Resource.Flock,
            primaryAmountPerZone = 1,
            secondaryResource = Resource.Wealth,
            secondaryAmountPerZone = 1,
            influenceStat = PlayerStats.PlayerStat.Aggresion
        },
        new ProductionEntry
        {
            partColorName = "Purple",
            district = Districts.District5,
            // Secrets pending → Wealth + Bliss
            primaryResource = Resource.Wealth,
            primaryAmountPerZone = 2,
            secondaryResource = Resource.Happiness,
            secondaryAmountPerZone = 1,
            influenceStat = PlayerStats.PlayerStat.Intrigue
        },
        new ProductionEntry
        {
            partColorName = "Blue",
            district = Districts.District2,
            isImperial = true,
            primaryResource = Resource.Wealth,
            primaryAmountPerZone = 0,
            influenceStat = PlayerStats.PlayerStat.None
        }
    };

    public float DistrictControlProductionMultiplier => districtControlProductionMultiplier;
    public float InfluenceStatScalePerPoint => influenceStatScalePerPoint;

    public bool TryGetEntry(Districts district, out ProductionEntry entry)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].district != district) continue;
            entry = entries[i];
            return true;
        }

        entry = default;
        return false;
    }

    public bool IsImperial(Districts district)
    {
        return TryGetEntry(district, out ProductionEntry entry) && entry.isImperial;
    }
}
