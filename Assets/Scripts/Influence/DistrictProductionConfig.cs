using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "District Production Config", menuName = "Influence/District Production Config", order = 1)]
public class DistrictProductionConfig : ScriptableObject
{
    [Serializable]
    public struct ResourceYield
    {
        public Resource resource;
        public int parts;
    }

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
        public ResourceYield[] yields;

        /// <summary>
        /// True: el monto sale de DistrictZone.ProductionAmount y se reparte por % de influencia.
        /// False: camino viejo (el centro). No cambiar esa entrada.
        /// </summary>
        public bool usesPerZoneAmount;
    }

    public struct ScaledYield
    {
        public Resource resource;
        public int amount;
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
            usesPerZoneAmount = true,
            yields = new[]
            {
                new ResourceYield { resource = Resource.Secrets, parts = 1 },
                new ResourceYield { resource = Resource.Wealth, parts = 2 }
            }
        },
        new ProductionEntry
        {
            partColorName = "Green",
            district = Districts.District3,
            primaryResource = Resource.Flock,
            primaryAmountPerZone = 10,
            influenceStat = PlayerStats.PlayerStat.Diplomacy,
            usesPerZoneAmount = true,
            yields = new[]
            {
                new ResourceYield { resource = Resource.Zeal, parts = 1 },
                new ResourceYield { resource = Resource.Flock, parts = 2 }
            }
        },
        new ProductionEntry
        {
            partColorName = "White",
            district = Districts.District6,
            primaryResource = Resource.Zeal,
            primaryAmountPerZone = 10,
            influenceStat = PlayerStats.PlayerStat.Learning,
            usesPerZoneAmount = true,
            yields = new[]
            {
                new ResourceYield { resource = Resource.Secrets, parts = 1 },
                new ResourceYield { resource = Resource.Zeal, parts = 2 }
            }
        },
        new ProductionEntry
        {
            partColorName = "Red",
            district = Districts.District1,
            primaryResource = Resource.Wealth,
            primaryAmountPerZone = 10,
            influenceStat = PlayerStats.PlayerStat.Aggresion,
            usesPerZoneAmount = true,
            yields = new[]
            {
                new ResourceYield { resource = Resource.Wealth, parts = 1 },
                new ResourceYield { resource = Resource.Flock, parts = 1 },
                new ResourceYield { resource = Resource.Zeal, parts = 1 }
            }
        },
        new ProductionEntry
        {
            partColorName = "Purple",
            district = Districts.District5,
            primaryResource = Resource.Secrets,
            primaryAmountPerZone = 10,
            influenceStat = PlayerStats.PlayerStat.Intrigue,
            usesPerZoneAmount = true,
            yields = new[]
            {
                new ResourceYield { resource = Resource.Secrets, parts = 1 },
                new ResourceYield { resource = Resource.Zeal, parts = 1 },
                new ResourceYield { resource = Resource.Wealth, parts = 1 },
                new ResourceYield { resource = Resource.Flock, parts = 1 }
            }
        },
        new ProductionEntry
        {
            partColorName = "Pink",
            district = Districts.District2,
            primaryResource = Resource.Secrets,
            primaryAmountPerZone = 10,
            influenceStat = PlayerStats.PlayerStat.Intrigue,
            usesPerZoneAmount = true,
            yields = new[]
            {
                new ResourceYield { resource = Resource.Wealth, parts = 1 },
                new ResourceYield { resource = Resource.Secrets, parts = 2 }
            }
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

    /// <summary>
    /// Parte la producción del nodo en los recursos del distrito. Con producción 10,
    /// 1:2 da 3 y 6, 1:1:1 da 3 cada uno, y cuatro partes iguales dan 2 cada una.
    /// </summary>
    public static void ScaleYields(ProductionEntry entry, int production, List<ScaledYield> into)
    {
        into.Clear();
        if (production <= 0) return;

        if (entry.yields != null && entry.yields.Length > 0)
        {
            int parts = 0;
            for (int i = 0; i < entry.yields.Length; i++)
            {
                parts += Mathf.Max(0, entry.yields[i].parts);
            }

            if (parts <= 0) return;

            for (int i = 0; i < entry.yields.Length; i++)
            {
                int share = Mathf.Max(0, entry.yields[i].parts);
                int amount = FloorShare(production, share, parts);
                if (amount <= 0) continue;
                into.Add(new ScaledYield { resource = entry.yields[i].resource, amount = amount });
            }

            return;
        }

        if (entry.usesPerZoneAmount)
        {
            into.Add(new ScaledYield { resource = entry.primaryResource, amount = production });
            return;
        }

        if (entry.primaryAmountPerZone > 0)
        {
            into.Add(new ScaledYield { resource = entry.primaryResource, amount = entry.primaryAmountPerZone });
        }

        if (entry.secondaryAmountPerZone > 0)
        {
            into.Add(new ScaledYield { resource = entry.secondaryResource, amount = entry.secondaryAmountPerZone });
        }
    }

    public static string FormatYields(List<ScaledYield> yields)
    {
        if (yields == null || yields.Count == 0) return "—";

        string line = yields[0].amount + " " + yields[0].resource;
        for (int i = 1; i < yields.Count; i++)
        {
            line += " + " + yields[i].amount + " " + yields[i].resource;
        }

        return line;
    }
}
