using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Pick a district, focus one of its zones, then click other zones to toggle a touch.
/// </summary>
public class ZoneTouchPaintWindow : EditorWindow
{
    private static readonly string[] DistrictNames = BuildDistrictNames();

    private Districts district = Districts.District1;
    private DistrictZone focus;
    private bool painting = true;
    private readonly List<DistrictZone> playableZones = new List<DistrictZone>();

    [MenuItem("Tools/Project Black Sun/Paint Zone Touches")]
    [MenuItem("GameObject/Districts/Paint Zone Touches")]
    public static void Open()
    {
        ZoneTouchPaintWindow window = GetWindow<ZoneTouchPaintWindow>("Paint Touches");
        window.ReadSelection();
        window.Show();
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        ReadSelection();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        painting = false;
        SceneView.RepaintAll();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Paint touches", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "Choose a district. Right click one of its zones to focus it. Left click any other zone to add or remove a touch with that focus.",
            EditorStyles.wordWrappedLabel);
        EditorGUILayout.Space(6f);

        EditorGUI.BeginChangeCheck();
        int index = EditorGUILayout.Popup("District", (int)district, DistrictNames);
        if (EditorGUI.EndChangeCheck())
        {
            district = (Districts)index;
            if (focus != null && focus.District != district) focus = null;
            SceneView.RepaintAll();
        }

        painting = EditorGUILayout.Toggle("Painting", painting);
        EditorGUILayout.LabelField("Focus", focus == null ? "None" : focus.SectorName);
        if (focus != null)
        {
            EditorGUILayout.LabelField("Touches", CountLinks(focus).ToString());
            if (GUILayout.Button("Clear focus"))
            {
                focus = null;
                SceneView.RepaintAll();
            }
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.HelpBox(
            "Right click a zone in the chosen district to focus it. Left click another zone to toggle a touch. Esc stops painting.",
            MessageType.Info);
    }

    private void OnSceneGUI(SceneView view)
    {
        if (!painting) return;

        Event evt = Event.current;
        int control = GUIUtility.GetControlID(FocusType.Passive);
        HandleUtility.AddDefaultControl(control);

        DrawDistrict();
        DrawHint();

        if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape)
        {
            painting = false;
            evt.Use();
            Repaint();
            return;
        }

        if (evt.type == EventType.ContextClick)
        {
            evt.Use();
            return;
        }

        bool click = evt.type == EventType.MouseDown && !evt.alt && (evt.button == 0 || evt.button == 1);
        if (!click) return;

        Ray ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
        DistrictZone zone = PickZone(ray);
        if (zone != null)
        {
            if (evt.button == 1)
            {
                if (zone.District == district) SetFocus(zone);
            }
            else if (focus == null)
            {
                if (zone.District == district) SetFocus(zone);
            }
            else if (zone != focus)
            {
                ToggleTouch(focus, zone);
            }
        }

