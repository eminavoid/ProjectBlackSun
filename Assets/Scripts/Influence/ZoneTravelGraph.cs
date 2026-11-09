using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.Rendering;
#endif

public enum TravelEdgeKind
{
    IntraDistrict,
    CrossDistrict
}

/// <summary>
/// Contact between two zone sides. Baked in the editor; rivals spend action points along these edges.
/// </summary>
[System.Serializable]
public class ZoneTravelEdge
{
    public DistrictZone a;
    public DistrictZone b;
    public float gap;
    public TravelEdgeKind kind;
    public Vector3 contactA;
    public Vector3 contactB;

    public DistrictZone Other(DistrictZone zone)
    {
        if (zone == a) return b;
        if (zone == b) return a;
        return null;
    }
}

/// <summary>
/// One touch stored on a zone. The other zone keeps the matching entry.
/// </summary>
[System.Serializable]
public class ZoneTravelLink
{
    public DistrictZone other;
    public float gap;
    public TravelEdgeKind kind;
    public Vector3 contactOnThis;
    public Vector3 contactOnOther;
}

/// <summary>
/// Distances between touching zone sides. Influence spill keeps using ZoneAdjacencyGraph.
/// </summary>
public class ZoneTravelGraph : MonoBehaviour
{
    [SerializeField] private float touchThreshold = 1.25f;
    [SerializeField] private float minOverlap = 0.45f;
    [SerializeField] private float lateralNormalLimit = 0.45f;
    [SerializeField] private float gapWeight = 0.15f;
    [SerializeField] private int borderSurcharge = 2;
    [SerializeField] private bool showOverlay = true;
    [SerializeField] private bool showZoneNames = true;
    [HideInInspector]
    [SerializeField] private List<ZoneTravelEdge> edges = new List<ZoneTravelEdge>();

    private static int linkVersion;

    private readonly List<ZoneTravelEdge> fallbackEdges = new List<ZoneTravelEdge>();
    private readonly Dictionary<DistrictZone, List<ZoneTravelEdge>> neighbors =
        new Dictionary<DistrictZone, List<ZoneTravelEdge>>();

    private bool cacheReady;
    private bool usingFallback;
    private bool warnedFallback;
    private int builtVersion = -1;

    public static void NotifyLinksChanged()
    {
        linkVersion++;
    }

    public bool HasLegacyEdgeList => HasUsableBake();
    public IReadOnlyList<ZoneTravelEdge> LegacyEdges => edges;

    public float TouchThreshold => Mathf.Max(0f, touchThreshold);
    public float MinOverlap => Mathf.Max(0f, minOverlap);
    public float LateralNormalLimit => Mathf.Clamp(lateralNormalLimit, 0f, 0.99f);
    public int BorderSurcharge => Mathf.Max(0, borderSurcharge);
    public IReadOnlyList<ZoneTravelEdge> Edges => HasUsableBake() ? edges : fallbackEdges;

    private struct PathState
    {
        public int hops;
        public float weight;
        public bool crossed;
    }

    private void OnValidate()
    {
        cacheReady = false;
    }

    private void Start()
    {
        EnsureReady();
    }

    public void ClearStoredEdges()
    {
        edges = new List<ZoneTravelEdge>();
        cacheReady = false;
        EnsureReady();
    }

    public void EnsureReady()
    {
        if (cacheReady && builtVersion == linkVersion) return;

        if (TryBuildCacheFromNodes())
        {
            usingFallback = false;
            builtVersion = linkVersion;
            cacheReady = true;
            return;
        }

        if (TryBuildCacheFromCatalog())
        {
            usingFallback = false;
            builtVersion = linkVersion;
            cacheReady = true;
            return;
        }

        if (HasUsableBake())
        {
            BuildCache(edges);
            usingFallback = false;
            builtVersion = linkVersion;
            cacheReady = true;
            return;
        }

        BuildFallback();
        usingFallback = true;
        builtVersion = linkVersion;
        cacheReady = true;
    }

