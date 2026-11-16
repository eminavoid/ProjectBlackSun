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
        if (GUILayout.Button("Rebuild from zone outlines", GUILayout.Height(28f)))
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
            EditorGUILayout.PropertyField(serializedObject.FindProperty("streetWidth"), new GUIContent("Street width"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("minFrontage"), new GUIContent("Shared frontage"));
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
    private struct Segment
    {
        public Vector2 a;
        public Vector2 b;
        public Rect box;
    }

    private struct Facing
    {
        public float length;
        public float gap;
        public Vector2 near;
        public Vector2 far;
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
        List<List<Segment>> outlines = new List<List<Segment>>();
        for (int i = 0; i < found.Length; i++)
        {
            DistrictZone zone = found[i];
            if (zone == null || !zone.IsPlayable) continue;

            List<Segment> outline = ReadOutline(zone, out bool usedBounds);
            if (outline.Count == 0) continue;
            if (usedBounds) boundsFallback++;
            zones.Add(zone);
            outlines.Add(outline);
        }

        if (zones.Count == 0)
        {
            Debug.LogWarning("Zone travel graph: no playable zones in the loaded scenes. Existing edges were kept.");
            return null;
        }

        float street = graph.StreetWidth;
        float frontage = graph.MinFrontage;
        float step = street * 0.03f;
        List<ZoneTravelEdge> edges = new List<ZoneTravelEdge>();

        for (int i = 0; i < zones.Count; i++)
        {
            Bounds bounds = zones[i].GetWorldBounds();
            bounds.Expand(street * 2f);

            for (int j = i + 1; j < zones.Count; j++)
            {
                Bounds other = zones[j].GetWorldBounds();
                if (!bounds.Intersects(other)) continue;

                Facing there = Measure(outlines[i], outlines[j], street, step);
                if (there.length < frontage) continue;
                Facing back = Measure(outlines[j], outlines[i], street, step);
                if (back.length < frontage) continue;

                TravelEdgeKind kind = zones[i].District == zones[j].District
                    ? TravelEdgeKind.IntraDistrict
                    : TravelEdgeKind.CrossDistrict;

                if (kind == TravelEdgeKind.IntraDistrict) intra++;
                else cross++;

                float heightA = zones[i].GetWorldBounds().max.y;
                float heightB = other.max.y;
                edges.Add(new ZoneTravelEdge
                {
                    a = zones[i],
                    b = zones[j],
                    gap = Mathf.Min(there.gap, back.gap),
                    kind = kind,
                    contactA = new Vector3(there.near.x, heightA, there.near.y),
                    contactB = new Vector3(there.far.x, heightB, there.far.y)
                });
            }
        }

        return edges;
    }

    private static Facing Measure(List<Segment> from, List<Segment> to, float street, float step)
    {
        Facing result = new Facing { gap = float.MaxValue };
        Vector2 nearSum = Vector2.zero;
        Vector2 farSum = Vector2.zero;
        int hits = 0;

        for (int i = 0; i < from.Count; i++)
        {
            Segment segment = from[i];
            Vector2 direction = segment.b - segment.a;
            float length = direction.magnitude;
            if (length < 1e-6f) continue;
            direction /= length;

            int samples = Mathf.Max(1, Mathf.CeilToInt(length / step));
            float piece = length / samples;
            for (int k = 0; k < samples; k++)
            {
                Vector2 point = Vector2.Lerp(segment.a, segment.b, (k + 0.5f) / samples);
                if (!TryClosest(point, to, street, out Vector2 closest, out float distance)) continue;
                if (distance > 1e-6f && Mathf.Abs(Vector2.Dot((closest - point) / distance, direction)) > 0.5f) continue;

                result.length += piece;
                result.gap = Mathf.Min(result.gap, distance);
                nearSum += point;
                farSum += closest;
                hits++;
            }
        }

        if (hits > 0)
        {
            result.near = nearSum / hits;
            result.far = farSum / hits;
        }

        return result;
    }

    private static bool TryClosest(Vector2 point, List<Segment> segments, float reach, out Vector2 closest, out float distance)
    {
        closest = point;
        distance = float.MaxValue;
        for (int i = 0; i < segments.Count; i++)
        {
            Rect box = segments[i].box;
            if (point.x < box.xMin - reach || point.x > box.xMax + reach || point.y < box.yMin - reach || point.y > box.yMax + reach)
            {
                continue;
            }

            Vector2 onSegment = ClosestOnSegment(point, segments[i].a, segments[i].b);
            float candidate = Vector2.Distance(point, onSegment);
            if (candidate >= distance) continue;
            distance = candidate;
            closest = onSegment;
        }

        return distance <= reach;
    }

    private static Vector2 ClosestOnSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lengthSquared = ab.sqrMagnitude;
        if (lengthSquared < 1e-12f) return a;
        return a + ab * Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSquared);
    }

    private static List<Segment> ReadOutline(DistrictZone zone, out bool usedBounds)
    {
        usedBounds = false;
        List<Segment> outline = new List<Segment>();
        if (TryReadMesh(zone, out Vector3[] vertices, out int[] triangles))
        {
            CollectOutline(vertices, triangles, outline);
        }

        if (outline.Count >= 3) return outline;

        usedBounds = true;
        outline.Clear();
        Bounds bounds = zone.GetWorldBounds();
        Vector2 min = new Vector2(bounds.min.x, bounds.min.z);
        Vector2 max = new Vector2(bounds.max.x, bounds.max.z);
        outline.Add(MakeSegment(min, new Vector2(max.x, min.y)));
        outline.Add(MakeSegment(new Vector2(max.x, min.y), max));
        outline.Add(MakeSegment(max, new Vector2(min.x, max.y)));
        outline.Add(MakeSegment(new Vector2(min.x, max.y), min));
        return outline;
    }

    private static void CollectOutline(Vector3[] vertices, int[] triangles, List<Segment> outline)
    {
        Dictionary<EdgeKey, int> uses = new Dictionary<EdgeKey, int>();

        for (int i = 0; i + 2 < triangles.Length; i += 3)
        {
            int i0 = triangles[i];
            int i1 = triangles[i + 1];
            int i2 = triangles[i + 2];
            if (i0 < 0 || i1 < 0 || i2 < 0 || i0 >= vertices.Length || i1 >= vertices.Length || i2 >= vertices.Length)
            {
                continue;
            }

            Vector3 normal = Vector3.Cross(vertices[i1] - vertices[i0], vertices[i2] - vertices[i0]);
            float magnitude = normal.magnitude;
            if (magnitude <= 0f || Mathf.Abs(normal.y) < magnitude * 0.5f) continue;

            Count(uses, i0, i1);
            Count(uses, i1, i2);
            Count(uses, i2, i0);
        }

        foreach (KeyValuePair<EdgeKey, int> pair in uses)
        {
            if (pair.Value != 1) continue;
            Vector3 p = vertices[pair.Key.lo];
            Vector3 q = vertices[pair.Key.hi];
            outline.Add(MakeSegment(new Vector2(p.x, p.z), new Vector2(q.x, q.z)));
        }
    }

    private static void Count(Dictionary<EdgeKey, int> uses, int indexA, int indexB)
    {
        EdgeKey key = new EdgeKey
        {
            lo = Mathf.Min(indexA, indexB),
            hi = Mathf.Max(indexA, indexB)
        };

        uses.TryGetValue(key, out int count);
        uses[key] = count + 1;
    }

    private static Segment MakeSegment(Vector2 a, Vector2 b)
    {
        return new Segment
        {
            a = a,
            b = b,
            box = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y))
        };
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
}
