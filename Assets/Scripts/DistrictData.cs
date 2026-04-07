using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "District Data", menuName = "Districts/New District", order = 1)]
public class DistrictData : ScriptableObject
{
    private readonly List<Node> nodes = new List<Node>();

    public List<Node> GetNodes()
    {
        return new List<Node>(nodes);
    }

    public void ClearNodes()
    {
        nodes.Clear();
    }

    public void AddNode(Node node)
    {
        nodes.Add(node);
    }
}