    /// <summary>
    /// Action points to affect each reachable zone from territory this faction already has.
    /// Owned zones cost 1. A path of N contact hops costs N², plus the border surcharge
    /// when that path enters the target district without an owned neighbor there.
    /// </summary>
    public void FillActionCosts(FactionId faction, Dictionary<DistrictZone, int> costs)
    {
        costs.Clear();
        EnsureReady();

        List<DistrictZone> sources = new List<DistrictZone>();
        if (!InfluenceManager.IsNull)
        {
            IReadOnlyList<DistrictZone> zones = InfluenceManager.Get.GetPlayableZones();
            for (int i = 0; i < zones.Count; i++)
            {
                if (HasPresence(zones[i], faction)) sources.Add(zones[i]);
            }
        }
        else
        {
            foreach (KeyValuePair<DistrictZone, List<ZoneTravelEdge>> pair in neighbors)
            {
                if (pair.Key != null && HasPresence(pair.Key, faction)) sources.Add(pair.Key);
            }
        }

        if (sources.Count == 0) return;

        Dictionary<DistrictZone, PathState> best = new Dictionary<DistrictZone, PathState>();
        List<DistrictZone> open = new List<DistrictZone>();
        for (int i = 0; i < sources.Count; i++)
        {
            best[sources[i]] = new PathState();
            open.Add(sources[i]);
        }

        HashSet<DistrictZone> settled = new HashSet<DistrictZone>();
        while (open.Count > 0)
        {
            int pick = 0;
            float pickWeight = best[open[0]].weight;
            for (int i = 1; i < open.Count; i++)
            {
                float weight = best[open[i]].weight;
                if (weight >= pickWeight) continue;
                pickWeight = weight;
                pick = i;
            }

            DistrictZone current = open[pick];
            open.RemoveAt(pick);
            if (!settled.Add(current)) continue;
            if (!neighbors.TryGetValue(current, out List<ZoneTravelEdge> links)) continue;

            PathState state = best[current];
            for (int i = 0; i < links.Count; i++)
            {
                ZoneTravelEdge edge = links[i];
                DistrictZone next = edge != null ? edge.Other(current) : null;
                if (next == null || settled.Contains(next)) continue;

                float step = 1f + Mathf.Max(0f, edge.gap) * Mathf.Max(0f, gapWeight);
                float weight = state.weight + step;
                if (best.TryGetValue(next, out PathState known) && known.weight <= weight) continue;

                bool crossed = state.crossed
                    || edge.kind == TravelEdgeKind.CrossDistrict
                    || next.District != current.District;

                best[next] = new PathState
                {
                    hops = state.hops + 1,
                    weight = weight,
                    crossed = crossed
                };
                open.Add(next);
            }
        }

        foreach (KeyValuePair<DistrictZone, PathState> pair in best)
        {
            DistrictZone zone = pair.Key;
            if (zone == null) continue;

            int cost;
            if (pair.Value.hops <= 0)
            {
                cost = 1;
            }
            else
            {
                cost = pair.Value.hops * pair.Value.hops;
                if (pair.Value.crossed && !HasFoothold(zone, faction)) cost += BorderSurcharge;
            }

            costs[zone] = Mathf.Max(1, cost);
        }
    }

    private bool HasUsableBake()
    {
        if (edges == null) return false;
        for (int i = 0; i < edges.Count; i++)
        {
            ZoneTravelEdge edge = edges[i];
            if (edge != null && edge.a != null && edge.b != null) return true;
        }

        return false;
    }

    private bool TryBuildCacheFromNodes()
    {
        DistrictZone[] zones = FindObjectsByType<DistrictZone>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        neighbors.Clear();
        HashSet<LinkPair> seen = new HashSet<LinkPair>();
        bool any = false;

        for (int i = 0; i < zones.Length; i++)
        {
            DistrictZone zone = zones[i];
            if (zone == null || !zone.IsPlayable) continue;

            IReadOnlyList<ZoneTravelLink> links = zone.TravelLinks;
            for (int n = 0; n < links.Count; n++)
            {
                ZoneTravelLink link = links[n];
                if (link == null || link.other == null || link.other == zone) continue;

                any = true;
                LinkPair pair = new LinkPair(zone.GetEntityId(), link.other.GetEntityId());
                if (!seen.Add(pair)) continue;

                ZoneTravelEdge edge = new ZoneTravelEdge
                {
                    a = zone,
                    b = link.other,
                    gap = link.gap,
                    kind = link.kind,
                    contactA = link.contactOnThis,
                    contactB = link.contactOnOther
                };
                AddNeighbor(zone, edge);
                AddNeighbor(link.other, edge);
            }
        }

        return any;
    }

