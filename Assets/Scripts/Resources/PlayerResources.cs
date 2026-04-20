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
        { Resource.Happiness, 0 }
    };

    public Action<Resource, int> onResourceGained;

    public void AddResource(Resource resource, int amount)
    {
        resources[resource] += amount;
        onResourceGained?.Invoke(resource, amount);

        //Hardcoded cap

        int happiness = resources[Resource.Happiness];
        resources[Resource.Happiness] = Math.Clamp(happiness, 0, 100);
    }

    public int GetResourceAmount(Resource resource)
    {
        return resources[resource];
    }
}