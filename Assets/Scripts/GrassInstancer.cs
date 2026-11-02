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
    public float raycastHeight = 500f;
    public float raycastDistance = 1000f;
    public LayerMask groundLayers = ~0;
    public LayerMask ignoredLayers;

    [Header("Placement")]
    public Vector2 randomScale = new Vector2(0.8f, 1.3f);
    public bool randomRotation = true;
    public bool alignToGround = false;
    public float verticalOffset = 0.02f;
    public int randomSeed = 12345;

    [Header("Rendering")]
    public int subMeshIndex = 0;
    public ShadowCastingMode shadowMode = ShadowCastingMode.Off;
    public bool receiveShadows = false;

    private const int BatchSize = 1023;

    private readonly List<Matrix4x4> matrices = new();
    private readonly Matrix4x4[] batchBuffer =
        new Matrix4x4[BatchSize];

    private Bounds grassBounds;
    private bool generated;

    [ContextMenu("Generate Grass")]
    public void GenerateGrass()
    {
        if (grassMesh == null || grassMaterial == null)
        {
            Debug.LogError("Assign Grass Mesh and Grass Material!");
            return;
        }

        if (spacing <= 0 || grassAmount <= 0)
        {
            Debug.LogError("Spacing and Grass Amount must be positive!");
            return;
        }

        if (subMeshIndex < 0 ||
            subMeshIndex >= grassMesh.subMeshCount)
        {
            Debug.LogError("Invalid Sub Mesh Index!");
            return;
        }

        matrices.Clear();
        generated = false;

        Random.InitState(randomSeed);

        int columns = Mathf.CeilToInt(gridSize.x / spacing);
        int rows = Mathf.CeilToInt(gridSize.y / spacing);

        int validMask =
            groundLayers.value & ~ignoredLayers.value;

        int raycastHits = 0;

        bool hasBounds = false;

        for (int x = 0; x < columns && matrices.Count < grassAmount; x++)
        {
            for (int z = 0; z < rows && matrices.Count < grassAmount; z++)
            {
                float offsetX = Random.Range(-spacing * 0.3f, spacing * 0.3f);
                float offsetZ = Random.Range(-spacing * 0.3f, spacing * 0.3f);

                Vector3 origin = transform.position + new Vector3(
                    (x + 0.5f) * spacing - gridSize.x * 0.5f + offsetX,
                    raycastHeight,
                    (z + 0.5f) * spacing - gridSize.y * 0.5f + offsetZ
                );

                if (!Physics.Raycast(
                    origin,
                    Vector3.down,
                    out RaycastHit hit,
                    raycastDistance,
                    validMask))
                {
                    continue;
                }

                raycastHits++;

                float scale = Random.Range(
                    randomScale.x,
                    randomScale.y
                );

                Quaternion rotation = Quaternion.identity;

                if (alignToGround)
                    rotation = Quaternion.FromToRotation(
                        Vector3.up, hit.normal);

                if (randomRotation)
                {
                    rotation *= Quaternion.Euler(
                        0, Random.Range(0f, 360f), 0);
                }

                Vector3 position = hit.point +
                    hit.normal * verticalOffset;

                Matrix4x4 matrix = Matrix4x4.TRS(
                    position,
                    rotation,
                    Vector3.one * scale
                );

                matrices.Add(matrix);

                if (!hasBounds)
                {
                    grassBounds = new Bounds(position, Vector3.one);
                    hasBounds = true;
                }
                else
                {
                    grassBounds.Encapsulate(position);
                }
            }
        }

        if (!hasBounds)
        {
            Debug.LogError(
                "ZERO GRASS GENERATED! No raycasts hit the ground. " +
                "Check Ground Layers, Terrain Collider and raycast height."
            );
            return;
        }

        float extraSize =
            grassMesh.bounds.size.magnitude *
            randomScale.y * 2f + 5f;

        grassBounds.Expand(extraSize);

        grassMaterial.enableInstancing = true;

        generated = true;

        Debug.Log(
            $"GRASS GENERATED: {matrices.Count} instances. " +
            $"Successful raycasts: {raycastHits}. " +
            $"Grid: {columns} x {rows}."
        );
    }

    private void Update()
    {
        if (!generated || matrices.Count == 0)
            return;

        RenderParams renderParams = new RenderParams(grassMaterial)
        {
            worldBounds = grassBounds,
            shadowCastingMode = shadowMode,
            receiveShadows = receiveShadows
        };

        for (int i = 0; i < matrices.Count; i += BatchSize)
        {
            int count = Mathf.Min(
                BatchSize,
                matrices.Count - i
            );

            matrices.CopyTo(i, batchBuffer, 0, count);

            Graphics.RenderMeshInstanced(
                renderParams,
                grassMesh,
                subMeshIndex,
                batchBuffer,
                count
            );
        }
    }

    [ContextMenu("Clear Grass")]
    public void ClearGrass()
    {
        matrices.Clear();
        generated = false;

        Debug.Log("Grass cleared.");
    }
}