    private bool TryBuildCacheFromCatalog()
    {
        ZoneTravelCatalog catalog = Resources.Load<ZoneTravelCatalog>("ZoneTravelCatalog");
        if (catalog == null || catalog.Links == null || catalog.Links.Count == 0) return false;

        DistrictZone[] zones = FindObjectsByType<DistrictZone>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Dictionary<string, DistrictZone> byName = new Dictionary<string, DistrictZone>();
        for (int i = 0; i < zones.Length; i++)
        {
            DistrictZone zone = zones[i];
            if (zone == null || !zone.IsPlayable) continue;
            byName[zone.gameObject.name] = zone;
        }

        Dictionary<DistrictZone, List<ZoneTravelLink>> buckets = new Dictionary<DistrictZone, List<ZoneTravelLink>>();
        IReadOnlyList<ZoneTravelCatalog.Record> records = catalog.Links;
        for (int i = 0; i < records.Count; i++)
        {
            ZoneTravelCatalog.Record record = records[i];
            if (record == null) continue;
            if (!byName.TryGetValue(record.fromZone, out DistrictZone from)) continue;
            if (!byName.TryGetValue(record.toZone, out DistrictZone to)) continue;
            if (from == to) continue;

            AddCatalogLink(buckets, from, to, record.gap, record.kind);
            AddCatalogLink(buckets, to, from, record.gap, record.kind);
        }

        if (buckets.Count == 0) return false;

        foreach (KeyValuePair<DistrictZone, List<ZoneTravelLink>> pair in buckets)
        {
            pair.Key.SetTravelLinks(pair.Value);
        }

        return TryBuildCacheFromNodes();
    }

    private static void AddCatalogLink(
        Dictionary<DistrictZone, List<ZoneTravelLink>> buckets,
        DistrictZone zone,
        DistrictZone other,
        float gap,
        TravelEdgeKind kind)
    {
        if (!buckets.TryGetValue(zone, out List<ZoneTravelLink> links))
        {
            links = new List<ZoneTravelLink>();
            buckets[zone] = links;
        }

        links.Add(new ZoneTravelLink
        {
            other = other,
            gap = gap,
            kind = kind,
            contactOnThis = zone.GetWorldBounds().center,
            contactOnOther = other.GetWorldBounds().center
        });
    }

    private struct LinkPair : IEquatable<LinkPair>
    {
        public EntityId lo;
        public EntityId hi;

        public LinkPair(EntityId a, EntityId b)
        {
            if (a.CompareTo(b) <= 0)
            {
                lo = a;
                hi = b;
            }
            else
            {
                lo = b;
                hi = a;
            }
        }

        public bool Equals(LinkPair other) => lo.Equals(other.lo) && hi.Equals(other.hi);
        public override bool Equals(object obj) => obj is LinkPair other && Equals(other);
        public override int GetHashCode() => (lo.GetHashCode() * 397) ^ hi.GetHashCode();
    }

    private void BuildCache(List<ZoneTravelEdge> source)
    {
        neighbors.Clear();
        if (source == null) return;

        for (int i = 0; i < source.Count; i++)
        {
            ZoneTravelEdge edge = source[i];
            if (edge == null || edge.a == null || edge.b == null || edge.a == edge.b) continue;
            AddNeighbor(edge.a, edge);
            AddNeighbor(edge.b, edge);
        }
    }

    private void AddNeighbor(DistrictZone zone, ZoneTravelEdge edge)
    {
        if (!neighbors.TryGetValue(zone, out List<ZoneTravelEdge> list))
        {
            list = new List<ZoneTravelEdge>();
            neighbors[zone] = list;
        }

        list.Add(edge);
    }

