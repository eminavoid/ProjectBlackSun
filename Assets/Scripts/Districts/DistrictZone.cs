using UnityEngine;

/// <summary>
/// Sector jugable del mapa (mesh del distrito). Aquí se plantan seeds; reemplaza el antiguo Node.
/// </summary>
[DisallowMultipleComponent]
public class DistrictZone : MonoBehaviour
{
    [SerializeField] private Districts district;

    private Seed plantedSeed;

    public Districts District => district;
    public bool IsOccupied => plantedSeed != null;
    public string SectorName => gameObject.name;

    public void SetDistrict(Districts value)
    {
        district = value;
    }

    public bool AddSeed(Seed seed)
    {
        if (plantedSeed != null) return false;
        if (seed == null) return false;

        seed.Initialize(this);
        plantedSeed = seed;
        return true;
    }

    public void RemoveSeed(Seed seed)
    {
        if (plantedSeed != seed) return;
        plantedSeed = null;
    }

    public void ResolveDistrictFromHierarchy(DistrictColorMapping mapping)
    {
        DistrictPart part = GetComponentInParent<DistrictPart>();
        if (part != null)
        {
            district = part.District;
            return;
        }

        if (mapping != null && mapping.TryGetDistrictFromZoneName(gameObject.name, out Districts fromName))
        {
            district = fromName;
        }
    }

    public void EnsureCollider()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null) meshFilter = GetComponentInChildren<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null) return;

        if (!TryGetComponent(out MeshCollider meshCollider))
        {
            meshCollider = gameObject.AddComponent<MeshCollider>();
        }

        meshCollider.sharedMesh = meshFilter.sharedMesh;
        meshCollider.convex = false;
        meshCollider.isTrigger = false;
    }

    private void OnEnable()
    {
        GameTime.OnTurnEnded += OnTurnEnded;
    }

    private void OnDisable()
    {
        GameTime.OnTurnEnded -= OnTurnEnded;
    }

    private void OnTurnEnded()
    {
        if (plantedSeed == null) return;
        plantedSeed.Tick();
    }

    private void OnValidate()
    {
        DistrictPart part = GetComponentInParent<DistrictPart>();
        if (part != null) district = part.District;
    }
}
