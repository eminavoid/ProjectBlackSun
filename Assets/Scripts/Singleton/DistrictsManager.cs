using System.Collections.Generic;
using UnityEngine;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class DistrictsManager : Singleton<DistrictsManager>
{
    [SerializeField] private List<DistrictDataReference> districtsConfig = new List<DistrictDataReference>();

    private readonly Dictionary<Districts, DistrictData> districts = new Dictionary<Districts, DistrictData>();

    public static DistrictData GetDistrictData(DistrictZone zone)
    {
        return GetDistrictData(zone.District);
    }

    public static DistrictData GetDistrictData(Districts district)
    {
        return Instance.districts[district];
    }

    public static bool TryGetDistrictData(Districts district, out DistrictData data)
    {
        return Instance.districts.TryGetValue(district, out data) && data != null;
    }

    public static List<DistrictZone> GetDistrictZones(Districts district)
    {
        if (!TryGetDistrictData(district, out DistrictData data)) return new List<DistrictZone>();
        return data.GetZones();
    }

    public static bool TryGetRandomZoneInDistrict(Districts district, out DistrictZone zone)
    {
        zone = null;
        if (!TryGetDistrictData(district, out DistrictData data)) return false;
        return data.TryGetRandomZone(out zone);
    }

    public static bool TryGetRandomFreeZoneInDistrict(Districts district, out DistrictZone zone)
    {
        zone = null;
        if (!TryGetDistrictData(district, out DistrictData data)) return false;
        return data.TryGetRandomFreeZone(out zone);
    }

    public static bool TryGetRandomFreeZoneAnyDistrict(out DistrictZone zone)
    {
        zone = null;
        List<DistrictZone> freeZones = new List<DistrictZone>();

        foreach (Districts district in Enum.GetValues(typeof(Districts)))
        {
            List<DistrictZone> districtZones = GetDistrictZones(district);
            for (int i = 0; i < districtZones.Count; i++)
            {
                DistrictZone candidate = districtZones[i];
                if (candidate != null && !candidate.IsOccupied)
                {
                    freeZones.Add(candidate);
                }
            }
        }

        if (freeZones.Count == 0) return false;

        zone = freeZones[UnityEngine.Random.Range(0, freeZones.Count)];
        return zone != null;
    }

    public static DistrictData GetRandomDistrict()
    {
        if (TryGetRandomDistrictWithZones(out DistrictData data))
        {
            return data;
        }

        Array array = Enum.GetValues(typeof(Districts));
        int randomIndex = UnityEngine.Random.Range(0, array.Length);
        Districts randomDistrict = (Districts)array.GetValue(randomIndex);

        return Instance.districts[randomDistrict];
    }

    public static bool TryGetRandomDistrictWithZones(out DistrictData data)
    {
        data = null;
        if (IsNull) return false;

        List<DistrictData> candidates = new List<DistrictData>();

        foreach (KeyValuePair<Districts, DistrictData> entry in Instance.districts)
        {
            if (entry.Value != null && entry.Value.ZoneCount > 0)
            {
                candidates.Add(entry.Value);
            }
        }

        if (candidates.Count == 0) return false;

        data = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        return data != null;
    }

    public static void RefreshZones()
    {
        if (IsNull) return;
        Instance.RefreshZonesInternal();
    }

    protected override sealed void OnInitialization()
    {
        for (int i = 0; i < districtsConfig.Count; i++)
        {
            districts.Add(districtsConfig[i].district, districtsConfig[i].data);
        }

        RefreshZonesInternal();
    }

    private void RefreshZonesInternal()
    {
        foreach (DistrictData data in districts.Values)
        {
            data?.ClearZones();
        }

        DistrictZone[] zones = FindObjectsByType<DistrictZone>(FindObjectsSortMode.None);

        for (int i = 0; i < zones.Length; i++)
        {
            if (zones[i] == null) continue;
            if (!districts.TryGetValue(zones[i].District, out DistrictData data) || data == null) continue;
            data.AddZone(zones[i]);
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

        List<DistrictZone> zones = districtData.GetZones();
        if (zones.Count == 0) return;

        Vector3 districtCenter = Vector3.zero;

        for (int i = 0; i < zones.Count; i++)
        {
            if (zones[i] == null) continue;
            districtCenter += zones[i].transform.position;

            for (int x = 0; x < zones.Count; x++)
            {
                if (zones[i] == zones[x]) continue;
                Gizmos.DrawLine(zones[i].transform.position, zones[x].transform.position);
            }
        }

        districtCenter /= zones.Count;

        Handles.Label(districtCenter, districtData.name);
    }

#endif
}

public enum Districts
{
    District1,
    District2,
    District3,
    District4,
    District5,
    District6
}