    private void BuildFallback()
    {
        fallbackEdges.Clear();
        neighbors.Clear();

        if (InfluenceManager.IsNull) return;

        InfluenceManager manager = InfluenceManager.Get;
        IReadOnlyList<DistrictZone> zones = manager.GetPlayableZones();
        if (zones == null || zones.Count == 0) return;

        if (!warnedFallback)
        {
            warnedFallback = true;
            Debug.LogWarning(
                "ZoneTravelGraph: no baked side contacts. Rivals are using influence adjacency until the graph is rebuilt from the inspector.",
                this);
        }

        for (int i = 0; i < zones.Count; i++)
        {
            DistrictZone zone = zones[i];
            if (zone == null) continue;

            IReadOnlyList<DistrictZone> adjacent = manager.Adjacency.GetNeighbors(zone);
            for (int n = 0; n < adjacent.Count; n++)
            {
                DistrictZone other = adjacent[n];
                if (other == null || other == zone) continue;
                if (other.GetEntityId().CompareTo(zone.GetEntityId()) < 0) continue;

                ZoneTravelEdge edge = new ZoneTravelEdge
                {
                    a = zone,
                    b = other,
                    gap = 0f,
                    kind = zone.District == other.District
                        ? TravelEdgeKind.IntraDistrict
                        : TravelEdgeKind.CrossDistrict,
                    contactA = zone.GetWorldBounds().center,
                    contactB = other.GetWorldBounds().center
                };
                fallbackEdges.Add(edge);
                AddNeighbor(zone, edge);
                AddNeighbor(other, edge);
            }
        }
    }

    private bool HasFoothold(DistrictZone target, FactionId faction)
    {
        if (target == null || !neighbors.TryGetValue(target, out List<ZoneTravelEdge> links)) return false;

        for (int i = 0; i < links.Count; i++)
        {
            DistrictZone other = links[i] != null ? links[i].Other(target) : null;
            if (other == null || other.District != target.District) continue;
            if (HasPresence(other, faction)) return true;
        }

        return false;
    }

    private static bool HasPresence(DistrictZone zone, FactionId faction)
    {
        ZoneInfluenceState state = zone != null ? zone.Influence : null;
        if (state == null) return false;
        return state.GetShare(faction) > 0 || state.GetClerics(faction) > 0;
    }

    public static Color ColorFor(Districts district)
    {
        switch (district)
        {
            case Districts.District1: return new Color(0.90f, 0.22f, 0.24f);
            case Districts.District2: return new Color(0.95f, 0.42f, 0.68f);
            case Districts.District3: return new Color(0.28f, 0.78f, 0.38f);
            case Districts.District4: return new Color(0.96f, 0.82f, 0.18f);
            case Districts.District5: return new Color(0.62f, 0.38f, 0.95f);
            default: return new Color(0.82f, 0.90f, 1f);
        }
    }

#if UNITY_EDITOR
    private static GUIStyle zoneNameStyle;
    private static GUIStyle zoneNameShadow;

    private void OnEnable()
    {
        Selection.selectionChanged += RepaintScene;
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= RepaintScene;
    }

    private static void RepaintScene()
    {
        SceneView.RepaintAll();
    }

    private void OnDrawGizmos()
    {
        if (!showOverlay) return;
        DrawOverlay();
    }

