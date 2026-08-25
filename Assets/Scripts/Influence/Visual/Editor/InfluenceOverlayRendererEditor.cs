using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(InfluenceOverlayRenderer))]
public class InfluenceOverlayRendererEditor : Editor
{
    public override void OnInspectorGUI()
    {
        InfluenceOverlayRenderer renderer = (InfluenceOverlayRenderer)target;
        InfluenceOverlaySettings settings = renderer.Settings;

        EditorGUILayout.HelpBox(
            "Los sliders se guardan en Assets/Resources/InfluenceOverlaySettings.asset. Sobreviven al salir de Play.",
            MessageType.Info);

        if (settings == null)
        {
            EditorGUILayout.HelpBox("No se pudo cargar InfluenceOverlaySettings.", MessageType.Warning);
            return;
        }

        EditorGUI.BeginChangeCheck();
        SerializedObject serializedSettings = new SerializedObject(settings);
        serializedSettings.UpdateIfRequiredOrScript();
        DrawPropertiesExcluding(serializedSettings, "m_Script");
        if (EditorGUI.EndChangeCheck())
        {
            serializedSettings.ApplyModifiedProperties();
            settings.Persist();
        }
    }
}
