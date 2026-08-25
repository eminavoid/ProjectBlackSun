using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Capa de influencia sobre el mapa: clona el mesh de cada cuadra, lo eleva y lo pinta con
/// Custom/InfluenceOverlay muestreando el campo global de InfluenceFieldBaker.
/// </summary>
[DefaultExecutionOrder(30)]
public class InfluenceOverlayRenderer : MonoBehaviour
{
    private const string MaterialResourcePath = "Materials/InfluenceOverlay";
    private const string ShaderName = "Custom/InfluenceOverlay";
    private const string OverlayChildName = "InfluenceOverlay";

    [Header("Campo")]
    [SerializeField] private int fieldResolution = 160;
    [SerializeField] private float splatRadiusScale = 1.5f;
    [Tooltip("Radio del puente que fusiona cuadras vecinas del mismo distrito y mismo dueño.")]
    [SerializeField] private float bridgeRadiusScale = 1.15f;
    [SerializeField] private int blurPasses = 2;
    [SerializeField] private int blurRadius = 2;
    [Tooltip("Cuánta presencia aporta un clérigo estacionado, en puntos de influencia.")]
    [SerializeField] private float clericWeight = 0.6f;
    [Tooltip("Presencia mínima visible en una zona ocupada.")]
    [SerializeField] private float minPresence = 0.14f;

    [Header("Visual")]
    [Tooltip("Elevación sobre el mapa, en fracciones del tamaño de una cuadra.")]
    [SerializeField] private float liftFactor = 0.35f;
    [Tooltip("Celdas del patrón hexagonal a lo ancho de una cuadra.")]
    [SerializeField] private float patternCellsPerZone = 3.5f;
    [SerializeField] private float transitionSeconds = 0.6f;
    [SerializeField] private float fadeSeconds = 0.25f;
    [SerializeField] private bool startVisible = true;

    private readonly List<ZoneOverlay> overlays = new List<ZoneOverlay>();

    private InfluenceFieldBaker baker;
    private Material overlayMaterial;

    private int builtZoneCount = -1;
    private bool rebakeQueued;
    private float transitionTimer;
    private bool transitioning;
    private bool visible = true;
    private float alpha;

    public void QueueRebake()
    {
        rebakeQueued = true;
    }

    private void Start()
    {
        visible = startVisible;
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

        baker = new InfluenceFieldBaker(fieldResolution);

        Rebake();
        baker.Publish(1f);
        ApplyAlpha();

        if (!InfluenceManager.IsNull)
        {
            InfluenceManager.Get.OnControlChanged += QueueRebake;
            InfluenceManager.Get.OnInfluenceTurnResolved += QueueRebake;
        }
    }

    private void OnDestroy()
    {
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

        float targetAlpha = visible ? 1f : 0f;
        if (!Mathf.Approximately(alpha, targetAlpha))
        {
            float step = fadeSeconds > 0f ? Time.unscaledDeltaTime / fadeSeconds : 1f;
            alpha = Mathf.MoveTowards(alpha, targetAlpha, step);
            ApplyAlpha();
        }

        if (rebakeQueued)
        {
            rebakeQueued = false;
            Rebake();
            transitionTimer = 0f;
            transitioning = transitionSeconds > 0f;
            if (!transitioning) baker.Publish(1f);
        }

        if (!transitioning) return;

        transitionTimer += Time.deltaTime;
        float blend = Mathf.Clamp01(transitionTimer / transitionSeconds);
        baker.Publish(blend);
        if (blend >= 1f) transitioning = false;
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
        if (InfluenceManager.IsNull) return;

        InfluenceManager manager = InfluenceManager.Get;
        IReadOnlyList<DistrictZone> zones = manager.GetPlayableZones();

        // Comparar contra el conteo de zonas del último build: algunas cuadras pueden no
        // tener mesh y nunca generar overlay, así que overlays.Count no sirve como referencia.
        if (zones.Count != builtZoneCount) BuildOverlays();

        baker.Bake(zones, manager.Adjacency, new InfluenceFieldBaker.Settings
        {
            SplatRadiusScale = splatRadiusScale,
            BridgeRadiusScale = bridgeRadiusScale,
            BlurPasses = blurPasses,
            BlurRadius = blurRadius,
            ClericWeight = clericWeight,
            MinPresence = minPresence
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

        // El mapa está escalado ~100x: elevación y patrón se derivan del tamaño real de una cuadra.
        float zoneExtent = extentCount > 0 ? Mathf.Max(extentSum / extentCount, 0.01f) : 1f;
        overlayMaterial.SetFloat("_Lift", zoneExtent * liftFactor);
        overlayMaterial.SetFloat("_PatternScale", patternCellsPerZone / zoneExtent);
        overlayMaterial.SetFloat("_BreathAmp", zoneExtent * 0.05f);
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

    /// <summary>Siempre devuelve una instancia: el overlay escribe propiedades y no debe ensuciar el asset.</summary>
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
