using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "District Data", menuName = "Districts/New District", order = 1)]
public class DistrictData : ScriptableObject
{
    private readonly List<DistrictZone> zones = new List<DistrictZone>();

    public int ZoneCount => zones.Count;

    public bool TryGetRandomZone(out DistrictZone zone)
    {
        zone = null;
        if (zones.Count == 0) return false;

        zone = zones[Random.Range(0, zones.Count)];
        return zone != null;
    }

    public bool TryGetRandomFreeZone(out DistrictZone zone)
    {
        zone = null;
        if (zones.Count == 0) return false;

        List<DistrictZone> freeZones = new List<DistrictZone>();
        for (int i = 0; i < zones.Count; i++)
        {
            DistrictZone candidate = zones[i];
            if (candidate != null && !candidate.IsOccupied)
            {
                freeZones.Add(candidate);
            }
        }

        if (freeZones.Count == 0) return false;

        zone = freeZones[Random.Range(0, freeZones.Count)];
        return zone != null;
    }

    public List<DistrictZone> GetZones()
    {
        return new List<DistrictZone>(zones);
    }

    public void ClearZones()
    {
        zones.Clear();
    }

    public void AddZone(DistrictZone zone)
    {
        if (zone == null) return;
        zones.Add(zone);
    }
}
