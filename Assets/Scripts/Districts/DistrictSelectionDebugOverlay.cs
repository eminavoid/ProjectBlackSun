using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// On-screen feedback to verify district click detection. Add to DistrictManager or any active object.
/// </summary>
public class DistrictSelectionDebugOverlay : MonoBehaviour
{
    [SerializeField] private bool showOnScreenLabel = true;
    [SerializeField] private bool logSelectionToConsole = true;
    [SerializeField] private bool logSetupReportOnStart = true;
    [SerializeField] private DistrictColorMapping colorMapping;

    private string lastHitObjectName = string.Empty;
    private string lastPartColorName = string.Empty;
    private string displayText = "Distrito: (ninguno)";

    private void OnEnable()
    {
        DistrictSelectionController.OnSelectionChanged += OnSelectionChanged;
    }

    private void OnDisable()
    {
        DistrictSelectionController.OnSelectionChanged -= OnSelectionChanged;
    }

    private void Start()
    {
        if (colorMapping == null)
        {
            DistrictSelectionController controller = FindAnyObjectByType<DistrictSelectionController>();
            if (controller != null) colorMapping = controller.ColorMapping;
        }

        if (logSetupReportOnStart)
        {
            Debug.Log(BuildSetupReport(colorMapping), this);
        }

        RefreshDisplay(DistrictSelectionController.SelectedDistrict);
    }

    private void OnSelectionChanged(Districts? district)
    {
        lastPartColorName = DistrictSelectionController.LastSelectedPartColorName;
        lastHitObjectName = DistrictSelectionController.LastSelectedZoneName;
        RefreshDisplay(district);

        if (!logSelectionToConsole) return;

        if (district.HasValue)
        {
            Debug.Log(
                DistrictSelectionController.FormatSelectionLog(
                    district.Value,
                    lastPartColorName,
                    lastHitObjectName,
                    colorMapping),
                this);
        }
        else
        {
            Debug.Log("[District click] deseleccionado / sin hit", this);
        }
    }

    public void RefreshDisplay(Districts? district)
    {
        lastPartColorName = DistrictSelectionController.LastSelectedPartColorName;
        lastHitObjectName = DistrictSelectionController.LastSelectedZoneName;

        if (!district.HasValue)
        {
            displayText = "Distrito: (ninguno — click en mapa)";
            return;
        }

        string districtLabel = colorMapping != null
            ? colorMapping.FormatDistrictWithColors(district.Value)
            : district.Value.ToString();

        displayText = $"Distrito: {districtLabel}\nColor/carpeta: {lastPartColorName}\nZona: {lastHitObjectName}";
    }

    private void OnGUI()
    {
        if (!showOnScreenLabel) return;

        GUIStyle style = new GUIStyle(GUI.skin.box)
        {
            fontSize = 16,
            alignment = TextAnchor.UpperLeft
        };
        style.normal.textColor = Color.white;

        GUI.Box(new Rect(12f, 12f, 420f, 88f), displayText, style);
    }

    [ContextMenu("Log District Setup Report")]
    public void LogSetupReport()
    {
        Debug.Log(BuildSetupReport(colorMapping), this);
    }

    public static string BuildSetupReport(DistrictColorMapping mapping)
    {
        if (mapping == null)
        {
            DistrictSelectionController controller = FindAnyObjectByType<DistrictSelectionController>();
            if (controller != null) mapping = controller.ColorMapping;
        }

        DistrictZone[] zones = FindObjectsByType<DistrictZone>(FindObjectsSortMode.None);
        var counts = new Dictionary<Districts, int>();
        var samples = new Dictionary<Districts, string>();
        var partFolders = new Dictionary<Districts, HashSet<string>>();
        int meshesWithoutZone = 0;

        for (int i = 0; i < zones.Length; i++)
        {
            DistrictZone zone = zones[i];
            if (zone == null) continue;

            Collider col = zone.GetComponent<Collider>();
            if (col == null || !col.enabled) continue;

            counts.TryGetValue(zone.District, out int count);
            counts[zone.District] = count + 1;

            if (!samples.ContainsKey(zone.District))
            {
                samples[zone.District] = zone.name;
            }

            string partName = DistrictSelectionController.ResolvePartColorName(zone);
            if (!string.IsNullOrEmpty(partName))
            {
                if (!partFolders.ContainsKey(zone.District))
                {
                    partFolders[zone.District] = new HashSet<string>();
                }

                partFolders[zone.District].Add(partName);
            }
        }

        MeshFilter[] allMeshes = FindObjectsByType<MeshFilter>(FindObjectsSortMode.None);
        for (int i = 0; i < allMeshes.Length; i++)
        {
            if (allMeshes[i] == null || allMeshes[i].sharedMesh == null) continue;
            if (allMeshes[i].GetComponentInParent<DistrictZone>() != null) continue;
            if (allMeshes[i].GetComponentInParent<DistrictMapBootstrap>() == null &&
                allMeshes[i].GetComponentInParent<DistrictPart>() == null)
            {
                continue;
            }

            meshesWithoutZone++;
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== District setup report ===");
        sb.AppendLine($"Zonas clickeables (con collider): {zones.Length}");
        sb.AppendLine();

        if (mapping != null)
        {
            sb.AppendLine("Mapping asset (color → distrito):");
            foreach (Districts district in System.Enum.GetValues(typeof(Districts)))
            {
                if (!mapping.TryGetPartNamesForDistrict(district, out List<string> colors)) continue;
                sb.AppendLine($"  {string.Join(", ", colors)} → {district}");
            }

            sb.AppendLine();
        }

        sb.AppendLine("En escena (distrito → carpetas detectadas → zonas):");

        foreach (Districts district in System.Enum.GetValues(typeof(Districts)))
        {
            counts.TryGetValue(district, out int count);
            samples.TryGetValue(district, out string sample);

            string folders = partFolders.TryGetValue(district, out HashSet<string> set) && set.Count > 0
                ? string.Join(", ", set)
                : "(ninguna carpeta)";

            string assetColors = mapping != null && mapping.TryGetPartNamesForDistrict(district, out List<string> expected)
                ? string.Join(", ", expected)
                : "?";

            sb.AppendLine($"  {district}: colores asset [{assetColors}] | carpetas [{folders}] | {count} zona(s){(count > 0 ? $" (ej: {sample})" : " — SIN ZONAS")}");
        }

        if (meshesWithoutZone > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"Meshes bajo el mapa SIN DistrictZone/collider: {meshesWithoutZone}");
            sb.AppendLine("  → Revisa que estén bajo la carpeta de color (Red, Green, …) y ejecuta Setup Map.");
        }

        sb.AppendLine();
        sb.AppendLine("Clic: log muestra carpeta/color, mesh y si coincide con el mapping (✓ / ✗).");
        return sb.ToString();
    }
}
