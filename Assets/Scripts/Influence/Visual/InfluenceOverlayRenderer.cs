using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Capa de influencia sobre el mapa: clona el mesh de cada cuadra, lo eleva y lo pinta con
/// Custom/InfluenceOverlay muestreando el campo global de InfluenceFieldBaker.
/// Los sliders viven en InfluenceOverlaySettings (Resources) para no perderse al salir de Play.
/// </summary>
[DefaultExecutionOrder(30)]
public class InfluenceOverlayRenderer : MonoBehaviour
{
    private const string MaterialResourcePath = "Materials/InfluenceOverlay";
    private const string ShaderName = "Custom/InfluenceOverlay";
    private const string OverlayChildName = "InfluenceOverlay";

    [SerializeField] private InfluenceOverlaySettings settings;

    private readonly List<ZoneOverlay> overlays = new List<ZoneOverlay>();

    private InfluenceFieldBaker baker;
    private Material overlayMaterial;

    private int builtZoneCount = -1;
    private float zoneExtent = 1f;
    private bool rebakeQueued;
    private float transitionTimer;
    private bool transitioning;
    private bool visible = true;
    private float alpha;

    public InfluenceOverlaySettings Settings
    {
        get
        {
            EnsureSettings();
            return settings;
        }
    }

    public void QueueRebake()
    {
        rebakeQueued = true;
    }

    private void OnEnable()
    {
        EnsureSettings();
    }

    private void Start()
    {
        EnsureSettings();

        visible = settings.startVisible;
        alpha = visible ? 1f : 0f;

        overlayMaterial = ResolveMaterial();
        if (overlayMaterial == null)
        {
            Debug.LogWarning(
                $"InfluenceOverlayRenderer: no se encontró el shader '{ShaderName}'. Overlay desactivado.",
                this);
            enabled = false;
            return;
        }

        baker = new InfluenceFieldBaker(settings.fieldResolution);

        Rebake();
        baker.Publish(1f);
        ApplyVolumeSettings();
        ApplyAlpha();

        if (!InfluenceManager.IsNull)
        {
            InfluenceManager.Get.OnControlChanged += QueueRebake;
            InfluenceManager.Get.OnInfluenceTurnResolved += QueueRebake;
        }
    }

    private void OnDisable()
    {
        if (settings != null) settings.Persist();
    }

    private void OnDestroy()
    {
        if (settings != null) settings.Persist();

        if (!InfluenceManager.IsNull)
        {
            InfluenceManager.Get.OnControlChanged -= QueueRebake;
            InfluenceManager.Get.OnInfluenceTurnResolved -= QueueRebake;
        }

        baker?.Release();

        if (overlayMaterial != null) Destroy(overlayMaterial);
    }

    private void Update()
    {
        ReadToggleInput();
        ApplyVolumeSettings();

        float targetAlpha = visible ? 1f : 0f;
        if (!Mathf.Approximately(alpha, targetAlpha))
        {
            float step = settings.fadeSeconds > 0f ? Time.unscaledDeltaTime / settings.fadeSeconds : 1f;
            alpha = Mathf.MoveTowards(alpha, targetAlpha, step);
            ApplyAlpha();
        }

        if (rebakeQueued)
        {
            rebakeQueued = false;
            Rebake();
            transitionTimer = 0f;
            transitioning = settings.transitionSeconds > 0f;
            if (!transitioning) baker.Publish(1f);
        }

        if (!transitioning) return;

        transitionTimer += Time.deltaTime;
        float blend = Mathf.Clamp01(transitionTimer / settings.transitionSeconds);
        baker.Publish(blend);
        if (blend >= 1f) transitioning = false;
    }

    private void EnsureSettings()
    {
        if (settings != null) return;
        settings = InfluenceOverlaySettings.LoadOrCreate();
    }

    private void ReadToggleInput()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.tabKey.wasPressedThisFrame) visible = !visible;
#else
        if (Input.GetKeyDown(KeyCode.Tab)) visible = !visible;
