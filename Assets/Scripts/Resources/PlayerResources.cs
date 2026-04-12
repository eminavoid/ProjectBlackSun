using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Player Resources", menuName = "PlayerResources", order = 1)]
public class PlayerResources : ScriptableObject
{
    public Dictionary<Resource, int> Resources { get; } = new Dictionary<Resource, int>()
    {
        { Resource.Gold, 0 },
        { Resource.Fervor, 0 },
        { Resource.Devouts, 0 },
        { Resource.Authority, 0 },
    };
}