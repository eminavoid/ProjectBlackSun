using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

public class GrassInstancer : MonoBehaviour
{
    [Header("Grass")]
    public Mesh grassMesh;
    public Material grassMaterial;
    public int grassAmount = 20000;

    [Header("Grid")]
    public Vector2 gridSize = new Vector2(200, 200);
    public float spacing = 1f;

    [Header("Ground Detection")]
    public Collider[] surfaces;
    [Tooltip("Only grow on this sub-mesh of a Mesh Collider surface, e.g. the field's plots. -1 grows on all of it.")]
    public int surfaceSubMesh = -1;
    public float raycastHeight = 500f;
    public float raycastDistance = 1000f;
    public LayerMask groundLayers = ~0;
    public LayerMask ignoredLayers;

    [Header("Rows")]
    [Tooltip("Plants each plot of the Surface Sub Mesh in straight rows that follow its texture's U axis, where the furrows run. Needs a readable Mesh Collider in Surfaces.")]
    public bool plantInRows = true;
    public float rowSpacing = 0.05f;
    public float stemSpacing = 0.02f;
    [Range(0f, 0.5f)] public float rowJitter = 0.1f;
    [Tooltip("Stems face along their row, turned up to this many degrees either way.")]
    [Range(0f, 90f)] public float stemTwist = 20f;

    [Header("Placement Mask")]
    public Texture2D placementMask;
    public Renderer maskArea;
    [Range(0f, 1f)] public float maskThreshold = 0.5f;

    [Header("Placement")]
    public Vector2 randomScale = new Vector2(0.8f, 1.3f);
    public bool randomRotation = true;
    public bool alignToGround = false;
    public Vector2 leanAngles = Vector2.zero;
    public float verticalOffset = 0.02f;
    public int randomSeed = 12345;
    public bool generateOnStart = true;

    [Header("Rendering")]
    public int subMeshIndex = 0;
    public ShadowCastingMode shadowMode = ShadowCastingMode.Off;
    public bool receiveShadows = false;
    public float chunkSize = 10f;
    public float drawDistance = 100f;

    private const int BatchSize = 1023;

    private sealed class Chunk
    {
        public Bounds bounds;
        public Matrix4x4[] matrices;
    }

    private sealed class Plot
    {
        public readonly List<Vector3> corners = new();
        private Vector2 furrow;

        public Vector2 Along => furrow.sqrMagnitude > 1e-12f ? furrow.normalized : Vector2.right;

        public void AddFurrow(Vector3 a, Vector3 b, Vector3 c, Vector2 uvA, Vector2 uvB, Vector2 uvC)
        {
            var ab = new Vector2(b.x - a.x, b.z - a.z);
            var ac = new Vector2(c.x - a.x, c.z - a.z);
            Vector2 uvAB = uvB - uvA, uvAC = uvC - uvA;
            float determinant = uvAB.x * uvAC.y - uvAC.x * uvAB.y;
            if (Mathf.Abs(determinant) < 1e-8f) return;

            Vector2 tangent = (ab * uvAC.y - ac * uvAB.y) / determinant;
            Vector2 weighted = tangent.normalized * Mathf.Abs(ab.x * ac.y - ab.y * ac.x);
            furrow += Vector2.Dot(furrow, weighted) < 0f ? -weighted : weighted;
        }
    }

    private readonly List<Chunk> chunks = new();
    private readonly Plane[] frustum = new Plane[6];

    private void Start()
    {
        if (generateOnStart) GenerateGrass();
    }

