using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Dibuja las jugadas que las IAs ya decidieron pero todavía no ejecutaron:
/// arcos de facción para movimientos de clérigos y marcadores descendentes para seeds.
/// </summary>
[DefaultExecutionOrder(65)]
public class AIIntentOverlay : MonoBehaviour
{
    private const string MaterialResourcePath = "Materials/IntentArrow";
    private const string ShaderName = "Custom/IntentArrow";

    [Header("Escala (relativa al tamaño de una cuadra)")]
    [SerializeField] private float liftFactor = 0.5f;
    [SerializeField] private float widthFactor = 0.34f;
    [SerializeField] private float arcHeightFactor = 0.9f;
    [SerializeField] private float dropHeightFactor = 2.2f;
    [SerializeField] private float ringRadiusFactor = 0.7f;
    [SerializeField] private float labelPaddingFactor = 0.5f;
    [SerializeField] private float labelScaleFactor = 0.06f;

    [Header("Forma")]
    [SerializeField] private float headRatio = 0.18f;
    [SerializeField] private float headWidthScale = 2.4f;
    [SerializeField] private float arcLengthInfluence = 0.22f;

    [Header("Visual")]
    [SerializeField] private bool startVisible = true;
    [SerializeField] private float fadeSeconds = 0.2f;

    private readonly List<IntentArrowView> views = new List<IntentArrowView>();

    private Transform root;
    private Material arrowMaterial;
    private AIIntentBoard board;
    private float referenceSize = 1f;
    private bool visible = true;
    private float alpha;
    private bool rebuildQueued;

    private void Start()
    {
        visible = startVisible;
        alpha = visible ? 1f : 0f;

        arrowMaterial = ResolveMaterial();
        if (arrowMaterial == null)
        {
            Debug.LogWarning(
                $"AIIntentOverlay: no se encontró el shader '{ShaderName}'. Flechas de intención desactivadas.",
                this);
            enabled = false;
            return;
        }

        // Raíz suelta en la escena: las mallas se construyen en world-space y no deben
        // heredar la escala del host (el mapa está escalado ~100x).
        GameObject rootObject = new GameObject("AIIntentArrows");
        root = rootObject.transform;

        ApplyAlpha();

        board = AIIntentBoard.Get;
        if (board != null) board.OnIntentsChanged += QueueRebuild;

        QueueRebuild();
    }

    private void OnDestroy()
    {
        if (board != null) board.OnIntentsChanged -= QueueRebuild;
        if (root != null) Destroy(root.gameObject);
        if (arrowMaterial != null) Destroy(arrowMaterial);
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

        if (!rebuildQueued) return;
        rebuildQueued = false;
        Rebuild();
    }

    private void QueueRebuild()
    {
        rebuildQueued = true;
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
        if (arrowMaterial != null) arrowMaterial.SetFloat("_Alpha", alpha);
        if (root != null) root.gameObject.SetActive(alpha > 0.001f);
    }

    private void Rebuild()
    {
        if (board == null) return;

        referenceSize = ComputeReferenceSize();

        IReadOnlyList<AIIntent> intents = board.Intents;
        int used = 0;

        for (int i = 0; i < intents.Count; i++)
        {
            AIIntent intent = intents[i];
            if (intent == null || !intent.IsValid) continue;

            IntentArrowView view = GetView(used);
            used++;

            Draw(view, intent);
        }

        for (int i = used; i < views.Count; i++)
        {
            views[i].SetVisible(false);
        }
    }

    private void Draw(IntentArrowView view, AIIntent intent)
    {
        view.SetVisible(true);

        float lift = referenceSize * liftFactor;
        Vector3 target = intent.Target.GetWorldBounds().center + Vector3.up * lift;
        Color color = intent.Color;

        IntentArrowView.ArrowStyle style = new IntentArrowView.ArrowStyle
        {
            Width = referenceSize * widthFactor,
            HeadRatio = headRatio,
            HeadWidthScale = headWidthScale,
            DropHeight = referenceSize * dropHeightFactor,
            RingRadius = referenceSize * ringRadiusFactor
        };

        bool hasOrigin = intent.Origin != null && intent.Origin != intent.Target;
        Vector3 origin = hasOrigin
            ? intent.Origin.GetWorldBounds().center + Vector3.up * lift
            : target;

        float labelPadding = referenceSize * labelPaddingFactor;
        float labelScale = referenceSize * labelScaleFactor;

        if (hasOrigin && (target - origin).sqrMagnitude > referenceSize * referenceSize * 0.04f)
        {
            float distance = Vector3.Distance(origin, target);
            style.ArcHeight = referenceSize * arcHeightFactor + distance * arcLengthInfluence;

            view.BuildArc(origin, target, color, style);
            view.SetLabel(
                LabelFor(intent),
                intent.LabelColor,
                (target - origin) * 0.5f + Vector3.up * (style.ArcHeight + labelPadding),
                labelScale);
            return;
        }

        view.BuildDrop(target, color, style);
        view.SetLabel(
            LabelFor(intent),
            intent.LabelColor,
            Vector3.up * (style.DropHeight + labelPadding),
            labelScale);
    }

    private static string LabelFor(AIIntent intent)
    {
        return intent.Kind == AIIntentKind.AssignClerics ? $"+{intent.Amount}" : "SEED";
    }

    private IntentArrowView GetView(int index)
    {
        while (views.Count <= index)
        {
            views.Add(IntentArrowView.Create(root, arrowMaterial));
        }

        return views[index];
    }

    /// <summary>Tamaño típico de una cuadra: todas las medidas del overlay se derivan de acá.</summary>
    private float ComputeReferenceSize()
    {
        if (InfluenceManager.IsNull) return referenceSize;

        IReadOnlyList<DistrictZone> zones = InfluenceManager.Get.GetPlayableZones();
        float sum = 0f;
        int count = 0;

        for (int i = 0; i < zones.Count; i++)
        {
            DistrictZone zone = zones[i];
            if (zone == null || !zone.IsPlayable) continue;

            Vector3 extents = zone.GetWorldBounds().extents;
            sum += Mathf.Max(extents.x, extents.z);
            count++;
        }

        return count > 0 ? Mathf.Max(sum / count, 0.01f) : referenceSize;
    }

    private static Material ResolveMaterial()
    {
        Material template = Resources.Load<Material>(MaterialResourcePath);
        if (template != null)
        {
            return new Material(template)
            {
                name = "IntentArrow_Runtime",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        Shader shader = Shader.Find(ShaderName);
        if (shader == null) return null;

        return new Material(shader)
        {
            name = "IntentArrow_Runtime",
            hideFlags = HideFlags.HideAndDontSave
        };
    }
}
