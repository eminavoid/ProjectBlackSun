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

        /// <summary>
        /// True: el monto sale de DistrictZone.ProductionAmount y se reparte por % de influencia.
        /// False: camino viejo (el centro). No cambiar esa entrada.
        /// </summary>
        public bool usesPerZoneAmount;
    }

    /// <summary>
    /// floor(produccion * share / total), en enteros. Lo que no llega a 1 se pierde.
    /// </summary>
    public static int FloorShare(int production, int share, int total)
    {
        if (production <= 0 || share <= 0 || total <= 0) return 0;
        return (int)((long)production * share / total);
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
            primaryAmountPerZone = 10,
            influenceStat = PlayerStats.PlayerStat.Stewardship,
            usesPerZoneAmount = true
        },
        new ProductionEntry
        {
            partColorName = "Green",
            district = Districts.District3,
            primaryResource = Resource.Flock,
            primaryAmountPerZone = 10,
            influenceStat = PlayerStats.PlayerStat.Diplomacy,
            usesPerZoneAmount = true
        },
        new ProductionEntry
        {
            partColorName = "White",
            district = Districts.District6,
            primaryResource = Resource.Zeal,
            primaryAmountPerZone = 10,
            influenceStat = PlayerStats.PlayerStat.Learning,
            usesPerZoneAmount = true
        },
        new ProductionEntry
        {
            partColorName = "Red",
            district = Districts.District1,
            primaryResource = Resource.Materials,
            primaryAmountPerZone = 10,
            influenceStat = PlayerStats.PlayerStat.Aggresion,
            usesPerZoneAmount = true
        },
        new ProductionEntry
        {
            partColorName = "Purple",
            district = Districts.District5,
            // Centro: no tocar. Sigue el reparto anterior.
            primaryResource = Resource.Wealth,
            primaryAmountPerZone = 2,
            secondaryResource = Resource.Happiness,
            secondaryAmountPerZone = 1,
            influenceStat = PlayerStats.PlayerStat.Intrigue
        },
        new ProductionEntry
        {
            partColorName = "Pink",
            district = Districts.District2,
            primaryResource = Resource.Secrets,
            primaryAmountPerZone = 10,
            influenceStat = PlayerStats.PlayerStat.Intrigue,
            usesPerZoneAmount = true
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
