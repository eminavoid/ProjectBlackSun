using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Grafo de adyacencia entre DistrictZones. Se construye por proximidad de bounds.
/// </summary>
public class ZoneAdjacencyGraph
{
    private readonly Dictionary<DistrictZone, List<DistrictZone>> neighbors =
        new Dictionary<DistrictZone, List<DistrictZone>>();

    public IReadOnlyList<DistrictZone> GetNeighbors(DistrictZone zone)
    {
        if (zone == null || !neighbors.TryGetValue(zone, out List<DistrictZone> list))
        {
            return System.Array.Empty<DistrictZone>();
        }

        return list;
    }

    public void Rebuild(IReadOnlyList<DistrictZone> zones, float contactPadding = 0.35f)
    {
        neighbors.Clear();
        if (zones == null || zones.Count == 0) return;

        for (int i = 0; i < zones.Count; i++)
        {
            DistrictZone zone = zones[i];
            if (zone == null || !zone.IsPlayable) continue;
            neighbors[zone] = new List<DistrictZone>();
        }

        List<DistrictZone> keys = new List<DistrictZone>(neighbors.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            DistrictZone a = keys[i];
            Bounds boundsA = GetWorldBounds(a);
            boundsA.Expand(contactPadding);

            for (int j = i + 1; j < keys.Count; j++)
            {
                DistrictZone b = keys[j];
                Bounds boundsB = GetWorldBounds(b);
                boundsB.Expand(contactPadding);

                if (!boundsA.Intersects(boundsB)) continue;

                neighbors[a].Add(b);
                neighbors[b].Add(a);
            }
        }
    }

    private static Bounds GetWorldBounds(DistrictZone zone)
    {
        Collider col = zone.GetComponent<Collider>();
        if (col != null) return col.bounds;

        Renderer renderer = zone.GetComponentInChildren<Renderer>();
        if (renderer != null) return renderer.bounds;

        return new Bounds(zone.transform.position, Vector3.one);
    }
}