    [ContextMenu("Generate Grass")]
    public void GenerateGrass()
    {
        if (grassMesh == null || grassMaterial == null)
        {
            Debug.LogError("Assign Grass Mesh and Grass Material!");
            return;
        }

        if (spacing <= 0 || grassAmount <= 0 || chunkSize <= 0 || rowSpacing <= 0 || stemSpacing <= 0)
        {
            Debug.LogError("Spacing, Row Spacing, Stem Spacing, Chunk Size and Grass Amount must be positive!");
            return;
        }

        if (subMeshIndex < 0 ||
            subMeshIndex >= grassMesh.subMeshCount)
        {
            Debug.LogError("Invalid Sub Mesh Index!");
            return;
        }

        chunks.Clear();

        Random.State previousState = Random.state;
        Random.InitState(randomSeed);

        var cells = new Dictionary<Vector2Int, List<Matrix4x4>>();
        int placed = plantInRows && TryGetPlots(out List<Plot> plots)
            ? PlantRows(plots, cells)
            : Scatter(cells);

        Random.state = previousState;

        if (placed == 0)
        {
            Debug.LogError(
                "ZERO GRASS GENERATED! No raycasts hit the ground. " +
                "Check Surfaces, Ground Layers, raycast height and Placement Mask."
            );
            return;
        }

        float padding = grassMesh.bounds.size.magnitude * randomScale.y;

        foreach (List<Matrix4x4> list in cells.Values)
        {
            var bounds = new Bounds(list[0].GetPosition(), Vector3.zero);
            for (int i = 1; i < list.Count; i++)
                bounds.Encapsulate(list[i].GetPosition());

            bounds.Expand(padding);
            chunks.Add(new Chunk { bounds = bounds, matrices = list.ToArray() });
        }

        grassMaterial.enableInstancing = true;

        Debug.Log($"GRASS GENERATED: {placed} instances in {chunks.Count} chunks.");
    }

    private int Scatter(Dictionary<Vector2Int, List<Matrix4x4>> cells)
    {
        int columns = Mathf.CeilToInt(gridSize.x / spacing);
        int rows = Mathf.CeilToInt(gridSize.y / spacing);
        int validMask = groundLayers.value & ~ignoredLayers.value;
        int placed = 0;

        for (int x = 0; x < columns && placed < grassAmount; x++)
        {
            for (int z = 0; z < rows && placed < grassAmount; z++)
            {
                float offsetX = Random.Range(-spacing * 0.3f, spacing * 0.3f);
                float offsetZ = Random.Range(-spacing * 0.3f, spacing * 0.3f);

                Vector3 origin = transform.position + new Vector3(
                    (x + 0.5f) * spacing - gridSize.x * 0.5f + offsetX,
                    raycastHeight,
                    (z + 0.5f) * spacing - gridSize.y * 0.5f + offsetZ
                );

                if (!TryFindGround(new Ray(origin, Vector3.down), validMask, out RaycastHit hit)) continue;
                if (!OnSurfaceSubMesh(hit)) continue;
                if (Plant(hit.point, hit.normal, randomRotation ? Random.Range(0f, 360f) : 0f, cells)) placed++;
            }
        }

        return placed;
    }

    private int PlantRows(List<Plot> plots, Dictionary<Vector2Int, List<Matrix4x4>> cells)
    {
        int placed = 0;

        foreach (Plot plot in plots)
        {
            Vector2 along = plot.Along;
            var across = new Vector2(-along.y, along.x);
            float rowYaw = Mathf.Atan2(along.x, along.y) * Mathf.Rad2Deg;
            Extents(plot.corners, along, out Vector2 min, out Vector2 max);

            for (float v = min.y + rowSpacing * 0.5f; v < max.y; v += rowSpacing)
            {
                for (float u = min.x + Random.Range(0f, stemSpacing); u < max.x; u += stemSpacing)
                {
                    if (placed >= grassAmount) return placed;

                    float stem = u + Random.Range(-0.3f, 0.3f) * stemSpacing;
                    float row = v + Random.Range(-rowJitter, rowJitter) * rowSpacing;
                    if (!TrySample(plot.corners, along * stem + across * row, out Vector3 point, out Vector3 normal)) continue;
                    if (Plant(point, normal, rowYaw + Random.Range(-stemTwist, stemTwist), cells)) placed++;
                }
            }
        }

        return placed;
    }

