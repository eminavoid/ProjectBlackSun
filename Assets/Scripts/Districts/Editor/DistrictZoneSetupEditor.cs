using UnityEditor;
using UnityEngine;

public static class DistrictZoneSetupEditor
{
    [MenuItem("GameObject/Districts/Setup Map Districts (Parts + Zones)", false, 10)]
    private static void SetupFromMenu()
    {
        if (Selection.activeGameObject == null)
        {
            Debug.LogWarning("Select the map root (e.g. 'mapa por distritos 1') first.");
            return;
        }

        SetupMap(Selection.activeGameObject.transform, null);
    }

    public static void SetupMap(Transform mapRoot, DistrictColorMapping mapping)
    {
        if (mapRoot == null) return;

        DistrictMapBootstrap bootstrap = mapRoot.GetComponent<DistrictMapBootstrap>();
        if (bootstrap == null) bootstrap = Undo.AddComponent<DistrictMapBootstrap>(mapRoot.gameObject);

        SerializedObject serialized = new SerializedObject(bootstrap);
        SerializedProperty mappingProp = serialized.FindProperty("colorMapping");
        if (mapping != null && mappingProp != null)
        {
            mappingProp.objectReferenceValue = mapping;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        bootstrap.Configure(mapRoot, mapping);
        bootstrap.SetupMap();
        EditorUtility.SetDirty(mapRoot.gameObject);

        Debug.Log($"District setup complete on '{mapRoot.name}'.", mapRoot);
    }
}

[CustomEditor(typeof(DistrictPart))]
public class DistrictPartEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        DistrictPart part = (DistrictPart)target;

        if (GUILayout.Button("Apply name from Color Mapping"))
        {
            DistrictColorMapping mapping = FindColorMappingAsset();
            if (mapping != null) part.ApplyMapping(mapping);
            EditorUtility.SetDirty(part);
        }
    }

    public static DistrictColorMapping FindColorMappingAsset()
    {
        string[] guids = AssetDatabase.FindAssets("t:DistrictColorMapping");
        if (guids.Length == 0) return null;
        return AssetDatabase.LoadAssetAtPath<DistrictColorMapping>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }
}

[CustomEditor(typeof(DistrictZone))]
public class DistrictZoneEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        DistrictZone zone = (DistrictZone)target;

        if (GUILayout.Button("Resolve district from parent"))
        {
            DistrictColorMapping mapping = DistrictPartEditor.FindColorMappingAsset();
            zone.ResolveDistrictFromHierarchy(mapping);
            EditorUtility.SetDirty(zone);
        }

        if (GUILayout.Button("Ensure Mesh Collider"))
        {
            zone.EnsureCollider();
            EditorUtility.SetDirty(zone);
        }
    }
}
