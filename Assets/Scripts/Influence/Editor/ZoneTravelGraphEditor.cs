using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[CustomEditor(typeof(ZoneTravelGraph))]
public class ZoneTravelGraphEditor : Editor
{
    private const string SettingsFoldoutKey = "ZoneTravelGraph.ShowSettings";

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        ZoneTravelGraph graph = (ZoneTravelGraph)target;

        EditorGUILayout.LabelField("How to read the map", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "Each zone keeps its own connection list. Select a zone to see and edit the zones it touches. A line in the zone color stays inside the district. A thick orange line crosses into another district.",
            EditorStyles.wordWrappedLabel);
        EditorGUILayout.Space(6f);

        CountContacts(out int zoneCount, out int inside, out int crossing);
        EditorGUILayout.LabelField("Zones", zoneCount.ToString());
        EditorGUILayout.LabelField("Touches inside a district", inside.ToString());
        EditorGUILayout.LabelField("Touches into another district", crossing.ToString());
        EditorGUILayout.Space(4f);

        DrawLegend();
        EditorGUILayout.Space(6f);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("showOverlay"), new GUIContent("Show zones and links"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("showZoneNames"), new GUIContent("Show zone names"));

        EditorGUILayout.Space(8f);
        if (GUILayout.Button("Rebuild from zone sides", GUILayout.Height(28f)))
        {
            ZoneTravelGraphBuilder.Rebuild(graph);
        }

        if (GUILayout.Button("Paint touches"))
        {
            ZoneTouchPaintWindow.Open();
        }

        if (graph.HasLegacyEdgeList)
        {
            EditorGUILayout.Space(4f);
            if (GUILayout.Button("Move saved contacts onto the zones"))
            {
                ZoneTravelGraphBuilder.CopyLegacyOntoZones(graph);
            }
        }

        EditorGUILayout.Space(8f);
        bool settingsOpen = EditorGUILayout.Foldout(EditorPrefs.GetBool(SettingsFoldoutKey, false), "Distance settings");
        EditorPrefs.SetBool(SettingsFoldoutKey, settingsOpen);
        if (settingsOpen)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("touchThreshold"), new GUIContent("Touch distance"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("minOverlap"), new GUIContent("Shared wall length"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("lateralNormalLimit"), new GUIContent("Side face cutoff"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("gapWeight"), new GUIContent("Gap weight"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("borderSurcharge"), new GUIContent("Unclaimed border cost"));
            EditorGUI.indentLevel--;
        }

        serializedObject.ApplyModifiedProperties();
    }

    private static void CountContacts(out int zoneCount, out int inside, out int crossing)
    {
        inside = 0;
        crossing = 0;
        HashSet<string> pairs = new HashSet<string>();
        HashSet<DistrictZone> zones = new HashSet<DistrictZone>();
        DistrictZone[] found = UnityEngine.Object.FindObjectsByType<DistrictZone>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < found.Length; i++)
        {
            DistrictZone zone = found[i];
            if (zone == null) continue;
            IReadOnlyList<ZoneTravelLink> links = zone.TravelLinks;
            for (int n = 0; n < links.Count; n++)
            {
                ZoneTravelLink link = links[n];
                if (link == null || link.other == null) continue;
                zones.Add(zone);
                zones.Add(link.other);

                int idA = zone.GetEntityId().GetHashCode();
                int idB = link.other.GetEntityId().GetHashCode();
                string key = idA < idB ? idA + ":" + idB : idB + ":" + idA;
                if (!pairs.Add(key)) continue;

                if (link.kind == TravelEdgeKind.CrossDistrict || zone.District != link.other.District) crossing++;
                else inside++;
            }
        }

        zoneCount = zones.Count;
    }

