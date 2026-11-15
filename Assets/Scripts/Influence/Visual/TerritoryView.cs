using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DefaultExecutionOrder(40)]
public class TerritoryView : MonoBehaviour
{
    private const string TileName = "Territory";

    private enum Look
    {
        Plain = 0,
        Held = 1,
        Hatched = 2,
        Contested = 3
    }

    [SerializeField] private Material territoryMaterial;

    [Header("Opacity")]
    [SerializeField, Range(0f, 1f)] private float playerAlpha = 0.62f;
    [SerializeField, Range(0f, 1f)] private float rivalAlpha = 0.8f;
    [SerializeField, Range(0f, 1f)] private float contestedAlpha = 0.62f;
    [Tooltip("A faith with influence here that doesn't rule it yet.")]
    [SerializeField, Range(0f, 1f)] private float growingAlpha = 0.35f;
    [Tooltip("The selected zone's fill fades so its highlight shows through.")]
    [SerializeField, Range(0f, 1f)] private float selectedDim = 0.3f;

    [Header("Owner Change")]
    [Tooltip("How long the wave takes to sweep out of a zone that changes hands.")]
    [SerializeField, Min(0.1f)] private float flashSeconds = 1.4f;

    public static event Action<DistrictZone, FactionId?, FactionId?> OwnerChanged;

    private static readonly int ModeId = Shader.PropertyToID("_Mode");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int Color2Id = Shader.PropertyToID("_Color2");
    private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
    private static readonly int FlashId = Shader.PropertyToID("_Flash");
    private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
    private static readonly int FlashTId = Shader.PropertyToID("_FlashT");
    private static readonly int CenterId = Shader.PropertyToID("_Center");
    private static readonly int RadiusId = Shader.PropertyToID("_Radius");
    private static readonly int SeedId = Shader.PropertyToID("_Seed");

    private readonly Dictionary<DistrictZone, Tile> tiles = new Dictionary<DistrictZone, Tile>();
    private readonly List<FactionId> ranked = new List<FactionId>();
    private MaterialPropertyBlock block;
    private bool visible = true;

    private class Tile
    {
        public MeshRenderer Renderer;
        public Vector4 Center;
        public float Radius;
        public float Seed;
        public bool Known;
        public FactionId? Owner;
        public bool Present;
        public Look Look;
        public Color Primary;
        public Color Secondary;
        public float Alpha;
        public float FlashStart = -1f;
        public Color FlashColor;
    }

    public static Color MapColor(FactionId faction)
    {
        Color color = FactionPalette.For(faction);
        return FactionIdUtil.IsPlayer(faction) ? Color.Lerp(color, new Color(0.3f, 0.62f, 1f, 1f), 0.5f) : color;
    }

    private void Start()
    {
        block = new MaterialPropertyBlock();
        if (InfluenceManager.IsNull || territoryMaterial == null)
        {
            enabled = false;
            return;
        }

        Build();
        InfluenceManager.Get.OnControlChanged += Refresh;
        InfluenceManager.Get.OnInfluenceTurnResolved += Refresh;
        DistrictSelectionController.OnSelectionChanged += OnSelectionChanged;
        Refresh();
    }

    private void OnDestroy()
    {
        DistrictSelectionController.OnSelectionChanged -= OnSelectionChanged;
        if (InfluenceManager.IsNull) return;
        InfluenceManager.Get.OnControlChanged -= Refresh;
        InfluenceManager.Get.OnInfluenceTurnResolved -= Refresh;
    }

    private void Update()
    {
        if (ToggleRequested())
        {
            visible = !visible;
            foreach (Tile tile in tiles.Values) Apply(tile);
        }

        float now = Time.time;
        foreach (Tile tile in tiles.Values)
        {
            if (tile.FlashStart < 0f) continue;
            if (now - tile.FlashStart >= flashSeconds) tile.FlashStart = -1f;
            Apply(tile);
        }
    }

    private static bool ToggleRequested()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Tab);
