using System.Collections.Generic;
using UnityEngine;
using System;

using UnityEditor;

public class DistrictsManager : Singleton<DistrictsManager>
{
    [SerializeField] private List<DistrictDataReference> districtsConfig = new List<DistrictDataReference>();

    private readonly Dictionary<Districts, DistrictData> districts = new Dictionary<Districts, DistrictData>();

    public static DistrictData GetDistrictData(Node node)
    {
        return GetDistrictData(node.District);
    }

    public static DistrictData GetDistrictData(Districts district)
    {
        return Instance.districts[district];
    }

    public static bool TryGetDistrictData(Districts district, out DistrictData data)
    {
        return Instance.districts.TryGetValue(district, out data) && data != null;
    }

    public static List<Node> GetDistrictNodes(Districts district)
    {
        if (!TryGetDistrictData(district, out DistrictData data)) return new List<Node>();
        return data.GetNodes();
    }

    public static bool TryGetRandomNodeInDistrict(Districts district, out Node node)
    {
        node = null;
        List<Node> nodes = GetDistrictNodes(district);
        if (nodes.Count == 0) return false;

        int randomIndex = UnityEngine.Random.Range(0, nodes.Count);
        node = nodes[randomIndex];
        return node != null;
    }

    public static bool TryGetRandomNodeAnyDistrict(out Node node)
    {
        node = null;
        List<Node> allNodes = new List<Node>();

        foreach (Districts district in Enum.GetValues(typeof(Districts)))
        {
            List<Node> districtNodes = GetDistrictNodes(district);
            for (int i = 0; i < districtNodes.Count; i++)
            {
                if (districtNodes[i] == null) continue;
                allNodes.Add(districtNodes[i]);
            }
        }

        if (allNodes.Count == 0) return false;

        int randomIndex = UnityEngine.Random.Range(0, allNodes.Count);
        node = allNodes[randomIndex];
        return node != null;
    }

    public static DistrictData GetRandomDistrict()
    {
        Array array = Enum.GetValues(typeof(Districts));
        int randomIndex = UnityEngine.Random.Range(0, array.Length);
        Districts randomDistrict = (Districts)array.GetValue(randomIndex);

        return Instance.districts[randomDistrict];
    }

    protected override sealed void OnInitialization()
    {
        for (int i = 0; i < districtsConfig.Count; i++)
        {
            districts.Add(districtsConfig[i].district, districtsConfig[i].data);
            if (districtsConfig[i].data != null) districtsConfig[i].data.ClearNodes();
        }

        Node[] nodes = FindObjectsByType<Node>(FindObjectsSortMode.None);

        for (int i = 0; i < nodes.Length; i++)
        {
            districts[nodes[i].District].AddNode(nodes[i]);
        }
    }

    [Serializable]
    private class DistrictDataReference
    {
        public Districts district;
        public DistrictData data;

        public DistrictDataReference() { }

        public DistrictDataReference(Districts district, DistrictData data)
        {
            this.district = district;
            this.data = data;
        }
    }

    private void OnValidate()
    {
        foreach (Districts district in Enum.GetValues(typeof(Districts)))
        {
            bool foundDistrictInList = false;

            for (int i = 0; i < districtsConfig.Count; i++)
            {
                if (districtsConfig[i].district == district)
                {
                    foundDistrictInList = true;
                    break;
                }
            }

            if (!foundDistrictInList)
            {
                districtsConfig.Add(new DistrictDataReference(district, null));
            }
        }
    }

#if UNITY_EDITOR

    private void OnDrawGizmos()
    {
        DrawDistrictsConnections();
    }

    private void DrawDistrictsConnections()
    {
        foreach (Districts district in districts.Keys)
        {
            if (districts[district] == null) continue;
            DrawDistrictConnections(districts[district]);
        }
    }

    private void DrawDistrictConnections(DistrictData districtData)
    {
        Gizmos.color = Color.yellow;

        List<Node> nodes = districtData.GetNodes();
        Vector3 districtCenter = Vector3.zero;

        for (int i = 0; i < nodes.Count; i++)
        {
            districtCenter += nodes[i].transform.position;

            for (int x = 0; x < nodes.Count; x++)
            {
                if (nodes[i] == nodes[x]) continue;
                Gizmos.DrawLine(nodes[i].transform.position, nodes[x].transform.position);
            }
        }

        districtCenter /= nodes.Count;

        Handles.Label(districtCenter, districtData.name);
    }

#endif
}

public enum Districts
{
    District1,
    District2,
    District3,
    District4
}