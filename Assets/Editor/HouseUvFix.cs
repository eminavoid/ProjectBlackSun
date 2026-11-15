using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class HouseUvFix : AssetPostprocessor
{
    private const string HousesFolder = "/Houses Fixed/";
    private const float FlatSpan = 1e-4f;
    private const float MappedSpan = 0.1f;

    private void OnPostprocessModel(GameObject root)
    {
        if (!assetPath.Replace('\\', '/').Contains(HousesFolder)) return;

        var swapped = new List<string>();
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null || !SwapIfFlipped(mesh)) continue;
            swapped.Add(mesh.name);
        }

        if (swapped.Count > 0)
        {
            Debug.Log($"HouseUvFix: {assetPath} had its UV maps in the wrong order on {swapped.Count} mesh(es); swapped UV0 and UV1: {string.Join(", ", swapped)}");
        }
    }

    private static bool SwapIfFlipped(Mesh mesh)
    {
        var first = new List<Vector2>();
        var second = new List<Vector2>();
        mesh.GetUVs(0, first);
        mesh.GetUVs(1, second);
        if (first.Count == 0 || second.Count != first.Count) return false;
        if (!IsFlat(first) || Span(second) < MappedSpan) return false;

        mesh.SetUVs(0, second);
        mesh.SetUVs(1, first);
        return true;
    }

    private static bool IsFlat(List<Vector2> uvs)
    {
        Bounds(uvs, out Vector2 min, out Vector2 max);
        return max.x - min.x < FlatSpan || max.y - min.y < FlatSpan;
    }

    private static float Span(List<Vector2> uvs)
    {
        Bounds(uvs, out Vector2 min, out Vector2 max);
        return Mathf.Min(max.x - min.x, max.y - min.y);
    }

    private static void Bounds(List<Vector2> uvs, out Vector2 min, out Vector2 max)
    {
        min = new Vector2(float.MaxValue, float.MaxValue);
        max = new Vector2(float.MinValue, float.MinValue);
        foreach (Vector2 uv in uvs)
        {
            min = Vector2.Min(min, uv);
            max = Vector2.Max(max, uv);
        }
    }
}
