using System.Collections.Generic;
using UnityEngine;
using System;

[CreateAssetMenu(fileName = "Player Resources", menuName = "PlayerResources", order = 1)]
public class PlayerResources : ScriptableObject
{
    private readonly Dictionary<Resource, int> resources = new Dictionary<Resource, int>()
    {
        { Resource.Wealth, 0 },
        { Resource.Zeal, 0 },
        { Resource.Flock, 0 },
        { Resource.Authority, 0 },
        { Resource.Happiness, 0 },
        { Resource.Materials, 0 },
        { Resource.Secrets, 0 },
    };

    public Action<Resource, int> onResourceGained;

    public void AddResource(Resource resource, int amount)
    {
        if (resource == Resource.Clerics)
        {
            if (InfluenceManager.IsNull) return;
            InfluenceManager.Get.GetStock(FactionId.Player).Clerics += amount;
            onResourceGained?.Invoke(resource, amount);
            return;
        }

        resources[resource] += amount;
        onResourceGained?.Invoke(resource, amount);

        //Hardcoded cap

        int happiness = resources[Resource.Happiness];
        resources[Resource.Happiness] = Math.Clamp(happiness, 0, 100);

        int flock = resources[Resource.Flock];
        resources[Resource.Flock] = Math.Clamp(flock, 0, int.MaxValue);

        resources[Resource.Authority] = Math.Max(0, resources[Resource.Authority]);
    }

    public void Clear()
    {
        foreach (Resource resource in new List<Resource>(resources.Keys)) resources[resource] = 0;
    }

    public int GetResourceAmount(Resource resource)
    {
        if (resource == Resource.Clerics) return InfluenceManager.IsNull ? 0 : InfluenceManager.Get.GetClericPool(FactionId.Player);
        return resources[resource];
    }

    public Dictionary<Resource, int> Snapshot()
    {
        return new Dictionary<Resource, int>(resources) { [Resource.Clerics] = GetResourceAmount(Resource.Clerics) };
    }

    public void GetChangesSince(Dictionary<Resource, int> snapshot, List<ResourceDelta> changes, bool asRefund = false)
    {
        foreach (KeyValuePair<Resource, int> pair in Snapshot())
        {
            snapshot.TryGetValue(pair.Key, out int before);
            if (pair.Value != before) ResourceDelta.Add(changes, pair.Key, pair.Value - before, asRefund);
        }
    }
}