using UnityEngine;

/// <summary>
/// Selectable mesh zone. District comes from a parent DistrictPart, or from legacy name parsing (Red.001).
/// </summary>
[DisallowMultipleComponent]
public class DistrictZone : MonoBehaviour
{
    [SerializeField] private Districts district;

    public Districts District => district;

    public void SetDistrict(Districts value)
    {
        district = value;
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

    private void OnValidate()
    {
        DistrictPart part = GetComponentInParent<DistrictPart>();
        if (part != null) district = part.District;
    }
}
