using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stock de clérigos y recursos de economía por facción (tithe AI / player).
/// </summary>
public class FactionStock
{
    private readonly Dictionary<Resource, int> resources = new Dictionary<Resource, int>();
    private int clerics;

    public FactionId Faction { get; }

    public int Clerics
    {
        get => clerics;
        set => clerics = Mathf.Max(0, value);
    }

    public FactionStock(FactionId faction, int startingClerics = 0)
    {
        Faction = faction;
        clerics = Mathf.Max(0, startingClerics);

        foreach (Resource resource in System.Enum.GetValues(typeof(Resource)))
        {
            resources[resource] = 0;
        }
    }

    public int GetResource(Resource resource)
    {
        return resources.TryGetValue(resource, out int value) ? value : 0;
    }

    public void AddResource(Resource resource, int amount)
    {
        if (amount == 0) return;
        if (!resources.ContainsKey(resource)) resources[resource] = 0;
        resources[resource] = Mathf.Max(0, resources[resource] + amount);

        if (resource == Resource.Happiness)
        {
            resources[resource] = Mathf.Clamp(resources[resource], 0, 100);
        }
    }

    public bool TrySpendClerics(int amount)
    {
        if (amount <= 0) return true;
        if (clerics < amount) return false;
        clerics -= amount;
        return true;
    }

    public void ReturnClerics(int amount)
    {
        if (amount <= 0) return;
        clerics += amount;
    }
}
