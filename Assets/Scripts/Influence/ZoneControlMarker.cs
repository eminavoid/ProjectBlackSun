using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Stats de la zona seleccionada, pintados planos sobre el overlay.
/// </summary>
[DisallowMultipleComponent]
public class ZoneControlMarker : MonoBehaviour
{
    private const string OutlineMaterialPath = "Fonts & Materials/LiberationSans SDF - Outline";

    private DistrictZone zone;
    private TextMeshPro label;
    private Material labelMaterial;
    private InfluenceOverlaySettings settings;
    private string cachedText;
    private static float referenceExtent;

    private bool IsInspected => zone != null && DistrictSelectionController.SelectedZone == zone;

    public void Refresh(ZoneInfluenceState state)
    {
        zone = GetComponent<DistrictZone>();
        BuildText(state);
        ApplyText();
    }

    private void OnEnable()
    {
        zone = GetComponent<DistrictZone>();
        settings = InfluenceOverlaySettings.LoadOrCreate();
        BindIntentBoard();
        DistrictSelectionController.OnSelectionChanged -= OnSelectionChanged;
        DistrictSelectionController.OnSelectionChanged += OnSelectionChanged;
    }

    private void Start()
    {
        BindIntentBoard();
        if (zone != null) Refresh(zone.Influence);
    }

    private void BindIntentBoard()
    {
        if (AIIntentBoard.IsNull) return;
        AIIntentBoard.Get.OnIntentsChanged -= OnIntentsChanged;
        AIIntentBoard.Get.OnIntentsChanged += OnIntentsChanged;
    }

    private void OnDisable()
    {
        DistrictSelectionController.OnSelectionChanged -= OnSelectionChanged;
        if (!AIIntentBoard.IsNull)
        {
            AIIntentBoard.Get.OnIntentsChanged -= OnIntentsChanged;
        }
    }

    private void OnDestroy()
    {
        if (labelMaterial != null) Destroy(labelMaterial);
    }

    private void OnSelectionChanged(Districts? _)
    {
        if (zone != null) Refresh(zone.Influence);
    }

    private void OnIntentsChanged()
    {
        if (zone != null) Refresh(zone.Influence);
    }

    private void LateUpdate()
    {
        if (!IsInspected || string.IsNullOrEmpty(cachedText))
        {
            SetVisible(false);
            return;
        }

        EnsureLabel();
        ApplyText();
        SetVisible(true);
        Layout();
    }

    private void Layout()
    {
        if (zone == null) zone = GetComponent<DistrictZone>();
        if (zone == null || label == null) return;

        Bounds bounds = zone.GetWorldBounds();
        float extent = Mathf.Max(bounds.extents.x, bounds.extents.z, 0.01f);

        float overlayLift = 0f;
        if (settings != null)
        {
            overlayLift = extent * (settings.volumeHeight + settings.volumeBreath);
        }

        Vector3 worldPosition = bounds.center
            + Vector3.up * (bounds.extents.y + overlayLift + extent * 0.02f);

        float worldScale = ReferenceExtent() * (settings != null ? settings.detailFontSize * 2.4f : 0.1f);
        Vector3 lossy = transform.lossyScale;
        float sx = worldScale / Mathf.Max(Mathf.Abs(lossy.x), 0.0001f);
        float sy = worldScale / Mathf.Max(Mathf.Abs(lossy.y), 0.0001f);
        float sz = worldScale / Mathf.Max(Mathf.Abs(lossy.z), 0.0001f);

        label.transform.position = worldPosition;
        // Plano XZ, readable desde la cámara top-down (el +X local iba espejado).
        label.transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
        label.transform.localScale = new Vector3(-sx, sy, sz);
    }

    private static float ReferenceExtent()
    {
        if (referenceExtent > 0.01f) return referenceExtent;

        if (!InfluenceManager.IsNull)
        {
            IReadOnlyList<DistrictZone> zones = InfluenceManager.Get.GetPlayableZones();
            float sum = 0f;
            int count = 0;

            for (int i = 0; i < zones.Count; i++)
            {
                DistrictZone candidate = zones[i];
                if (candidate == null || !candidate.IsPlayable) continue;

                Vector3 extents = candidate.GetWorldBounds().extents;
                sum += Mathf.Max(extents.x, extents.z);
                count++;
            }

            if (count > 0) referenceExtent = sum / count;
        }

        if (referenceExtent < 0.01f) referenceExtent = 1f;
        return referenceExtent;
    }

    private void BuildText(ZoneInfluenceState state)
    {
        cachedText = string.Empty;
        Color unused = Color.white;

        StringBuilder builder = new StringBuilder(96);
        AppendControl(state, builder, ref unused);
        AppendInfluence(state, builder);
        AppendTurnAction(builder, true, ref unused);

        cachedText = builder.ToString();
    }