    private void DrawOverlay()
    {
        DistrictZone selected = ResolveSelectedZone();
        if (selected == null) return;

        CompareFunction previousTest = Handles.zTest;
        Handles.zTest = CompareFunction.Always;

        IReadOnlyList<ZoneTravelLink> ownLinks = selected.TravelLinks;
        if (ownLinks != null && ownLinks.Count > 0)
        {
            DrawZoneFootprint(selected);
            for (int i = 0; i < ownLinks.Count; i++)
            {
                ZoneTravelLink link = ownLinks[i];
                if (link == null || link.other == null) continue;
                DrawZoneFootprint(link.other);
                DrawLink(selected, link);
            }

            for (int i = 0; i < ownLinks.Count; i++)
            {
                if (ownLinks[i] != null && ownLinks[i].other != null) DrawZoneMarker(ownLinks[i].other);
            }

            DrawZoneMarker(selected);
            Handles.zTest = previousTest;
            return;
        }

        if (DrawSavedLinks(selected))
        {
            Handles.zTest = previousTest;
            return;
        }

        IReadOnlyList<ZoneTravelEdge> drawn = Edges;
        List<DistrictZone> neighborZones = new List<DistrictZone>();
        List<ZoneTravelEdge> links = new List<ZoneTravelEdge>();
        if (drawn != null)
        {
            for (int i = 0; i < drawn.Count; i++)
            {
                ZoneTravelEdge edge = drawn[i];
                if (edge == null || edge.a == null || edge.b == null) continue;
                if (edge.a != selected && edge.b != selected) continue;

                links.Add(edge);
                RememberZone(neighborZones, edge.a == selected ? edge.b : edge.a);
            }
        }

        DrawZoneFootprint(selected);
        for (int i = 0; i < neighborZones.Count; i++) DrawZoneFootprint(neighborZones[i]);
        for (int i = 0; i < links.Count; i++) DrawConnection(links[i]);
        for (int i = 0; i < neighborZones.Count; i++) DrawZoneMarker(neighborZones[i]);
        DrawZoneMarker(selected);
        Handles.zTest = previousTest;
    }

    private bool DrawSavedLinks(DistrictZone selected)
    {
        ZoneTravelCatalog catalog = Resources.Load<ZoneTravelCatalog>("ZoneTravelCatalog");
        if (catalog == null || catalog.Links == null || catalog.Links.Count == 0) return false;

        string name = selected.gameObject.name;
        bool any = false;
        DrawZoneFootprint(selected);
        Dictionary<string, DistrictZone> byName = ZonesByName();

        IReadOnlyList<ZoneTravelCatalog.Record> records = catalog.Links;
        for (int i = 0; i < records.Count; i++)
        {
            ZoneTravelCatalog.Record record = records[i];
            if (record == null) continue;

            string otherName = null;
            if (record.fromZone == name) otherName = record.toZone;
            else if (record.toZone == name) otherName = record.fromZone;
            if (otherName == null) continue;

            if (!byName.TryGetValue(otherName, out DistrictZone other) || other == null) continue;

            any = true;
            DrawZoneFootprint(other);
            DrawSpan(selected, other, record.kind == TravelEdgeKind.CrossDistrict || selected.District != other.District);
            DrawZoneMarker(other);
        }

        DrawZoneMarker(selected);
        return any;
    }

    private static Dictionary<string, DistrictZone> ZonesByName()
    {
        Dictionary<string, DistrictZone> byName = new Dictionary<string, DistrictZone>();
        DistrictZone[] zones = FindObjectsByType<DistrictZone>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < zones.Length; i++)
        {
            DistrictZone zone = zones[i];
            if (zone == null) continue;
            byName[zone.gameObject.name] = zone;
        }