#endif
    }

    private void Build()
    {
        IReadOnlyList<DistrictZone> zones = InfluenceManager.Get.GetPlayableZones();
        for (int i = 0; i < zones.Count; i++)
        {
            DistrictZone zone = zones[i];
            MeshRenderer created = CreateTile(zone);
            if (created == null) continue;

            Bounds bounds = zone.GetWorldBounds();
            tiles[zone] = new Tile
            {
                Renderer = created,
                Center = new Vector4(bounds.center.x, bounds.center.z, 0f, 0f),
                Radius = Mathf.Max(bounds.extents.x, bounds.extents.z),
                Seed = Mathf.Repeat(i * 0.618034f, 1f)
            };
        }
    }

    private MeshRenderer CreateTile(DistrictZone zone)
    {
        if (zone == null) return null;
        MeshFilter source = zone.GetComponent<MeshFilter>();
        if (source == null) source = zone.GetComponentInChildren<MeshFilter>();
        if (source == null || source.sharedMesh == null) return null;

        Transform existing = source.transform.Find(TileName);
        if (existing != null) Destroy(existing.gameObject);

        GameObject go = new GameObject(TileName);
        go.transform.SetParent(source.transform, false);
        go.layer = source.gameObject.layer;
        go.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;

        MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = territoryMaterial;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        meshRenderer.enabled = false;
        return meshRenderer;
    }

    private void OnSelectionChanged(Districts? district)
    {
        foreach (Tile tile in tiles.Values) Apply(tile);
    }

    private void Refresh()
    {
        foreach (KeyValuePair<DistrictZone, Tile> pair in tiles)
        {
            DistrictZone zone = pair.Key;
            Tile tile = pair.Value;
            ZoneInfluenceState state = zone.Influence;
            FactionId? owner = state != null && state.Status == ZoneControlStatus.Controlled ? state.Controller : null;

            if (tile.Known && owner != tile.Owner)
            {
                tile.FlashStart = Time.time;
                tile.FlashColor = owner.HasValue ? MapColor(owner.Value) : Color.white;
                OwnerChanged?.Invoke(zone, tile.Owner, owner);
            }

            tile.Known = true;
            tile.Owner = owner;
            Describe(tile, state, owner);
            Apply(tile);
        }
    }

    private void Describe(Tile tile, ZoneInfluenceState state, FactionId? owner)
    {
        ranked.Clear();
        tile.Present = state != null && state.HasAnyPresence;
        if (!tile.Present) return;

        foreach (FactionId faction in FactionIdUtil.All)
        {
            if (state.GetShare(faction) > 0 || state.GetClerics(faction) > 0) ranked.Add(faction);
        }

        ranked.Sort((a, b) => state.GetShare(b).CompareTo(state.GetShare(a)));

        if (owner.HasValue)
        {
            bool player = FactionIdUtil.IsPlayer(owner.Value);
            tile.Look = player ? Look.Held : Look.Hatched;
            tile.Primary = MapColor(owner.Value);
            tile.Alpha = player ? playerAlpha : rivalAlpha;
            return;
        }

        if (ranked.Count >= 2)
        {
            tile.Look = Look.Contested;
            tile.Primary = MapColor(ranked[0]);
            tile.Secondary = MapColor(ranked[1]);
            tile.Alpha = contestedAlpha;
            return;
        }

        bool playerOnly = FactionIdUtil.IsPlayer(ranked[0]);
        tile.Look = playerOnly ? Look.Held : Look.Hatched;
        tile.Primary = MapColor(ranked[0]);
        tile.Alpha = growingAlpha;
    }

    private void Apply(Tile tile)
    {
        if (tile.Renderer == null) return;

        float progress = FlashProgress(tile);
        float flash = progress >= 0f ? 1f - progress : 0f;
        bool show = visible && (tile.Present || flash > 0f);
        tile.Renderer.enabled = show;
        if (!show) return;

        DistrictZone selected = DistrictSelectionController.SelectedZone;
        float alpha = tile.Alpha;
        if (selected != null && tiles.TryGetValue(selected, out Tile selectedTile) && selectedTile == tile) alpha *= selectedDim;

        tile.Renderer.GetPropertyBlock(block);
        block.SetFloat(ModeId, (float)tile.Look);
        block.SetColor(ColorId, tile.Primary);
        block.SetColor(Color2Id, tile.Secondary);
        block.SetFloat(AlphaId, alpha);
        block.SetFloat(FlashId, flash);
        block.SetFloat(FlashTId, Mathf.Max(progress, 0f));
        block.SetColor(FlashColorId, tile.FlashColor);
        block.SetVector(CenterId, tile.Center);
        block.SetFloat(RadiusId, tile.Radius);
        block.SetFloat(SeedId, tile.Seed);
        tile.Renderer.SetPropertyBlock(block);
    }

    private float FlashProgress(Tile tile)
    {
        if (tile.FlashStart < 0f) return -1f;
        return Mathf.Clamp01((Time.time - tile.FlashStart) / flashSeconds);
    }
}
