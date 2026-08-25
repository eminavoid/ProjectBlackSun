using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Malla procedural de una intención: cinta en arco con punta para movimientos,
/// o flecha descendente con anillo cuando no hay origen (plantación de seeds).
/// El color viaja en los vértices para que todas las flechas compartan material.
/// </summary>
[DisallowMultipleComponent]
public class IntentArrowView : MonoBehaviour
{
    private const int ArcSamples = 22;
    private const int RingSegments = 28;

    private static readonly List<Vector3> Vertices = new List<Vector3>();
    private static readonly List<Vector2> Uvs = new List<Vector2>();
    private static readonly List<Color> Colors = new List<Color>();
    private static readonly List<int> Triangles = new List<int>();

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh mesh;
    private TextMesh label;

    public static IntentArrowView Create(Transform parent, Material sharedMaterial)
    {
        GameObject go = new GameObject("IntentArrow");
        go.transform.SetParent(parent, false);

        IntentArrowView view = go.AddComponent<IntentArrowView>();
        view.Initialize(sharedMaterial);
        return view;
    }

    public void SetVisible(bool value)
    {
        gameObject.SetActive(value);
    }

    /// <summary>Cinta en arco desde una base de poder hacia el objetivo.</summary>
    public void BuildArc(Vector3 from, Vector3 to, Color color, ArrowStyle style)
    {
        BeginBuild(from);

        Vector3 localFrom = Vector3.zero;
        Vector3 localTo = to - from;
        Vector3 control = (localFrom + localTo) * 0.5f + Vector3.up * style.ArcHeight;

        AppendArcRibbon(localFrom, control, localTo, color, style);
        EndBuild();
    }

    /// <summary>Marcador vertical sobre el objetivo: anillo en el piso y flecha que baja.</summary>
    public void BuildDrop(Vector3 target, Color color, ArrowStyle style)
    {
        BeginBuild(target);

        Vector3 top = Vector3.up * style.DropHeight;
        AppendCrossedRibbon(top, Vector3.zero, color, style);
        AppendRing(Vector3.zero, style.RingRadius, style.Width * 0.6f, color);

        EndBuild();
    }

    public void SetLabel(string text, Color color, Vector3 localPosition, float scale)
    {
        if (string.IsNullOrEmpty(text))
        {
            if (label != null) label.gameObject.SetActive(false);
            return;
        }

        EnsureLabel();
        label.gameObject.SetActive(true);
        label.transform.localPosition = localPosition;
        label.transform.localScale = Vector3.one * scale;
        label.text = text;
        label.color = color;
    }

    private void Initialize(Material sharedMaterial)
    {
        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshRenderer = gameObject.AddComponent<MeshRenderer>();

        mesh = new Mesh { name = "IntentArrowMesh", hideFlags = HideFlags.HideAndDontSave };
        mesh.MarkDynamic();
        meshFilter.sharedMesh = mesh;

        meshRenderer.sharedMaterial = sharedMaterial;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
    }