    private static void DrawLegend()
    {
        Swatch("Crucible", ZoneTravelGraph.ColorFor(Districts.District1));
        Swatch("Pink Quarter", ZoneTravelGraph.ColorFor(Districts.District2));
        Swatch("Warrens", ZoneTravelGraph.ColorFor(Districts.District3));
        Swatch("Agora", ZoneTravelGraph.ColorFor(Districts.District4));
        Swatch("Lacrimal Hill", ZoneTravelGraph.ColorFor(Districts.District5));
        Swatch("Ocularium", ZoneTravelGraph.ColorFor(Districts.District6));
        Swatch("Crosses districts", new Color(1f, 0.42f, 0.08f));
    }

    private static void Swatch(string label, Color color)
    {
        EditorGUILayout.BeginHorizontal();
        Rect rect = GUILayoutUtility.GetRect(16f, 16f, GUILayout.Width(16f), GUILayout.Height(16f));
        rect.y += 2f;
        EditorGUI.DrawRect(rect, color);
        EditorGUILayout.LabelField(label);
        EditorGUILayout.EndHorizontal();
    }
}

public static class ZoneTravelGraphBuilder
{
    private struct LateralSide
    {
        public Vector2 a;
        public Vector2 b;
        public Vector2 outward;
        public float height;
    }

    private sealed class EdgeInfo
    {
        public int count;
        public Vector3 p;
        public Vector3 q;
        public Vector2 outward;
    }

    private struct EdgeKey : IEquatable<EdgeKey>
    {
        public int lo;
        public int hi;

        public bool Equals(EdgeKey other) => lo == other.lo && hi == other.hi;
        public override bool Equals(object obj) => obj is EdgeKey other && Equals(other);
        public override int GetHashCode() => (lo * 397) ^ hi;
    }

    [MenuItem("Tools/Project Black Sun/Rebuild Zone Travel Graph")]
    [MenuItem("GameObject/Districts/Rebuild Zone Travel Graph")]
    private static void RebuildFromMenu()
    {
        ZoneTravelGraph graph = UnityEngine.Object.FindAnyObjectByType<ZoneTravelGraph>();
        if (graph == null)
        {
            InfluenceSystemBootstrap bootstrap = UnityEngine.Object.FindAnyObjectByType<InfluenceSystemBootstrap>();
            if (bootstrap == null)
            {
                Debug.LogWarning("Zone travel graph: open the game scene before rebuilding.");
                return;
            }

            graph = Undo.AddComponent<ZoneTravelGraph>(bootstrap.gameObject);
        }

        Rebuild(graph);
    }

    public static void Rebuild(ZoneTravelGraph graph)
    {
        if (graph == null) return;

        List<ZoneTravelEdge> built = Build(graph, out int intra, out int cross, out int boundsFallback);
        if (built == null) return;

        WriteLinksOntoZones(built);
        Undo.RecordObject(graph, "Rebuild Zone Travel Graph");
        graph.ClearStoredEdges();
        EditorUtility.SetDirty(graph);
        Debug.Log(
            $"Zone travel graph: {built.Count} contacts ({intra} inside districts, {cross} across) written onto the zones. {boundsFallback} zone(s) used a bounding box.",
            graph);
    }