    private bool Plant(Vector3 point, Vector3 normal, float yaw, Dictionary<Vector2Int, List<Matrix4x4>> cells)
    {
        if (!PassesMask(point)) return false;

        float scale = Random.Range(randomScale.x, randomScale.y);
        Quaternion rotation = alignToGround ? Quaternion.FromToRotation(Vector3.up, normal) : Quaternion.identity;
        rotation *= Quaternion.Euler(0f, yaw, 0f);
        rotation =Quaternion.Euler(-Random.Range(leanAngles.x, leanAngles.y), 0f, 0f) * rotation;

        Vector3 position = point + normal * verticalOffset;
        var key = new Vector2Int(Mathf.FloorToInt(position.x / chunkSize), Mathf.FloorToInt(position.z / chunkSize));

        if (!cells.TryGetValue(key, out List<Matrix4x4> list))
        {
            list = new List<Matrix4x4>();
            cells.Add(key, list);
        }

        list.Add(Matrix4x4.TRS(position, rotation, Vector3.one * scale));
        return true;
    }

    private bool TryGetPlots(out List<Plot> plots)
    {
        plots = null;
        MeshCollider surface = null;
        if (surfaces != null)
        {
            foreach (Collider candidate in surfaces)
            {
                if (candidate is MeshCollider meshCollider && meshCollider.sharedMesh != null)
                {
                    surface = meshCollider;
                    break;
                }
            }
        }

        Mesh mesh = surface != null ? surface.sharedMesh : null;
        if (mesh == null || !mesh.isReadable || surfaceSubMesh < 0 || surfaceSubMesh >= mesh.subMeshCount)
        {
            Debug.LogWarning("Rows need a readable Mesh Collider in Surfaces and a Surface Sub Mesh. Scattering instead.");
            return false;
        }

        Vector3[] vertices = mesh.vertices;
        for (int i = 0; i < vertices.Length; i++) vertices[i] = surface.transform.TransformPoint(vertices[i]);
        Vector2[] uvs = mesh.uv;
        int[] triangles = mesh.GetTriangles(surfaceSubMesh);

        var weld = new Dictionary<Vector3Int, int>();
        var welded = new int[triangles.Length];
        for (int i = 0; i < triangles.Length; i++)
        {
            Vector3Int key = Vector3Int.RoundToInt(vertices[triangles[i]] * 1000f);
            if (!weld.TryGetValue(key, out int id))
            {
                id = weld.Count;
                weld.Add(key, id);
            }

            welded[i] = id;
        }

        var parent = new int[weld.Count];
        for (int i = 0; i < parent.Length; i++) parent[i] = i;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int root = Root(parent, welded[i]);
            parent[Root(parent, welded[i + 1])] = root;
            parent[Root(parent, welded[i + 2])] = root;
        }

        var byRoot = new Dictionary<int, Plot>();
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int root = Root(parent, welded[i]);
            if (!byRoot.TryGetValue(root, out Plot plot))
            {
                plot = new Plot();
                byRoot.Add(root, plot);
            }