        return byName;
    }

    private static DistrictZone ResolveSelectedZone()
    {
        GameObject active = Selection.activeGameObject;
        if (active != null)
        {
            DistrictZone picked = active.GetComponent<DistrictZone>();
            if (picked == null) picked = active.GetComponentInParent<DistrictZone>();
            if (picked != null && picked.IsPlayable) return picked;
        }

        return DistrictSelectionController.SelectedZone;
    }

    private static void RememberZone(List<DistrictZone> zones, DistrictZone zone)
    {
        if (zone == null || zones.Contains(zone)) return;
        zones.Add(zone);
    }

    private static void DrawZoneFootprint(DistrictZone zone)
    {
        Bounds bounds = zone.GetWorldBounds();
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        float y = bounds.max.y + 0.15f;
        Vector3 a = new Vector3(center.x - extents.x, y, center.z - extents.z);
        Vector3 b = new Vector3(center.x + extents.x, y, center.z - extents.z);
        Vector3 c = new Vector3(center.x + extents.x, y, center.z + extents.z);
        Vector3 d = new Vector3(center.x - extents.x, y, center.z + extents.z);

        Color color = ColorFor(zone.District);
        color.a = 0.95f;
        Handles.color = color;
        Handles.DrawAAPolyLine(3.5f, a, b, c, d, a);
    }

    private static void DrawConnection(ZoneTravelEdge edge)
    {
        if (edge == null || edge.a == null || edge.b == null) return;
        bool crossing = edge.kind == TravelEdgeKind.CrossDistrict || edge.a.District != edge.b.District;
        DrawSpan(edge.a, edge.b, crossing);
    }

    private static void DrawLink(DistrictZone self, ZoneTravelLink link)
    {
        if (self == null || link == null || link.other == null) return;
        bool crossing = link.kind == TravelEdgeKind.CrossDistrict || self.District != link.other.District;
        DrawSpan(self, link.other, crossing);
    }

    private static void DrawSpan(DistrictZone fromZone, DistrictZone toZone, bool crossing)
    {
        Vector3 from = MarkerPosition(fromZone);
        Vector3 to = MarkerPosition(toZone);
        Handles.color = crossing ? new Color(1f, 0.42f, 0.08f, 1f) : ColorFor(fromZone.District);
        Handles.DrawAAPolyLine(crossing ? 7f : 3.5f, from, to);
        if (!crossing) return;

        Handles.DrawSolidDisc((from + to) * 0.5f, Vector3.up, MarkerRadius(fromZone) * 0.28f);
    }

    private void DrawZoneMarker(DistrictZone zone)
    {
        Vector3 marker = MarkerPosition(zone);
        float radius = MarkerRadius(zone);
        Color color = ColorFor(zone.District);

        Handles.color = new Color(color.r, color.g, color.b, 0.92f);
        Handles.DrawSolidDisc(marker, Vector3.up, radius);
        Handles.color = Color.white;
        Handles.DrawWireDisc(marker, Vector3.up, radius * 1.18f);

        if (!showZoneNames) return;

        EnsureNameStyles();
        bool lightDisc = zone.District == Districts.District4 || zone.District == Districts.District6;
        zoneNameStyle.normal.textColor = lightDisc ? Color.black : Color.white;
        zoneNameShadow.normal.textColor = lightDisc ? new Color(1f, 1f, 1f, 0.8f) : new Color(0f, 0f, 0f, 0.85f);
        string name = ShortZoneName(zone);
        Handles.Label(marker + new Vector3(0.05f, 0f, 0.05f), name, zoneNameShadow);
        Handles.Label(marker, name, zoneNameStyle);
    }

    private static Vector3 MarkerPosition(DistrictZone zone)
    {
        Bounds bounds = zone.GetWorldBounds();
        return new Vector3(bounds.center.x, bounds.max.y + 0.35f, bounds.center.z);
    }

    private static float MarkerRadius(DistrictZone zone)
    {
        Vector3 extents = zone.GetWorldBounds().extents;
        float span = Mathf.Max(0.8f, Mathf.Min(extents.x, extents.z));
        return span * 0.18f;
    }

    private static string ShortZoneName(DistrictZone zone)
    {
        string raw = zone.SectorName;
        if (string.IsNullOrEmpty(raw)) return DistrictColorMapping.GetDefaultDisplayName(zone.District);

        string trimmed = raw.StartsWith("ZONE_") ? raw.Substring(5) : raw;
        int split = trimmed.LastIndexOf('_');
        if (split <= 0 || !int.TryParse(trimmed.Substring(split + 1), out int number)) return trimmed;

        string color = trimmed.Substring(0, split).ToLowerInvariant();
        if (color.Length > 0) color = char.ToUpperInvariant(color[0]) + color.Substring(1);
        return color + " " + number;
    }

    private static void EnsureNameStyles()
    {
        if (zoneNameStyle != null) return;

        zoneNameStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13,
            alignment = TextAnchor.MiddleCenter
        };
        zoneNameStyle.normal.textColor = Color.white;

        zoneNameShadow = new GUIStyle(zoneNameStyle);
        zoneNameShadow.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
    }
#endif
}