    public static void CopyLegacyOntoZones(ZoneTravelGraph graph)
    {
        if (graph == null || !graph.HasLegacyEdgeList) return;

        List<ZoneTravelEdge> legacy = new List<ZoneTravelEdge>();
        IReadOnlyList<ZoneTravelEdge> source = graph.LegacyEdges;
        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] != null) legacy.Add(source[i]);
        }

        WriteLinksOntoZones(legacy);
        Undo.RecordObject(graph, "Move contacts onto zones");
        graph.ClearStoredEdges();
        EditorUtility.SetDirty(graph);
    }

    private static void WriteLinksOntoZones(List<ZoneTravelEdge> built)
    {
        DistrictZone[] found = UnityEngine.Object.FindObjectsByType<DistrictZone>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        Dictionary<DistrictZone, List<ZoneTravelLink>> buckets = new Dictionary<DistrictZone, List<ZoneTravelLink>>();

        for (int i = 0; i < found.Length; i++)
        {
            DistrictZone zone = found[i];
            if (zone == null || !zone.IsPlayable) continue;
            buckets[zone] = new List<ZoneTravelLink>();
        }

        for (int i = 0; i < built.Count; i++)
        {
            ZoneTravelEdge edge = built[i];
            if (edge == null || edge.a == null || edge.b == null) continue;
            AddLink(buckets, edge.a, edge.b, edge.gap, edge.kind, edge.contactA, edge.contactB);
            AddLink(buckets, edge.b, edge.a, edge.gap, edge.kind, edge.contactB, edge.contactA);
        }

        foreach (KeyValuePair<DistrictZone, List<ZoneTravelLink>> pair in buckets)
        {
            Undo.RecordObject(pair.Key, "Rebuild Zone Travel Graph");
            pair.Key.SetTravelLinks(pair.Value);
            EditorUtility.SetDirty(pair.Key);
        }
    }

    private static void AddLink(
        Dictionary<DistrictZone, List<ZoneTravelLink>> buckets,
        DistrictZone zone,
        DistrictZone other,
        float gap,
        TravelEdgeKind kind,
        Vector3 contactOnThis,
        Vector3 contactOnOther)
    {
        if (zone == null || other == null) return;
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
            contactOnThis = contactOnThis,
            contactOnOther = contactOnOther
        });
    }

    private static List<ZoneTravelEdge> Build(ZoneTravelGraph graph, out int intra, out int cross, out int boundsFallback)
    {
        intra = 0;
        cross = 0;
        boundsFallback = 0;

        DistrictZone[] found = UnityEngine.Object.FindObjectsByType<DistrictZone>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        List<DistrictZone> zones = new List<DistrictZone>();
        List<List<LateralSide>> sides = new List<List<LateralSide>>();
        for (int i = 0; i < found.Length; i++)
        {
            DistrictZone zone = found[i];
            if (zone == null || !zone.IsPlayable) continue;

            List<LateralSide> zoneSides = ExtractSides(zone, graph.LateralNormalLimit, out bool usedBounds);
            if (zoneSides.Count == 0) continue;
            if (usedBounds) boundsFallback++;
            zones.Add(zone);
            sides.Add(zoneSides);
        }

        if (zones.Count == 0)
        {
            Debug.LogWarning("Zone travel graph: no playable zones in the loaded scenes. Existing edges were kept.");
            return null;
        }

        float threshold = graph.TouchThreshold;
        float overlap = graph.MinOverlap;
        List<ZoneTravelEdge> edges = new List<ZoneTravelEdge>();

        for (int i = 0; i < zones.Count; i++)
        {
            Bounds bounds = zones[i].GetWorldBounds();
            bounds.Expand(threshold * 2f);

            for (int j = i + 1; j < zones.Count; j++)
            {
                if (!bounds.Intersects(zones[j].GetWorldBounds())) continue;
                if (!TryBestContact(sides[i], sides[j], threshold, overlap, out float gap, out Vector3 contactA, out Vector3 contactB))
                {
                    continue;
                }

                TravelEdgeKind kind = zones[i].District == zones[j].District
                    ? TravelEdgeKind.IntraDistrict
                    : TravelEdgeKind.CrossDistrict;

                if (kind == TravelEdgeKind.IntraDistrict) intra++;
                else cross++;

                edges.Add(new ZoneTravelEdge
                {
                    a = zones[i],
                    b = zones[j],
                    gap = gap,
                    kind = kind,
                    contactA = contactA,
                    contactB = contactB
                });
            }
        }

        return edges;
    }

    private static bool TryBestContact(
        List<LateralSide> aSides,
        List<LateralSide> bSides,
        float threshold,
        float minOverlap,
        out float gap,
        out Vector3 contactA,
        out Vector3 contactB)
    {
        gap = float.MaxValue;
        contactA = Vector3.zero;
        contactB = Vector3.zero;
        bool found = false;

        for (int i = 0; i < aSides.Count; i++)
        {
            LateralSide a = aSides[i];
            for (int j = 0; j < bSides.Count; j++)
            {
                LateralSide b = bSides[j];
                if (!TryContact(a, b, threshold, minOverlap, out float pairGap, out Vector2 ca, out Vector2 cb)) continue;
                if (pairGap >= gap) continue;

                gap = pairGap;
                contactA = new Vector3(ca.x, a.height, ca.y);
                contactB = new Vector3(cb.x, b.height, cb.y);
                found = true;
            }
        }

        return found;
    }

    private static List<LateralSide> ExtractSides(DistrictZone zone, float normalLimit, out bool usedBounds)
    {
        usedBounds = false;
        List<LateralSide> sides = new List<LateralSide>();
        Vector3 center3 = zone.GetWorldBounds().center;
        Vector2 center = new Vector2(center3.x, center3.z);

        if (TryReadMesh(zone, out Vector3[] vertices, out int[] triangles))
        {
            CollectLateralSides(vertices, triangles, normalLimit, center, sides);
            if (sides.Count > 80) sides = HullSides(sides);
            else MergeSides(sides);
        }

        if (sides.Count >= 3) return sides;

        usedBounds = true;
        sides.Clear();
        AddBoundsSides(zone, sides);
        return sides;
    }

    private static bool TryReadMesh(DistrictZone zone, out Vector3[] vertices, out int[] triangles)
    {
        vertices = null;
        triangles = null;

        MeshFilter filter = zone.GetComponent<MeshFilter>();
        if (filter == null) filter = zone.GetComponentInChildren<MeshFilter>();
        Mesh mesh = filter != null ? filter.sharedMesh : null;
        if (mesh == null || filter == null) return false;

        try
        {
            using (Mesh.MeshDataArray dataArray = Mesh.AcquireReadOnlyMeshData(mesh))
            {
                Mesh.MeshData data = dataArray[0];
                if (data.vertexCount == 0) return false;

                using (NativeArray<Vector3> raw = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp))
                {
                    data.GetVertices(raw);
                    vertices = new Vector3[raw.Length];
                    Transform transform = filter.transform;
                    for (int i = 0; i < raw.Length; i++)
                    {
                        vertices[i] = transform.TransformPoint(raw[i]);
                    }
                }

                List<int> indices = new List<int>();
                bool wide = mesh.indexFormat == IndexFormat.UInt32;
                for (int s = 0; s < data.subMeshCount; s++)
                {
                    SubMeshDescriptor sub = data.GetSubMesh(s);
                    if (sub.topology != MeshTopology.Triangles || sub.indexCount < 3) continue;

                    if (wide)
                    {
                        using (NativeArray<int> rawIndices = new NativeArray<int>(sub.indexCount, Allocator.Temp))
                        {
                            data.GetIndices(rawIndices, s, -1, true);
                            for (int i = 0; i < rawIndices.Length; i++) indices.Add(rawIndices[i]);
                        }
                    }
                    else
                    {
                        using (NativeArray<ushort> rawIndices = new NativeArray<ushort>(sub.indexCount, Allocator.Temp))
                        {
                            data.GetIndices(rawIndices, s, -1, true);
                            for (int i = 0; i < rawIndices.Length; i++) indices.Add(rawIndices[i]);
                        }
                    }
                }

                if (indices.Count < 3) return false;
                triangles = indices.ToArray();
                return true;
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Zone travel graph: could not read '{zone.SectorName}'. Using its bounding box. {exception.Message}", zone);
            return false;
        }
    }

    private static void CollectLateralSides(
        Vector3[] vertices,
        int[] triangles,
        float normalLimit,
        Vector2 center,
        List<LateralSide> sides)
    {
        Dictionary<EdgeKey, EdgeInfo> edges = new Dictionary<EdgeKey, EdgeInfo>();

        for (int i = 0; i + 2 < triangles.Length; i += 3)
        {
            int i0 = triangles[i];
            int i1 = triangles[i + 1];
            int i2 = triangles[i + 2];
            if (i0 < 0 || i1 < 0 || i2 < 0 || i0 >= vertices.Length || i1 >= vertices.Length || i2 >= vertices.Length)
            {
                continue;
            }

            Vector3 p0 = vertices[i0];
            Vector3 p1 = vertices[i1];
            Vector3 p2 = vertices[i2];
            Vector3 normal = Vector3.Cross(p1 - p0, p2 - p0);
            float magnitude = normal.magnitude;
            if (magnitude < 1e-6f) continue;
            normal /= magnitude;
            if (Mathf.Abs(normal.y) > normalLimit) continue;

            Vector2 outward = new Vector2(normal.x, normal.z);
            if (outward.sqrMagnitude < 1e-6f) continue;
            outward.Normalize();

            AddEdge(edges, i0, i1, p0, p1, outward);
            AddEdge(edges, i1, i2, p1, p2, outward);
            AddEdge(edges, i2, i0, p2, p0, outward);
        }

        foreach (KeyValuePair<EdgeKey, EdgeInfo> pair in edges)
        {
            EdgeInfo info = pair.Value;
            if (info.count != 1) continue;

            Vector2 a = new Vector2(info.p.x, info.p.z);
            Vector2 b = new Vector2(info.q.x, info.q.z);
            if ((b - a).sqrMagnitude < 0.0004f) continue;

            Vector2 outward = info.outward;
            Vector2 mid = (a + b) * 0.5f;
            if (Vector2.Dot(outward, mid - center) < 0f) outward = -outward;

            sides.Add(new LateralSide
            {
                a = a,
                b = b,
                outward = outward,
                height = (info.p.y + info.q.y) * 0.5f
            });
        }
    }

    private static void AddEdge(
        Dictionary<EdgeKey, EdgeInfo> edges,
        int indexA,
        int indexB,
        Vector3 p,
        Vector3 q,
        Vector2 outward)
    {
        EdgeKey key = new EdgeKey
        {
            lo = Mathf.Min(indexA, indexB),
            hi = Mathf.Max(indexA, indexB)
        };

        if (!edges.TryGetValue(key, out EdgeInfo info))
        {
            edges[key] = new EdgeInfo
            {
                count = 1,
                p = p,
                q = q,
                outward = outward
            };
            return;
        }

        info.count++;
    }

    private static List<LateralSide> HullSides(List<LateralSide> sides)
    {
        List<Vector2> points = new List<Vector2>();
        float height = 0f;
        for (int i = 0; i < sides.Count; i++)
        {
            points.Add(sides[i].a);
            points.Add(sides[i].b);
            height += sides[i].height;
        }

        height /= Mathf.Max(1, sides.Count);
        List<Vector2> hull = ConvexHull(points);
        if (hull.Count < 3) return sides;

        Vector2 center = Vector2.zero;
        for (int i = 0; i < hull.Count; i++) center += hull[i];
        center /= hull.Count;

        List<LateralSide> result = new List<LateralSide>();
        for (int i = 0; i < hull.Count; i++)
        {
            AddEdge(result, hull[i], hull[(i + 1) % hull.Count], height, center);
        }

        return result.Count >= 3 ? result : sides;
    }

    private static List<Vector2> ConvexHull(List<Vector2> points)
    {
        points.Sort((p, q) =>
        {
            int byX = p.x.CompareTo(q.x);
            return byX != 0 ? byX : p.y.CompareTo(q.y);
        });

        List<Vector2> unique = new List<Vector2>();
        for (int i = 0; i < points.Count; i++)
        {
            if (unique.Count == 0 || (unique[unique.Count - 1] - points[i]).sqrMagnitude > 0.0001f)
            {
                unique.Add(points[i]);
            }
        }

        if (unique.Count < 3) return unique;

        List<Vector2> hull = new List<Vector2>();
        for (int i = 0; i < unique.Count; i++)
        {
            while (hull.Count >= 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], unique[i]) <= 0f)
            {
                hull.RemoveAt(hull.Count - 1);
            }

            hull.Add(unique[i]);
        }

        int lowerCount = hull.Count + 1;
        for (int i = unique.Count - 2; i >= 0; i--)
        {
            while (hull.Count >= lowerCount && Cross(hull[hull.Count - 2], hull[hull.Count - 1], unique[i]) <= 0f)
            {
                hull.RemoveAt(hull.Count - 1);
            }

            hull.Add(unique[i]);
        }

        if (hull.Count > 1) hull.RemoveAt(hull.Count - 1);
        return hull;
    }

    private static float Cross(Vector2 a, Vector2 b, Vector2 c)
    {
        Vector2 ab = b - a;
        Vector2 ac = c - a;
        return ab.x * ac.y - ab.y * ac.x;
    }

    private static void MergeSides(List<LateralSide> sides)
    {
        const float join = 0.15f;
        const float align = 0.98f;
        bool changed = true;
        while (changed)
        {
            changed = false;
            for (int i = 0; i < sides.Count && !changed; i++)
            {
                for (int j = i + 1; j < sides.Count; j++)
                {
                    if (!TryMerge(sides[i], sides[j], join, align, out LateralSide merged)) continue;
                    sides[i] = merged;
                    sides.RemoveAt(j);
                    changed = true;
                    break;
                }
            }
        }
    }

    private static bool TryMerge(LateralSide a, LateralSide b, float join, float align, out LateralSide merged)
    {
        merged = default;
        Vector2 direction = b.b - b.a;
        float lengthA = (a.b - a.a).magnitude;
        float lengthB = direction.magnitude;
        if (lengthA < 0.001f || lengthB < 0.001f) return false;

        Vector2 da = (a.b - a.a) / lengthA;
        Vector2 db = direction / lengthB;
        if (Mathf.Abs(Vector2.Dot(da, db)) < align) return false;
        if (DistanceToLine(b.a, a.a, da) > join || DistanceToLine(b.b, a.a, da) > join) return false;

        float b0 = Vector2.Dot(b.a - a.a, da);
        float b1 = Vector2.Dot(b.b - a.a, da);
        float bMin = Mathf.Min(b0, b1);
        float bMax = Mathf.Max(b0, b1);
        if (bMin > lengthA + join || 0f > bMax + join) return false;

        float start = Mathf.Min(0f, bMin);
        float end = Mathf.Max(lengthA, bMax);
        Vector2 outward = a.outward + b.outward;
        if (outward.sqrMagnitude < 0.0001f) outward = a.outward;
        else outward.Normalize();

        merged = new LateralSide
        {
            a = a.a + da * start,
            b = a.a + da * end,
            outward = outward,
            height = (a.height + b.height) * 0.5f
        };
        return true;
    }

    private static float DistanceToLine(Vector2 point, Vector2 origin, Vector2 directionUnit)
    {
        Vector2 relative = point - origin;
        Vector2 along = directionUnit * Vector2.Dot(relative, directionUnit);
        return (relative - along).magnitude;
    }

    private static void AddBoundsSides(DistrictZone zone, List<LateralSide> sides)
    {
        Bounds bounds = zone.GetWorldBounds();
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        Vector2 p0 = new Vector2(center.x - extents.x, center.z - extents.z);
        Vector2 p1 = new Vector2(center.x + extents.x, center.z - extents.z);
        Vector2 p2 = new Vector2(center.x + extents.x, center.z + extents.z);
        Vector2 p3 = new Vector2(center.x - extents.x, center.z + extents.z);
        Vector2 flatCenter = new Vector2(center.x, center.z);
        AddEdge(sides, p0, p1, center.y, flatCenter);
        AddEdge(sides, p1, p2, center.y, flatCenter);
        AddEdge(sides, p2, p3, center.y, flatCenter);
        AddEdge(sides, p3, p0, center.y, flatCenter);
    }

    private static void AddEdge(List<LateralSide> sides, Vector2 a, Vector2 b, float height, Vector2 center)
    {
        Vector2 edge = b - a;
        if (edge.sqrMagnitude < 0.0004f) return;

        Vector2 outward = new Vector2(-edge.y, edge.x);
        Vector2 mid = (a + b) * 0.5f;
        if (Vector2.Dot(outward, mid - center) < 0f) outward = -outward;
        outward.Normalize();

        sides.Add(new LateralSide
        {
            a = a,
            b = b,
            outward = outward,
            height = height
        });
    }

    private static bool TryContact(
        LateralSide a,
        LateralSide b,
        float maxGap,
        float minOverlap,
        out float gap,
        out Vector2 contactA,
        out Vector2 contactB)
    {
        gap = ClosestSegment(a.a, a.b, b.a, b.b, out contactA, out contactB);
        if (gap > maxGap) return false;

        Vector2 da = a.b - a.a;
        Vector2 db = b.b - b.a;
        float lengthA = da.magnitude;
        float lengthB = db.magnitude;
        if (lengthA < 0.02f || lengthB < 0.02f) return false;
        da /= lengthA;
        db /= lengthB;
        if (Mathf.Abs(Vector2.Dot(da, db)) < 0.75f) return false;

        Vector2 dir = contactB - contactA;
        if (dir.sqrMagnitude < 1e-6f)
        {
            if (Vector2.Dot(a.outward, b.outward) > -0.25f) return false;
        }
        else
        {
            dir.Normalize();
            if (Vector2.Dot(a.outward, dir) < 0.25f) return false;
            if (Vector2.Dot(b.outward, -dir) < 0.25f) return false;
        }

        return OverlapAtLeast(a.a, a.b, b.a, b.b, da, minOverlap);
    }

    private static bool OverlapAtLeast(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1, Vector2 axis, float min)
    {
        float aMin = Mathf.Min(Vector2.Dot(a0, axis), Vector2.Dot(a1, axis));
        float aMax = Mathf.Max(Vector2.Dot(a0, axis), Vector2.Dot(a1, axis));
        float bMin = Mathf.Min(Vector2.Dot(b0, axis), Vector2.Dot(b1, axis));
        float bMax = Mathf.Max(Vector2.Dot(b0, axis), Vector2.Dot(b1, axis));
        return Mathf.Min(aMax, bMax) - Mathf.Max(aMin, bMin) >= min;
    }

    private static float ClosestSegment(
        Vector2 p1,
        Vector2 q1,
        Vector2 p2,
        Vector2 q2,
        out Vector2 c1,
        out Vector2 c2)
    {
        Vector2 d1 = q1 - p1;
        Vector2 d2 = q2 - p2;
        Vector2 r = p1 - p2;
        float a = Vector2.Dot(d1, d1);
        float e = Vector2.Dot(d2, d2);
        float f = Vector2.Dot(d2, r);
        const float epsilon = 1e-6f;
        float s;
        float t;

        if (a <= epsilon && e <= epsilon)
        {
            c1 = p1;
            c2 = p2;
            return Vector2.Distance(c1, c2);
        }

        if (a <= epsilon)
        {
            s = 0f;
            t = Mathf.Clamp01(f / e);
        }
        else
        {
            float c = Vector2.Dot(d1, r);
            if (e <= epsilon)
            {
                t = 0f;
                s = Mathf.Clamp01(-c / a);
            }
            else
            {
                float b = Vector2.Dot(d1, d2);
                float denom = a * e - b * b;
                s = denom > epsilon ? Mathf.Clamp01((b * f - c * e) / denom) : 0f;
                t = (b * s + f) / e;
                if (t < 0f)
                {
                    t = 0f;
                    s = Mathf.Clamp01(-c / a);
                }
                else if (t > 1f)
                {
                    t = 1f;
                    s = Mathf.Clamp01((b - c) / a);
                }
            }
        }

        c1 = p1 + d1 * s;
        c2 = p2 + d2 * t;
        return Vector2.Distance(c1, c2);
    }
}
