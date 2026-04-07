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