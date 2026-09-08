using UnityEditor;
using UnityEngine;

public static class MapStatsPrefabGenerator
{
    private const string PrefabsRoot = "Assets/Prefabs";
    private const string StatsFolder = "Assets/Prefabs/Stats";

    [MenuItem("Project Black Sun/Stats/Generate Prefabs")]
    private static void Generate()
    {
        EnsureFolder(PrefabsRoot);
        EnsureFolder(StatsFolder);

        GameObject canvasGo = new GameObject("StatsPrefabBakeCanvas", typeof(RectTransform), typeof(Canvas));
        try
        {
            MapStatsWindowView window = MapStatsUiBuilder.BuildWindow(canvasGo.transform);
            SaveCopy(window.FactionRowTemplate, StatsFolder + "/StatsFactionRow.prefab");
            SaveCopy(window.SeedRowTemplate, StatsFolder + "/StatsSeedRow.prefab");
            SaveCopy(window.NodeRowTemplate, StatsFolder + "/StatsNodeRow.prefab");
            SaveCopy(window.DistrictRowTemplate, StatsFolder + "/StatsDistrictRow.prefab");

            window.Root.SetActive(false);
            window.Root.transform.SetParent(null, false);
            SaveAndDestroy(window.Root, StatsFolder + "/MapStatsWindow.prefab");

            MapContextMenuView menu = MapStatsUiBuilder.BuildContextMenu(canvasGo.transform);
            menu.Root.SetActive(false);
            menu.Root.transform.SetParent(null, false);
            SaveAndDestroy(menu.Root, StatsFolder + "/MapContextMenu.prefab");
        }
        finally
        {
            Object.DestroyImmediate(canvasGo);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Map stats prefabs generated in " + StatsFolder);
    }

    private static void SaveCopy(GameObject source, string path)
    {
        if (source == null) return;

        GameObject copy = Object.Instantiate(source);
        copy.name = source.name;
        copy.SetActive(true);
        copy.transform.SetParent(null, false);
        SaveAndDestroy(copy, path);
    }

    private static void SaveAndDestroy(GameObject go, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        int slash = path.LastIndexOf('/');
        string parent = path.Substring(0, slash);
        string name = path.Substring(slash + 1);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
