using UnityEngine;

/// <summary>
/// Sector jugable del mapa (mesh del distrito). Aquí se plantan seeds; reemplaza el antiguo Node.
/// </summary>
[DisallowMultipleComponent]
public class DistrictZone : MonoBehaviour
{
    private const string PlantedMaterialResourcePath = "Materials/SeedPlantedInvert";
    private const string PlantedShaderName = "Custom/SeedPlantedInvert";
    private const string SelectedMaterialResourcePath = "Materials/NodeSelectedShield";
    private const string SelectedShaderName = "Custom/NodeSelectedShield";

    [SerializeField] private Districts district;

    private static Material plantedTemplate;
    private static Material selectedTemplate;

    private Seed plantedSeed;
    private bool isSelected;
    private ZoneInfluenceState influence;
    private ZoneControlMarker controlMarker;

    private MeshRenderer cachedRenderer;
    private Material[] originalSharedMaterials;
    private Material[] runtimeVisualMaterials;

    public Districts District => district;
    public bool IsOccupied => plantedSeed != null;
    public bool IsSelected => isSelected;
    public Seed PlantedSeed => plantedSeed;
    public string SectorName => gameObject.name;
    public ZoneInfluenceState Influence => influence;

    /// <summary>
    /// True for playable cuadras under a DistrictPart. False for map props like Plane.122.
    /// </summary>
    public bool IsPlayable => GetComponentInParent<DistrictPart>() != null;

    public void SetDistrict(Districts value)
    {
        district = value;
    }

    /// <summary>Bounds en mundo del sector; cae al transform si el mesh no está disponible.</summary>
    public Bounds GetWorldBounds()
    {
        Collider col = GetComponent<Collider>();
        if (col != null) return col.bounds;

        Renderer meshRenderer = ResolveRenderer();
        if (meshRenderer != null) return meshRenderer.bounds;

        return new Bounds(transform.position, Vector3.one);
    }

    public void EnsureInfluenceState(int cap = ZoneInfluenceState.DefaultCap)
    {
        if (influence == null)
        {
            influence = new ZoneInfluenceState { Cap = cap };
        }
        else
        {
            influence.Cap = cap;
        }
    }

    public void EnsureControlMarker()
    {
        if (controlMarker == null)
        {
            controlMarker = GetComponent<ZoneControlMarker>();
            if (controlMarker == null) controlMarker = gameObject.AddComponent<ZoneControlMarker>();
        }
    }

    public void RefreshControlVisual()
    {
        if (!IsPlayable) return;
        EnsureControlMarker();
        if (influence != null) controlMarker.Refresh(influence);
    }

    public void SetSelected(bool selected)
    {
        if (selected && !IsPlayable) return;
        if (isSelected == selected) return;
        isSelected = selected;
        RefreshVisual();
    }

    public bool AddSeed(Seed seed)
    {
        if (plantedSeed != null) return false;
        if (seed == null) return false;

        seed.Initialize(this);
        plantedSeed = seed;

        // Drop selection so the planted shield becomes visible immediately.
        if (isSelected && DistrictSelectionController.SelectedZone == this)
        {
            DistrictSelectionController.SetSelectedDistrict(null, null, string.Empty, string.Empty);
        }
        else
        {
            RefreshVisual();
        }

        return true;
    }

    public void RemoveSeed(Seed seed)
    {
        if (plantedSeed != seed) return;
        plantedSeed = null;
        RefreshVisual();
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

        Mesh mesh = meshFilter.sharedMesh;

        if (TryUseMeshCollider(mesh))
        {
            return;
        }

        EnsureBoxColliderFromMesh(mesh);
    }

    private bool TryUseMeshCollider(Mesh mesh)
    {
        if (mesh == null) return false;

        if (TryGetComponent(out BoxCollider boxCollider))
        {
            if (Application.isPlaying) Destroy(boxCollider);
            else DestroyImmediate(boxCollider);
        }

        if (!TryGetComponent(out MeshCollider meshCollider))
        {
            meshCollider = gameObject.AddComponent<MeshCollider>();
        }

        // Imported meshes often work as colliders even when !isReadable.
        // Boxes overlap neighbors and steal clicks; only fall back if assignment fails.
        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = mesh;
        meshCollider.convex = false;
        meshCollider.isTrigger = false;
        meshCollider.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation
            | MeshColliderCookingOptions.EnableMeshCleaning
            | MeshColliderCookingOptions.WeldColocatedVertices;

        return meshCollider.sharedMesh != null;
    }

    private void EnsureBoxColliderFromMesh(Mesh mesh)
    {
        if (TryGetComponent(out MeshCollider meshCollider))
        {
            if (Application.isPlaying) Destroy(meshCollider);
            else DestroyImmediate(meshCollider);
        }

        if (!TryGetComponent(out BoxCollider boxCollider))
        {
            boxCollider = gameObject.AddComponent<BoxCollider>();
        }

        boxCollider.center = mesh.bounds.center;
        boxCollider.size = mesh.bounds.size;
        boxCollider.isTrigger = false;
    }