    private void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
    }

    private void LateUpdate()
    {
        if (label == null || !label.gameObject.activeSelf) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        label.transform.rotation = Quaternion.LookRotation(label.transform.position - cam.transform.position);
    }

    private void BeginBuild(Vector3 worldOrigin)
    {
        transform.position = worldOrigin;
        transform.rotation = Quaternion.identity;

        Vertices.Clear();
        Uvs.Clear();
        Colors.Clear();
        Triangles.Clear();
    }

    private void EndBuild()
    {
        mesh.Clear();
        mesh.SetVertices(Vertices);
        mesh.SetUVs(0, Uvs);
        mesh.SetColors(Colors);
        mesh.SetTriangles(Triangles, 0);
        mesh.RecalculateBounds();
    }

    private static void AppendArcRibbon(Vector3 p0, Vector3 p1, Vector3 p2, Color color, ArrowStyle style)
    {
        float shaftEnd = Mathf.Clamp01(1f - style.HeadRatio);

        Vector3 previousBinormal = Vector3.right;
        int firstIndex = Vertices.Count;

        for (int i = 0; i < ArcSamples; i++)
        {
            float t = i / (float)(ArcSamples - 1) * shaftEnd;
            Vector3 point = Bezier(p0, p1, p2, t);
            Vector3 tangent = BezierTangent(p0, p1, p2, t);

            Vector3 binormal = Vector3.Cross(tangent, Vector3.up);
            if (binormal.sqrMagnitude < 1e-5f) binormal = previousBinormal;
            else binormal = binormal.normalized;
            previousBinormal = binormal;

            // La cinta se ensancha hacia el destino para reforzar la dirección.
            float u = shaftEnd > 0f ? t / shaftEnd : 0f;
            float halfWidth = style.Width * Mathf.Lerp(0.55f, 1f, u) * 0.5f;

            AddVertex(point - binormal * halfWidth, new Vector2(u, 0f), color);
            AddVertex(point + binormal * halfWidth, new Vector2(u, 1f), color);

            if (i == 0) continue;

            int b = firstIndex + (i - 1) * 2;
            Triangles.Add(b);
            Triangles.Add(b + 1);
            Triangles.Add(b + 2);
            Triangles.Add(b + 1);
            Triangles.Add(b + 3);
            Triangles.Add(b + 2);
        }

        AppendHead(
            Bezier(p0, p1, p2, shaftEnd),
            BezierTangent(p0, p1, p2, shaftEnd),
            p2,
            previousBinormal,
            color,
            style);
    }

    private static void AppendHead(
        Vector3 baseCenter,
        Vector3 tangent,
        Vector3 tip,
        Vector3 binormal,
        Color color,
        ArrowStyle style)
    {
        Vector3 axis = Vector3.Cross(tangent, Vector3.up);
        if (axis.sqrMagnitude > 1e-5f) binormal = axis.normalized;

        float halfWidth = style.Width * style.HeadWidthScale * 0.5f;

        int index = Vertices.Count;
        AddVertex(baseCenter - binormal * halfWidth, new Vector2(0.82f, 0f), color);
        AddVertex(baseCenter + binormal * halfWidth, new Vector2(0.82f, 1f), color);
        AddVertex(tip, new Vector2(1f, 0.5f), color);

        Triangles.Add(index);
        Triangles.Add(index + 1);
        Triangles.Add(index + 2);
    }

    /// <summary>Dos cintas verticales cruzadas: legible desde cualquier ángulo de cámara.</summary>
    private static void AppendCrossedRibbon(Vector3 from, Vector3 to, Color color, ArrowStyle style)
    {
        AppendStraightRibbon(from, to, Vector3.right, color, style);
        AppendStraightRibbon(from, to, Vector3.forward, color, style);
    }

    private static void AppendStraightRibbon(
        Vector3 from,
        Vector3 to,
        Vector3 binormal,
        Color color,
        ArrowStyle style)
    {
        float shaftEnd = Mathf.Clamp01(1f - style.HeadRatio);
        Vector3 shaftTarget = Vector3.Lerp(from, to, shaftEnd);
        float halfWidth = style.Width * 0.5f;

        int index = Vertices.Count;
        AddVertex(from - binormal * halfWidth * 0.55f, new Vector2(0f, 0f), color);
        AddVertex(from + binormal * halfWidth * 0.55f, new Vector2(0f, 1f), color);
        AddVertex(shaftTarget - binormal * halfWidth, new Vector2(shaftEnd, 0f), color);
        AddVertex(shaftTarget + binormal * halfWidth, new Vector2(shaftEnd, 1f), color);

        Triangles.Add(index);
        Triangles.Add(index + 1);
        Triangles.Add(index + 2);
        Triangles.Add(index + 1);
        Triangles.Add(index + 3);
        Triangles.Add(index + 2);

        float headHalfWidth = halfWidth * style.HeadWidthScale;
        int headIndex = Vertices.Count;
        AddVertex(shaftTarget - binormal * headHalfWidth, new Vector2(0.82f, 0f), color);
        AddVertex(shaftTarget + binormal * headHalfWidth, new Vector2(0.82f, 1f), color);
        AddVertex(to, new Vector2(1f, 0.5f), color);

        Triangles.Add(headIndex);
        Triangles.Add(headIndex + 1);
        Triangles.Add(headIndex + 2);
    }

    private static void AppendRing(Vector3 center, float radius, float bandWidth, Color color)
    {
        float inner = Mathf.Max(0.01f, radius - bandWidth * 0.5f);
        float outer = radius + bandWidth * 0.5f;

        int firstIndex = Vertices.Count;

        for (int i = 0; i <= RingSegments; i++)
        {
            float t = i / (float)RingSegments;
            float angle = t * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

            AddVertex(center + dir * inner, new Vector2(t, 0f), color);
            AddVertex(center + dir * outer, new Vector2(t, 1f), color);

            if (i == 0) continue;

            int b = firstIndex + (i - 1) * 2;
            Triangles.Add(b);
            Triangles.Add(b + 1);
            Triangles.Add(b + 2);
            Triangles.Add(b + 1);
            Triangles.Add(b + 3);
            Triangles.Add(b + 2);
        }
    }

    private static void AddVertex(Vector3 position, Vector2 uv, Color color)
    {
        Vertices.Add(position);
        Uvs.Add(uv);
        Colors.Add(color);
    }

    private static Vector3 Bezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float inv = 1f - t;
        return inv * inv * p0 + 2f * inv * t * p1 + t * t * p2;
    }

    private static Vector3 BezierTangent(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        Vector3 tangent = 2f * (1f - t) * (p1 - p0) + 2f * t * (p2 - p1);
        return tangent.sqrMagnitude < 1e-6f ? (p2 - p0) : tangent;
    }

    private void EnsureLabel()
    {
        if (label != null) return;

        GameObject go = new GameObject("IntentLabel");
        go.transform.SetParent(transform, false);

        label = go.AddComponent<TextMesh>();
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.fontSize = 64;
        label.characterSize = 0.5f;
        label.text = string.Empty;
    }

    public struct ArrowStyle
    {
        public float Width;
        public float ArcHeight;
        public float HeadRatio;
        public float HeadWidthScale;
        public float DropHeight;
        public float RingRadius;
    }
}