            int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
            plot.corners.Add(vertices[a]);
            plot.corners.Add(vertices[b]);
            plot.corners.Add(vertices[c]);
            if (uvs.Length == vertices.Length) plot.AddFurrow(vertices[a], vertices[b], vertices[c], uvs[a], uvs[b], uvs[c]);
        }

        plots = new List<Plot>(byRoot.Values);
        return true;
    }

    private static int Root(int[] parent, int i)
    {
        while (parent[i] != i) i = parent[i] = parent[parent[i]];
        return i;
    }

    private static void Extents(List<Vector3> plot, Vector2 along, out Vector2 min, out Vector2 max)
    {
        var across = new Vector2(-along.y, along.x);
        min = new Vector2(float.MaxValue, float.MaxValue);
        max = new Vector2(float.MinValue, float.MinValue);

        foreach (Vector3 corner in plot)
        {
            var flat = new Vector2(corner.x, corner.z);
            var local = new Vector2(Vector2.Dot(flat, along), Vector2.Dot(flat, across));
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }
    }

    private static bool TrySample(List<Vector3> plot, Vector2 flat, out Vector3 point, out Vector3 normal)
    {
        for (int i = 0; i < plot.Count; i += 3)
        {
            Vector3 a = plot[i], b = plot[i + 1], c = plot[i + 2];
            var ab = new Vector2(b.x - a.x, b.z - a.z);
            var ac = new Vector2(c.x - a.x, c.z - a.z);
            var ap = new Vector2(flat.x - a.x, flat.y - a.z);
            float determinant = ab.x * ac.y - ab.y * ac.x;
            if (Mathf.Abs(determinant) < 1e-8f) continue;

            float s = (ap.x * ac.y - ap.y * ac.x) / determinant;
            float t = (ab.x * ap.y - ab.y * ap.x) / determinant;
            if (s < 0f || t < 0f || s + t > 1f) continue;

            point = a + (b - a) * s + (c - a) * t;
            normal = Vector3.Cross(b - a, c - a).normalized;
            if (normal.y < 0f) normal = -normal;
            return true;
        }

        point = normal = Vector3.zero;
        return false;
    }

    private bool TryFindGround(Ray ray, int validMask, out RaycastHit hit)
    {
        if (surfaces == null || surfaces.Length == 0)
            return Physics.Raycast(ray, out hit, raycastDistance, validMask);

        hit = default;
        bool found = false;

        for (int i = 0; i < surfaces.Length; i++)
        {
            if (surfaces[i] == null) continue;
            if (!surfaces[i].Raycast(ray, out RaycastHit candidate, raycastDistance)) continue;
            if (found && candidate.distance >= hit.distance) continue;

            hit = candidate;
            found = true;
        }

        return found;
    }

    private bool OnSurfaceSubMesh(RaycastHit hit)
    {
        if (surfaceSubMesh < 0) return true;
        if (!(hit.collider is MeshCollider meshCollider) || meshCollider.sharedMesh == null) return true;

        Mesh mesh = meshCollider.sharedMesh;
        if (surfaceSubMesh >= mesh.subMeshCount) return false;

        SubMeshDescriptor subMesh = mesh.GetSubMesh(surfaceSubMesh);
        int index = hit.triangleIndex * 3;
        return index >= subMesh.indexStart && index < subMesh.indexStart + subMesh.indexCount;
    }

    private bool PassesMask(Vector3 point)
    {
        if (placementMask == null || maskArea == null) return true;

        Bounds area = maskArea.bounds;
        float u = Mathf.InverseLerp(area.min.x, area.max.x, point.x);
        float v = Mathf.InverseLerp(area.min.z, area.max.z, point.z);
        return placementMask.GetPixelBilinear(u, v).r >= maskThreshold;
    }

    private void Update()
    {
        if (chunks.Count == 0)
            return;

        Camera cam = Camera.main;
        if (cam == null)
            return;

        GeometryUtility.CalculateFrustumPlanes(cam, frustum);
        Vector3 eye = cam.transform.position;
        float maxDistanceSqr = drawDistance * drawDistance;

        RenderParams renderParams = new RenderParams(grassMaterial)
        {
            shadowCastingMode = shadowMode,
            receiveShadows = receiveShadows,
            layer = gameObject.layer
        };

        for (int c = 0; c < chunks.Count; c++)
        {
            Chunk chunk = chunks[c];
            if (chunk.bounds.SqrDistance(eye) > maxDistanceSqr) continue;
            if (!GeometryUtility.TestPlanesAABB(frustum, chunk.bounds)) continue;

            renderParams.worldBounds = chunk.bounds;

            for (int i = 0; i < chunk.matrices.Length; i += BatchSize)
            {
                Graphics.RenderMeshInstanced(
                    renderParams,
                    grassMesh,
                    subMeshIndex,
                    chunk.matrices,
                    Mathf.Min(BatchSize, chunk.matrices.Length - i),
                    i
                );
            }
        }
    }

    [ContextMenu("Clear Grass")]
    public void ClearGrass()
    {
        chunks.Clear();

        Debug.Log("Grass cleared.");
    }
}
