using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Ajustes persistentes del overlay de influencia. El renderer se crea en Play,
/// así que los sliders viven acá (asset) y no se pierden al parar el juego.
/// </summary>
[CreateAssetMenu(fileName = "InfluenceOverlaySettings", menuName = "Influence/Overlay Settings", order = 10)]
public class InfluenceOverlaySettings : ScriptableObject
{
    public const string ResourceName = "InfluenceOverlaySettings";
    public const string AssetPath = "Assets/Resources/InfluenceOverlaySettings.asset";

    [Header("Campo")]
    public int fieldResolution = 160;
    public float splatRadiusScale = 1.5f;
    [Tooltip("Radio del puente que fusiona cuadras vecinas del mismo distrito y mismo dueño.")]
    public float bridgeRadiusScale = 1.15f;
    public int blurPasses = 2;
    public int blurRadius = 2;
    [Tooltip("Cuánta presencia aporta un clérigo estacionado, en puntos de influencia.")]
    public float clericWeight = 0.6f;
    [Tooltip("Presencia mínima visible en una zona ocupada.")]
    public float minPresence = 0.14f;

    [Header("Visual")]
    [Tooltip("Celdas del patrón hexagonal a lo ancho de una cuadra.")]
    public float patternCellsPerZone = 3.5f;
    public float transitionSeconds = 0.6f;
    public float fadeSeconds = 0.25f;
    public bool startVisible = true;

    [Header("Volumen")]
    [Tooltip("Altura del overlay sobre el mapa, en fracciones del tamaño de una cuadra. 0 = pegado al piso.")]
    [Range(0f, 0.4f)]
    public float volumeHeight = 0.05f;
    [Tooltip("Respiración vertical del volumen.")]
    [Range(0f, 0.08f)]
    public float volumeBreath = 0.01f;
    [Tooltip("Brillo del tinte de facción.")]
    [Range(0.2f, 4f)]
    public float overlayIntensity = 1.75f;

    public static InfluenceOverlaySettings LoadOrCreate()
    {
        InfluenceOverlaySettings settings = Resources.Load<InfluenceOverlaySettings>(ResourceName);
        if (settings != null) return settings;

#if UNITY_EDITOR
        settings = AssetDatabase.LoadAssetAtPath<InfluenceOverlaySettings>(AssetPath);
        if (settings == null)
        {
            settings = CreateInstance<InfluenceOverlaySettings>();
            settings.name = ResourceName;

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            AssetDatabase.CreateAsset(settings, AssetPath);
            AssetDatabase.SaveAssets();
        }

        return settings;
#else
        return CreateInstance<InfluenceOverlaySettings>();
#endif
    }

    public void Persist()
    {
#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssetIfDirty(this);
#endif
    }
}
