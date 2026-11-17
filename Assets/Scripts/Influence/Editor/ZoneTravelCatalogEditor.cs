using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ZoneTravelCatalogEditor
{
    public const string AssetPath = "Assets/Resources/ZoneTravelCatalog.asset";

    static ZoneTravelCatalogEditor()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    [MenuItem("Tools/Project Black Sun/Capture Runtime Touches")]
    public static void CaptureFromMenu()
    {
        CaptureOpenZones();
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingPlayMode) return;
        CaptureOpenZones();
    }

    public static int CaptureOpenZones()
    {
        DistrictZone[] zones = Object.FindObjectsByType<DistrictZone>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        List<ZoneTravelCatalog.Record> records = new List<ZoneTravelCatalog.Record>();
        HashSet<string> seen = new HashSet<string>();

        for (int i = 0; i < zones.Length; i++)
        {
            DistrictZone zone = zones[i];
            if (zone == null) continue;

            IReadOnlyList<ZoneTravelLink> links = zone.TravelLinks;
            for (int n = 0; n < links.Count; n++)
            {
                ZoneTravelLink link = links[n];
                if (link == null || link.other == null) continue;

                string from = zone.gameObject.name;
                string to = link.other.gameObject.name;
                if (string.CompareOrdinal(from, to) > 0)
                {
                    string swap = from;
                    from = to;
                    to = swap;
                }

                string key = from + "|" + to;
                if (!seen.Add(key)) continue;

                records.Add(new ZoneTravelCatalog.Record
                {
                    fromZone = from,
                    toZone = to,
                    gap = link.gap,
                    kind = zone.gameObject.name == from && link.other.gameObject.name == to
                        ? link.kind
                        : (from == to ? link.kind : KindBetween(zone, link.other))
                });
            }
        }

        if (records.Count == 0) return 0;

        records.Sort((x, y) =>
        {
            int byFrom = string.CompareOrdinal(x.fromZone, y.fromZone);
            return byFrom != 0 ? byFrom : string.CompareOrdinal(x.toZone, y.toZone);
        });

        ZoneTravelCatalog catalog = GetOrCreate();
        if (SameLinks(catalog.Links, records)) return records.Count;

        catalog.SetLinks(records);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        return records.Count;
    }

    public static ZoneTravelCatalog GetOrCreate()
    {
        ZoneTravelCatalog catalog = AssetDatabase.LoadAssetAtPath<ZoneTravelCatalog>(AssetPath);
        if (catalog != null) return catalog;

        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        catalog = ScriptableObject.CreateInstance<ZoneTravelCatalog>();
        AssetDatabase.CreateAsset(catalog, AssetPath);
        AssetDatabase.SaveAssets();
        return catalog;
    }

    private static bool SameLinks(IReadOnlyList<ZoneTravelCatalog.Record> saved, List<ZoneTravelCatalog.Record> captured)
    {
        if (saved == null || saved.Count != captured.Count) return false;
        for (int i = 0; i < captured.Count; i++)
        {
            ZoneTravelCatalog.Record a = saved[i];
            ZoneTravelCatalog.Record b = captured[i];
            if (a == null || a.fromZone != b.fromZone || a.toZone != b.toZone || a.kind != b.kind) return false;
            if (!Mathf.Approximately(a.gap, b.gap)) return false;
        }

        return true;
    }

    private static TravelEdgeKind KindBetween(DistrictZone a, DistrictZone b)
    {
        return a.District == b.District ? TravelEdgeKind.IntraDistrict : TravelEdgeKind.CrossDistrict;
    }
}