    private static void AppendControl(ZoneInfluenceState state, StringBuilder builder, ref Color color)
    {
        if (state == null || !state.HasAnyPresence)
        {
            builder.Append("Libre");
            return;
        }

        if (state.Status == ZoneControlStatus.Controlled && state.Controller.HasValue)
        {
            FactionId controller = state.Controller.Value;
            color = FactionPalette.For(controller);
            builder.Append("Domina: ").Append(FactionIdUtil.DisplayName(controller));
            return;
        }

        FactionId? leader = LeadingFaction(state);
        if (leader.HasValue)
        {
            color = FactionPalette.For(leader.Value);
            builder.Append("Lidera: ")
                .Append(FactionIdUtil.DisplayName(leader.Value))
                .Append(" (en disputa)");
            return;
        }

        builder.Append("En disputa");
    }

    private static void AppendInfluence(ZoneInfluenceState state, StringBuilder builder)
    {
        if (state == null || state.TotalInfluence <= 0)
        {
            builder.Append("\nInfluencia: 0");
            return;
        }

        builder.Append("\nInfluencia: ")
            .Append(state.TotalInfluence)
            .Append('/')
            .Append(state.Cap);

        FactionId? leader = state.Status == ZoneControlStatus.Controlled && state.Controller.HasValue
            ? state.Controller
            : LeadingFaction(state);

        if (leader.HasValue)
        {
            builder.Append("  ")
                .Append(Mathf.RoundToInt(state.GetSharePercent(leader.Value)))
                .Append('%');
        }
    }

    private void AppendTurnAction(StringBuilder builder, bool showEmpty, ref Color color)
    {
        if (zone == null || AIIntentBoard.IsNull)
        {
            if (showEmpty) builder.Append("\nEste turno: —");
            return;
        }

        IReadOnlyList<AIIntent> intents = AIIntentBoard.Get.Intents;
        bool any = false;

        for (int i = 0; i < intents.Count; i++)
        {
            AIIntent intent = intents[i];
            if (intent == null || intent.Target != zone) continue;

            builder.Append(any ? "\n" : "\nEste turno: ");
            any = true;
            builder.Append(DescribeIntent(intent));

            if (intent.Faction.HasValue && color == Color.white)
            {
                color = FactionPalette.For(intent.Faction.Value);
            }
        }

        if (!any && showEmpty) builder.Append("\nEste turno: —");
    }

    private static string DescribeIntent(AIIntent intent)
    {
        string who = intent.Faction.HasValue
            ? FactionIdUtil.DisplayName(intent.Faction.Value)
            : "IA";

        if (intent.Kind == AIIntentKind.PlantSeed)
        {
            string seed = string.IsNullOrEmpty(intent.Label) ? "una seed" : intent.Label;
            return who + " plantan " + seed;
        }

        string unit = intent.Amount == 1 ? "clérigo" : "clérigos";
        return who + " +" + intent.Amount + " " + unit;
    }

    private static FactionId? LeadingFaction(ZoneInfluenceState state)
    {
        if (state == null) return null;

        FactionId? best = null;
        int bestShare = 0;
        bool tie = false;

        foreach (FactionId faction in state.FactionsWithShare())
        {
            int share = state.GetShare(faction);
            if (share > bestShare)
            {
                bestShare = share;
                best = faction;
                tie = false;
            }
            else if (share == bestShare)
            {
                tie = true;
            }
        }

        return tie ? null : best;
    }

    private void ApplyText()
    {
        string text = cachedText ?? string.Empty;
        if (label != null && label.text != text) label.text = text;
    }

    private void EnsureLabel()
    {
        if (label != null) return;

        Transform leftover = transform.Find("ControlMarkerOutline");
        if (leftover != null) Destroy(leftover.gameObject);
        leftover = transform.Find("ControlMarker");
        if (leftover != null) Destroy(leftover.gameObject);

        GameObject go = new GameObject("ControlMarker");
        go.transform.SetParent(transform, false);

        label = go.AddComponent<TextMeshPro>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontStyle = FontStyles.Bold;
        label.fontSize = 8f;
        label.color = Color.white;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Overflow;
        label.rectTransform.sizeDelta = new Vector2(28f, 14f);
        label.text = string.Empty;

        Material outline = Resources.Load<Material>(OutlineMaterialPath);
        if (outline != null)
        {
            labelMaterial = new Material(outline)
            {
                name = "ZoneControlMarker_Outline",
                hideFlags = HideFlags.HideAndDontSave
            };
            labelMaterial.EnableKeyword("OUTLINE_ON");
            labelMaterial.SetColor(Shader.PropertyToID("_FaceColor"), Color.white);
            labelMaterial.SetColor(Shader.PropertyToID("_OutlineColor"), Color.black);
            labelMaterial.SetFloat(Shader.PropertyToID("_OutlineWidth"), 0.22f);
            labelMaterial.renderQueue = 3001;
            label.fontMaterial = labelMaterial;
        }

        label.outlineColor = Color.black;
        label.outlineWidth = 0.22f;

        MeshRenderer meshRenderer = go.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }
    }

    private void SetVisible(bool visible)
    {
        if (label != null) label.gameObject.SetActive(visible);
    }
}