#endif
    }

    private void ApplyAlpha()
    {
        if (overlayMaterial == null) return;
        overlayMaterial.SetFloat("_GlobalAlpha", alpha);

        for (int i = 0; i < overlays.Count; i++)
        {
            MeshRenderer meshRenderer = overlays[i].Renderer;
            if (meshRenderer == null) continue;
            meshRenderer.enabled = alpha > 0.001f && overlays[i].HasPresence;
        }
    }

    private void Rebake()
    {
        if (InfluenceManager.IsNull || settings == null) return;

        InfluenceManager manager = InfluenceManager.Get;
        IReadOnlyList<DistrictZone> zones = manager.GetPlayableZones();

        if (zones.Count != builtZoneCount) BuildOverlays();

        baker.Bake(zones, manager.Adjacency, new InfluenceFieldBaker.Settings
        {
            SplatRadiusScale = settings.splatRadiusScale,
            BridgeRadiusScale = settings.bridgeRadiusScale,
            BlurPasses = settings.blurPasses,
            BlurRadius = settings.blurRadius,
            ClericWeight = settings.clericWeight,
            MinPresence = settings.minPresence
        });

        RefreshVisibility();
    }

    private void RefreshVisibility()
    {
        for (int i = 0; i < overlays.Count; i++)
        {
            ZoneOverlay overlay = overlays[i];
            if (overlay.Zone == null || overlay.Renderer == null) continue;

            ZoneInfluenceState state = overlay.Zone.Influence;
            overlay.HasPresence = state != null && state.HasAnyPresence;
            overlays[i] = overlay;

            overlay.Renderer.enabled = overlay.HasPresence && alpha > 0.001f;
        }
    }

    private void BuildOverlays()
    {
        ClearOverlays();

        if (InfluenceManager.IsNull) return;

        IReadOnlyList<DistrictZone> zones = InfluenceManager.Get.GetPlayableZones();
        builtZoneCount = zones.Count;
        float extentSum = 0f;
        int extentCount = 0;

        for (int i = 0; i < zones.Count; i++)
        {
            DistrictZone zone = zones[i];
            if (zone == null || !zone.IsPlayable) continue;

            MeshRenderer created = CreateOverlayFor(zone);
            if (created == null) continue;

            overlays.Add(new ZoneOverlay { Zone = zone, Renderer = created });

            Vector3 extents = zone.GetWorldBounds().extents;
            extentSum += Mathf.Max(extents.x, extents.z);
            extentCount++;
        }

        zoneExtent = extentCount > 0 ? Mathf.Max(extentSum / extentCount, 0.01f) : 1f;
        ApplyVolumeSettings();
    }

    private void ApplyVolumeSettings()
    {
        if (overlayMaterial == null || settings == null) return;

        overlayMaterial.SetFloat("_Lift", zoneExtent * settings.volumeHeight);
        overlayMaterial.SetFloat("_BreathAmp", zoneExtent * settings.volumeBreath);
        overlayMaterial.SetFloat("_Intensity", settings.overlayIntensity);
        overlayMaterial.SetFloat("_PatternScale", settings.patternCellsPerZone / zoneExtent);
        overlayMaterial.SetFloat("_SmokeStrength", settings.smokeStrength);
        overlayMaterial.SetFloat("_SmokeScale", settings.smokeCellsPerZone / zoneExtent);
        overlayMaterial.SetFloat("_SmokeSpeed", settings.smokeSpeed);
    }

    private MeshRenderer CreateOverlayFor(DistrictZone zone)
    {
        MeshFilter sourceFilter = zone.GetComponent<MeshFilter>();
        if (sourceFilter == null) sourceFilter = zone.GetComponentInChildren<MeshFilter>();
        if (sourceFilter == null || sourceFilter.sharedMesh == null) return null;

        Transform existing = sourceFilter.transform.Find(OverlayChildName);
        if (existing != null) Destroy(existing.gameObject);

        GameObject go = new GameObject(OverlayChildName);
        go.transform.SetParent(sourceFilter.transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        go.layer = sourceFilter.gameObject.layer;

        MeshFilter filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = sourceFilter.sharedMesh;

        MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = overlayMaterial;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        meshRenderer.enabled = false;

        return meshRenderer;
    }

    private void ClearOverlays()
    {
        for (int i = 0; i < overlays.Count; i++)
        {
            MeshRenderer meshRenderer = overlays[i].Renderer;
            if (meshRenderer != null) Destroy(meshRenderer.gameObject);
        }

        overlays.Clear();
    }

    private static Material ResolveMaterial()
    {
        Material template = Resources.Load<Material>(MaterialResourcePath);
        if (template != null)
        {
            return new Material(template)
            {
                name = "InfluenceOverlay_Runtime",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        Shader shader = Shader.Find(ShaderName);
        if (shader == null) return null;

        return new Material(shader)
        {
            name = "InfluenceOverlay_Runtime",
            hideFlags = HideFlags.HideAndDontSave
        };
    }

    private struct ZoneOverlay
    {
        public DistrictZone Zone;
        public MeshRenderer Renderer;
        public bool HasPresence;
    }
}
