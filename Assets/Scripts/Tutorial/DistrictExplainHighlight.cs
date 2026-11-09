using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pinta el distrito entero con su propio color. Solo vive durante la explicación.
/// </summary>
public class DistrictExplainHighlight : MonoBehaviour
{
    private const string ShaderName = "Custom/DistrictExplain";
    private const string ShellName = "DistrictExplain";

    private readonly List<GameObject> shells = new List<GameObject>();
    private Material material;

    public bool Show(Districts district, out Bounds bounds)
    {
        Clear();
        bounds = default;

        List<DistrictZone> zones = ZonesOf(district);
        if (zones.Count == 0) return false;
        if (!TryReadColor(zones, out Color color)) return false;

        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogWarning($"DistrictExplainHighlight: no se encontró el shader '{ShaderName}'.");
            return false;
        }

        material = new Material(shader) { name = "DistrictExplain" };
        color.a = 1f;
        material.SetColor("_Color", color);

        bool any = false;
        for (int i = 0; i < zones.Count; i++)
        {
            DistrictZone zone = zones[i];
            if (zone == null) continue;
            if (!TryShell(zone)) continue;

            Bounds zoneBounds = zone.GetWorldBounds();
            if (!any)
            {
                bounds = zoneBounds;
                any = true;
            }
            else
            {
                bounds.Encapsulate(zoneBounds);
            }
        }

        if (!any) Clear();
        return any;
    }

    public void Clear()
    {
        for (int i = 0; i < shells.Count; i++)
        {
            if (shells[i] != null) Destroy(shells[i]);
        }

        shells.Clear();
        if (material != null)
        {
            Destroy(material);
            material = null;
        }
    }

    private void OnDestroy()
    {
        Clear();
    }

    private bool TryShell(DistrictZone zone)
    {
        MeshFilter filter = zone.GetComponent<MeshFilter>();
        if (filter == null) filter = zone.GetComponentInChildren<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) return false;

        GameObject shell = new GameObject(ShellName);
        shell.transform.SetParent(filter.transform, false);
        shell.transform.localPosition = Vector3.zero;
        shell.transform.localRotation = Quaternion.identity;
        shell.transform.localScale = Vector3.one;

        shell.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
        MeshRenderer renderer = shell.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        shells.Add(shell);
        return true;
    }

    private static bool TryReadColor(List<DistrictZone> zones, out Color color)
    {
        for (int i = 0; i < zones.Count; i++)
        {
            DistrictZone zone = zones[i];
            if (zone == null) continue;
            MeshRenderer renderer = zone.GetComponent<MeshRenderer>();
            if (renderer == null) renderer = zone.GetComponentInChildren<MeshRenderer>();
            Material source = renderer != null ? renderer.sharedMaterial : null;
            if (source == null) continue;

            if (source.HasProperty("_BaseColor"))
            {
                color = source.GetColor("_BaseColor");
                return true;
            }

            if (source.HasProperty("_Color"))
            {
                color = source.GetColor("_Color");
                return true;
            }
        }

        color = Color.white;
        return false;
    }

    private static List<DistrictZone> ZonesOf(Districts district)
    {
        List<DistrictZone> zones = new List<DistrictZone>();
        if (!InfluenceManager.IsNull)
        {
            IReadOnlyList<DistrictZone> playable = InfluenceManager.Get.GetPlayableZones();
            for (int i = 0; i < playable.Count; i++)
            {
                DistrictZone zone = playable[i];
                if (zone != null && zone.IsPlayable && zone.District == district) zones.Add(zone);
            }
        }

        if (zones.Count > 0 || DistrictsManager.IsNull) return zones;

        List<DistrictZone> fallback = DistrictsManager.GetDistrictZones(district);
        for (int i = 0; i < fallback.Count; i++)
        {
            if (fallback[i] != null && fallback[i].IsPlayable) zones.Add(fallback[i]);
        }

        return zones;
    }
}
