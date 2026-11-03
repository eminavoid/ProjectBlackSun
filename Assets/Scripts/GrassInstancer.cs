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
    public float raycastHeight = 500f;
    public float raycastDistance = 1000f;
    public LayerMask groundLayers = ~0;
    public LayerMask ignoredLayers;

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

        if (spacing <= 0 || grassAmount <= 0 || chunkSize <= 0)
        {
            Debug.LogError("Spacing, Chunk Size and Grass Amount must be positive!");
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

        int columns = Mathf.CeilToInt(gridSize.x / spacing);
        int rows = Mathf.CeilToInt(gridSize.y / spacing);

        int validMask =
            groundLayers.value & ~ignoredLayers.value;

        var cells = new Dictionary<Vector2Int, List<Matrix4x4>>();
        int placed = 0;
        int raycastHits = 0;

        for (int x = 0; x < columns && placed < grassAmount; x++)
        {
            for (int z = 0; z < rows && placed < grassAmount; z++)
            {
                float offsetX = Random.Range(-spacing * 0.3f, spacing * 0.3f);
                float offsetZ = Random.Range(-spacing * 0.3f, spacing * 0.3f);
                float scale = Random.Range(randomScale.x, randomScale.y);
                float yaw = Random.Range(0f, 360f);
                float lean = Random.Range(leanAngles.x, leanAngles.y);

                Vector3 origin = transform.position + new Vector3(
                    (x + 0.5f) * spacing - gridSize.x * 0.5f + offsetX,
                    raycastHeight,
                    (z + 0.5f) * spacing - gridSize.y * 0.5f + offsetZ
                );

                if (!TryFindGround(new Ray(origin, Vector3.down), validMask, out RaycastHit hit))
                    continue;

                raycastHits++;

                if (!PassesMask(hit.point))
                    continue;

                Quaternion rotation = Quaternion.identity;

                if (alignToGround)
                    rotation = Quaternion.FromToRotation(
                        Vector3.up, hit.normal);

                if (randomRotation)
                    rotation *= Quaternion.Euler(0, yaw, 0);

                rotation = Quaternion.Euler(-lean, 0, 0) * rotation;

                Vector3 position = hit.point +
                    hit.normal * verticalOffset;

                var key = new Vector2Int(
                    Mathf.FloorToInt(position.x / chunkSize),
                    Mathf.FloorToInt(position.z / chunkSize));

                if (!cells.TryGetValue(key, out List<Matrix4x4> list))
                {
                    list = new List<Matrix4x4>();
                    cells.Add(key, list);
                }

                list.Add(Matrix4x4.TRS(position, rotation, Vector3.one * scale));
                placed++;
            }
        }

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

        Debug.Log(
            $"GRASS GENERATED: {placed} instances in {chunks.Count} chunks. " +
            $"Successful raycasts: {raycastHits}. " +
            $"Grid: {columns} x {rows}."
        );
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