    private void RefreshVisual()
    {
        MeshRenderer targetRenderer = ResolveRenderer();
        if (targetRenderer == null) return;

        CaptureOriginalMaterialsIfNeeded(targetRenderer);
        DestroyRuntimeMaterials();

        // Selection highlight wins while active; planted shield returns after deselect.
        Material template = null;
        string label = null;
        if (isSelected)
        {
            template = ResolveSelectedTemplate();
            label = "Selected";
        }
        else if (plantedSeed != null)
        {
            template = ResolvePlantedTemplate();
            label = "Planted";
        }

        if (template == null)
        {
            if (originalSharedMaterials != null)
            {
                targetRenderer.sharedMaterials = originalSharedMaterials;
            }

            return;
        }

        if (originalSharedMaterials == null || originalSharedMaterials.Length == 0) return;

        runtimeVisualMaterials = new Material[originalSharedMaterials.Length];
        for (int i = 0; i < originalSharedMaterials.Length; i++)
        {
            Material source = originalSharedMaterials[i];
            Material instance = new Material(template)
            {
                name = $"{SectorName}_{label}_{i}"
            };
            CopyMaterialAppearance(source, instance);
            runtimeVisualMaterials[i] = instance;
        }

        targetRenderer.materials = runtimeVisualMaterials;
    }

    private void CaptureOriginalMaterialsIfNeeded(MeshRenderer targetRenderer)
    {
        if (originalSharedMaterials != null) return;
        if (targetRenderer == null) return;
        originalSharedMaterials = targetRenderer.sharedMaterials;
    }

    private void ClearVisualToOriginal()
    {
        DestroyRuntimeMaterials();

        MeshRenderer targetRenderer = ResolveRenderer();
        if (targetRenderer != null && originalSharedMaterials != null)
        {
            targetRenderer.sharedMaterials = originalSharedMaterials;
        }
    }

    private void DestroyRuntimeMaterials()
    {
        if (runtimeVisualMaterials == null) return;

        for (int i = 0; i < runtimeVisualMaterials.Length; i++)
        {
            if (runtimeVisualMaterials[i] == null) continue;
            if (Application.isPlaying) Destroy(runtimeVisualMaterials[i]);
            else DestroyImmediate(runtimeVisualMaterials[i]);
        }

        runtimeVisualMaterials = null;
    }

    private MeshRenderer ResolveRenderer()
    {
        if (cachedRenderer != null) return cachedRenderer;

        if (!TryGetComponent(out cachedRenderer))
        {
            cachedRenderer = GetComponentInChildren<MeshRenderer>();
        }

        return cachedRenderer;
    }

    private static Material ResolvePlantedTemplate()
    {
        if (plantedTemplate != null) return plantedTemplate;

        plantedTemplate = Resources.Load<Material>(PlantedMaterialResourcePath);
        if (plantedTemplate != null) return plantedTemplate;

        Shader shader = Shader.Find(PlantedShaderName);
        if (shader == null) return null;

        plantedTemplate = new Material(shader) { name = "SeedPlantedInvert_RuntimeTemplate" };
        return plantedTemplate;
    }

    private static Material ResolveSelectedTemplate()
    {
        if (selectedTemplate != null) return selectedTemplate;

        selectedTemplate = Resources.Load<Material>(SelectedMaterialResourcePath);
        if (selectedTemplate != null) return selectedTemplate;

        Shader shader = Shader.Find(SelectedShaderName);
        if (shader == null) return null;

        selectedTemplate = new Material(shader) { name = "NodeSelectedShield_RuntimeTemplate" };
        return selectedTemplate;
    }

    private static void CopyMaterialAppearance(Material source, Material destination)
    {
        if (destination == null) return;

        if (source == null)
        {
            if (destination.HasProperty("_BaseColor")) destination.SetColor("_BaseColor", Color.white);
            return;
        }

        Texture map = null;
        if (source.HasProperty("_BaseMap")) map = source.GetTexture("_BaseMap");
        if (map == null && source.HasProperty("_MainTex")) map = source.GetTexture("_MainTex");

        if (destination.HasProperty("_BaseMap") && map != null)
        {
            destination.SetTexture("_BaseMap", map);
            destination.SetTextureScale("_BaseMap", source.HasProperty("_BaseMap")
                ? source.GetTextureScale("_BaseMap")
                : source.GetTextureScale("_MainTex"));
            destination.SetTextureOffset("_BaseMap", source.HasProperty("_BaseMap")
                ? source.GetTextureOffset("_BaseMap")
                : source.GetTextureOffset("_MainTex"));
        }

        Color color = Color.white;
        if (source.HasProperty("_BaseColor")) color = source.GetColor("_BaseColor");
        else if (source.HasProperty("_Color")) color = source.GetColor("_Color");

        if (destination.HasProperty("_BaseColor")) destination.SetColor("_BaseColor", color);
    }

    private void OnEnable()
    {
        GameTime.OnTurnEnded += OnTurnEnded;
        if (plantedSeed != null || isSelected) RefreshVisual();
    }

    private void OnDisable()
    {
        GameTime.OnTurnEnded -= OnTurnEnded;
        ClearVisualToOriginal();
    }

    private void OnDestroy()
    {
        DestroyRuntimeMaterials();
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
