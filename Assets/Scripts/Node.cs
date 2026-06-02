using System;
using UnityEngine;

/// <summary>
/// Obsoleto: el gameplay usa <see cref="DistrictZone"/> (sectores del mapa 3D).
/// </summary>
[Obsolete("Use DistrictZone on map sector meshes instead of Node.")]
public class Node : MonoBehaviour
{
    [field: SerializeField] public Districts District { get; private set; }

    private DistrictZone Zone => GetComponent<DistrictZone>();

    public void SetDistrict(Districts district)
    {
        District = district;
        if (Zone != null) Zone.SetDistrict(district);
    }

    public bool AddSeed(Seed seed)
    {
        DistrictZone zone = Zone;
        if (zone == null)
        {
            Debug.LogWarning($"Node on '{name}' has no DistrictZone; add DistrictZone or use map bootstrap.", this);
            return false;
        }

        return zone.AddSeed(seed);
    }

    public void RemoveSeed(Seed seed)
    {
        Zone?.RemoveSeed(seed);
    }
}
