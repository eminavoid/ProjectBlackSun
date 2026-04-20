using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "District Data", menuName = "Districts/New District", order = 1)]
public class DistrictData : ScriptableObject
{
    [SerializeField] private List<Node> configuredNodes = new List<Node>();

    private readonly List<Node> runtimeNodes = new List<Node>();

    public Node GetRandomNode()
    {
        List<Node> nodes = GetNodes();
        if (nodes.Count == 0) return null;
        return nodes[Random.Range(0, nodes.Count)];
    }

    public List<Node> GetNodes()
    {
        if (runtimeNodes.Count > 0) return new List<Node>(runtimeNodes);
        return new List<Node>(configuredNodes);
    }

    public List<Node> GetConfiguredNodes()
    {
        return new List<Node>(configuredNodes);
    }

    public void ClearNodes()
    {
        runtimeNodes.Clear();
    }

    public void AddNode(Node node)
    {
        if (node == null) return;
        runtimeNodes.Add(node);
    }
}