        evt.Use();
    }

    private void SetFocus(DistrictZone zone)
    {
        focus = zone;
        Selection.activeGameObject = zone.gameObject;
        Repaint();
        SceneView.RepaintAll();
    }

    private void ReadSelection()
    {
        GameObject active = Selection.activeGameObject;
        if (active == null) return;

        DistrictPart part = active.GetComponent<DistrictPart>();
        if (part == null) part = active.GetComponentInParent<DistrictPart>();
        if (part != null) district = part.District;

        DistrictZone zone = active.GetComponent<DistrictZone>();
        if (zone == null) zone = active.GetComponentInParent<DistrictZone>();
        if (zone != null && zone.IsPlayable)
        {
            district = zone.District;
            focus = zone;
        }
    }

    private void DrawDistrict()
    {
        RefreshZones();
        CompareFunction previous = Handles.zTest;
        Handles.zTest = CompareFunction.Always;
        Color districtColor = ZoneTravelGraph.ColorFor(district);

        for (int i = 0; i < playableZones.Count; i++)
        {
            DistrictZone zone = playableZones[i];
            if (zone == null || zone.District != district) continue;

            bool isFocus = zone == focus;
            Color color = districtColor;
            color.a = isFocus ? 1f : 0.45f;
            DrawFootprint(zone, color, isFocus ? 4.5f : 2f);
        }

        if (focus != null)
        {
            IReadOnlyList<ZoneTravelLink> links = focus.TravelLinks;
            for (int i = 0; i < links.Count; i++)
            {
                ZoneTravelLink link = links[i];
                if (link == null || link.other == null) continue;
                DrawFootprint(link.other, ZoneTravelGraph.ColorFor(link.other.District), 2.5f);
                DrawSpan(focus, link.other, link.kind == TravelEdgeKind.CrossDistrict || focus.District != link.other.District);
            }
        }

        Handles.zTest = previous;
    }

    private void DrawHint()
    {
        Handles.BeginGUI();
        string focusName = focus == null ? "right click a zone" : focus.SectorName;
        GUI.Label(new Rect(12f, 12f, 520f, 36f), "Painting " + DistrictNames[(int)district] + "  ·  Focus: " + focusName);
        Handles.EndGUI();
    }

    private void RefreshZones()
    {
        playableZones.Clear();
        DistrictZone[] found = Object.FindObjectsByType<DistrictZone>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null && found[i].IsPlayable) playableZones.Add(found[i]);
        }
    }

    private DistrictZone PickZone(Ray ray)
    {
        RefreshZones();
        DistrictZone closest = null;
        float closestDistance = float.MaxValue;

        RaycastHit[] hits = Physics.RaycastAll(ray, 100000f);
        for (int i = 0; i < hits.Length; i++)
        {
            DistrictZone zone = hits[i].collider.GetComponentInParent<DistrictZone>();
            if (zone == null || !zone.IsPlayable || hits[i].distance >= closestDistance) continue;
            closest = zone;
            closestDistance = hits[i].distance;
        }

        if (closest != null) return closest;

        for (int i = 0; i < playableZones.Count; i++)
        {
            DistrictZone zone = playableZones[i];
            if (!zone.GetWorldBounds().IntersectRay(ray, out float distance) || distance >= closestDistance) continue;
            closest = zone;
            closestDistance = distance;
        }

        return closest;
    }

    private static void ToggleTouch(DistrictZone a, DistrictZone b)
    {
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Toggle zone touch");
        int group = Undo.GetCurrentGroup();

        if (HasLink(a, b))
        {
            RemoveLink(a, b);
            RemoveLink(b, a);
        }
        else
        {
            AddLink(a, b);
            AddLink(b, a);
        }

        Undo.CollapseUndoOperations(group);
        ZoneTravelCatalogEditor.CaptureOpenZones();
        SceneView.RepaintAll();
    }

    private static bool HasLink(DistrictZone zone, DistrictZone other)
    {
        IReadOnlyList<ZoneTravelLink> links = zone.TravelLinks;
        for (int i = 0; i < links.Count; i++)
        {
            if (links[i] != null && links[i].other == other) return true;
        }

        return false;
    }

    private static void AddLink(DistrictZone zone, DistrictZone other)
    {
        List<ZoneTravelLink> links = new List<ZoneTravelLink>();
        IReadOnlyList<ZoneTravelLink> current = zone.TravelLinks;
        for (int i = 0; i < current.Count; i++)
        {
            if (current[i] != null && current[i].other != other) links.Add(current[i]);
        }

        links.Add(new ZoneTravelLink
        {
            other = other,
            gap = 0f,
            kind = zone.District == other.District ? TravelEdgeKind.IntraDistrict : TravelEdgeKind.CrossDistrict,
            contactOnThis = zone.GetWorldBounds().center,
            contactOnOther = other.GetWorldBounds().center
        });

        Undo.RecordObject(zone, "Toggle zone touch");
        zone.SetTravelLinks(links);
        EditorUtility.SetDirty(zone);
    }

    private static void RemoveLink(DistrictZone zone, DistrictZone other)
    {
        List<ZoneTravelLink> links = new List<ZoneTravelLink>();
        IReadOnlyList<ZoneTravelLink> current = zone.TravelLinks;
        for (int i = 0; i < current.Count; i++)
        {
            if (current[i] != null && current[i].other != other) links.Add(current[i]);
        }

        Undo.RecordObject(zone, "Toggle zone touch");
        zone.SetTravelLinks(links);
        EditorUtility.SetDirty(zone);
    }

    private static int CountLinks(DistrictZone zone)
    {
        int count = 0;
        IReadOnlyList<ZoneTravelLink> links = zone.TravelLinks;
        for (int i = 0; i < links.Count; i++)
        {
            if (links[i] != null && links[i].other != null) count++;
        }

        return count;
    }

    private static void DrawFootprint(DistrictZone zone, Color color, float width)
    {
        Bounds bounds = zone.GetWorldBounds();
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        float y = bounds.max.y + 0.2f;
        Handles.color = color;
        Handles.DrawAAPolyLine(
            width,
            new Vector3(center.x - extents.x, y, center.z - extents.z),
            new Vector3(center.x + extents.x, y, center.z - extents.z),
            new Vector3(center.x + extents.x, y, center.z + extents.z),
            new Vector3(center.x - extents.x, y, center.z + extents.z),
            new Vector3(center.x - extents.x, y, center.z - extents.z));
    }

    private static void DrawSpan(DistrictZone from, DistrictZone to, bool crossing)
    {
        Bounds fromBounds = from.GetWorldBounds();
        Bounds toBounds = to.GetWorldBounds();
        Vector3 a = new Vector3(fromBounds.center.x, fromBounds.max.y + 0.4f, fromBounds.center.z);
        Vector3 b = new Vector3(toBounds.center.x, toBounds.max.y + 0.4f, toBounds.center.z);
        Handles.color = crossing ? new Color(1f, 0.42f, 0.08f, 1f) : ZoneTravelGraph.ColorFor(from.District);
        Handles.DrawAAPolyLine(crossing ? 7f : 3.5f, a, b);
    }

    private static string[] BuildDistrictNames()
    {
        DistrictColorMapping mapping = DistrictPartEditor.FindColorMappingAsset();
        Districts[] values = (Districts[])System.Enum.GetValues(typeof(Districts));
        string[] names = new string[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            names[i] = DistrictColorMapping.GetDisplayName(values[i], mapping);
        }

        return names;
    }
}
