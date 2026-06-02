using UnityEngine;

public class DistrictMapBootstrap : MonoBehaviour
{
    [SerializeField] private Transform mapRoot;
    [SerializeField] private DistrictColorMapping colorMapping;
    [SerializeField] private bool setupOnAwake;

    private void Awake()
    {
        if (setupOnAwake) SetupMap();
    }

    public void Configure(Transform root, DistrictColorMapping mapping)
    {
        mapRoot = root;
        colorMapping = mapping;
    }

    public void SetupMap()
    {
        if (mapRoot == null)
        {
            Debug.LogWarning("DistrictMapBootstrap: map root is not assigned.", this);
            return;
        }

        SetupDistrictParts(mapRoot);
        SetupZones(mapRoot);
    }

    private void SetupDistrictParts(Transform root)
    {
        Transform[] allTransforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform current = allTransforms[i];
            if (current == root) continue;
            if (colorMapping == null || !colorMapping.TryGetDistrictForPart(current.name, out Districts district)) continue;

            DistrictPart part = current.GetComponent<DistrictPart>();
            if (part == null) part = current.gameObject.AddComponent<DistrictPart>();

            part.SetDistrict(district);
        }
    }

    private void SetupZones(Transform root)
    {
        MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < meshFilters.Length; i++)
        {
            MeshFilter meshFilter = meshFilters[i];
            if (meshFilter == null || meshFilter.sharedMesh == null) continue;

            GameObject target = meshFilter.gameObject;
            DistrictZone zone = target.GetComponent<DistrictZone>();
            if (zone == null) zone = target.AddComponent<DistrictZone>();

            zone.ResolveDistrictFromHierarchy(colorMapping);

            DistrictPart parentPart = target.GetComponentInParent<DistrictPart>();
            if (parentPart == null && colorMapping != null &&
                !colorMapping.TryGetDistrictFromZoneName(target.name, out _))
            {
                continue;
            }

            zone.EnsureCollider();
        }
    }
}
