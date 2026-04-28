using System;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class DistrictMeshView : MonoBehaviour
{
    public enum DistrictAction
    {
        Seed,
        Stats
    }

    public static event Action<Districts> OnDistrictSelected;
    public static event Action<Districts, DistrictAction> OnDistrictActionSelected;

    private enum BuildMode
    {
        SingleDistrict,
        AllDistricts
    }

    [Serializable]
    private struct DistrictColorOverride
    {
        public Districts district;
        public Color color;
    }

    [Header("Build")]
    [SerializeField] private BuildMode buildMode = BuildMode.SingleDistrict;
    [SerializeField] private Districts district = Districts.District1;
    [SerializeField] private bool rebuildOnStart = true;
    [SerializeField] private bool sortPointsByAngle = false;
    [SerializeField] private float yOffset = 0.05f;
    [SerializeField] private float pointMergeEpsilon = 0.001f;

    [Header("Rendering")]
    [SerializeField] private Material districtMaterial;
    [SerializeField] private Shader districtShader;
    [SerializeField] private bool createMaterialFromShader = true;
    [SerializeField] private string shaderName = "Custom/DistrictOverlay";
    [SerializeField] private List<string> shaderFallbackNames = new List<string>
    {
        "Universal Render Pipeline/Unlit",
        "Universal Render Pipeline/Lit",
        "Standard"
    };
    [SerializeField] private Color color = new Color(1f, 0.9f, 0.1f, 0.35f);
    [SerializeField] private List<DistrictColorOverride> districtColorOverrides = new List<DistrictColorOverride>();
    
    [Header("Interaction")]
    [SerializeField] private bool enableDistrictSelection = true;
    [SerializeField] private LayerMask selectionMask = ~0;
    [SerializeField] private Camera selectionCamera;
    [SerializeField] private bool blockClicksOverUI = true;
    [SerializeField] private bool verboseInteractionLogs = false;
    [SerializeField] private Vector2 contextMenuSize = new Vector2(160f, 96f);
    [SerializeField] private Vector2 contextMenuOffset = new Vector2(12f, 12f);

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh districtMesh;
    private Material singleRuntimeMaterial;

    private readonly Dictionary<Districts, Mesh> generatedDistrictMeshes = new Dictionary<Districts, Mesh>();
    private readonly Dictionary<Districts, Material> generatedDistrictMaterials = new Dictionary<Districts, Material>();
    private readonly Dictionary<Districts, GameObject> generatedDistrictObjects = new Dictionary<Districts, GameObject>();
    private readonly Dictionary<Collider, Districts> selectableDistrictByCollider = new Dictionary<Collider, Districts>();

    private bool contextMenuOpen;
    private Districts contextMenuDistrict;
    private Rect contextMenuRect;
    private bool warnedMissingCamera;
    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
    }

    private void Start()
    {
        if (rebuildOnStart)
        {
            Rebuild();
        }
    }

    private void Update()
    {
        if (!enableDistrictSelection) return;
        if (!TryGetPrimaryClickDown(out Vector2 mousePosition)) return;

        if (blockClicksOverUI && IsPointerOverUi(mousePosition))
        {
            return;
        }

        if (IsPointerOverContextMenu(mousePosition))
        {
            return;
        }

        Camera targetCamera = GetSelectionCamera();
        if (targetCamera == null)
        {
            if (!warnedMissingCamera)
            {
                Debug.LogWarning("DistrictMeshView: no camera found for district selection. Assign Selection Camera or tag one camera as MainCamera.", this);
                warnedMissingCamera = true;
            }
            return;
        }
        warnedMissingCamera = false;

        Ray ray = targetCamera.ScreenPointToRay(mousePosition);
        int mask = selectionMask.value == 0 ? Physics.DefaultRaycastLayers : selectionMask.value;
        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, mask, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0)
        {
            contextMenuOpen = false;
            if (verboseInteractionLogs)
            {
                Debug.Log("DistrictMeshView: raycast missed all colliders.", this);
            }
            return;
        }

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        bool foundDistrictHit = false;
        RaycastHit selectedHit = default;
        Districts selectedDistrict = default;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit currentHit = hits[i];
            if (!selectableDistrictByCollider.TryGetValue(currentHit.collider, out Districts districtHit)) continue;

            selectedHit = currentHit;
            selectedDistrict = districtHit;
            foundDistrictHit = true;
            break;
        }

        if (!foundDistrictHit)
        {
            contextMenuOpen = false;
            if (verboseInteractionLogs)
            {
                Debug.Log($"DistrictMeshView: raycast hit {hits.Length} collider(s), but none are district colliders.", this);
            }
            return;
        }

        contextMenuDistrict = selectedDistrict;
        OnDistrictSelected?.Invoke(contextMenuDistrict);

        Vector3 screenPoint = targetCamera.WorldToScreenPoint(selectedHit.point);
        float menuX = Mathf.Clamp(screenPoint.x + contextMenuOffset.x, 0f, Screen.width - contextMenuSize.x);
        float menuY = Mathf.Clamp(Screen.height - screenPoint.y + contextMenuOffset.y, 0f, Screen.height - contextMenuSize.y);
        contextMenuRect = new Rect(menuX, menuY, contextMenuSize.x, contextMenuSize.y);
        contextMenuOpen = true;

        if (verboseInteractionLogs)
        {
            Debug.Log($"DistrictMeshView: selected {contextMenuDistrict}.", this);
        }
    }

    private static bool TryGetPrimaryClickDown(out Vector2 mousePosition)
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
        {
            mousePosition = default;
            return false;
        }

        mousePosition = Mouse.current.position.ReadValue();
        return true;
#else
        if (!Input.GetMouseButtonDown(0))
        {
            mousePosition = default;
            return false;
        }

        mousePosition = Input.mousePosition;
        return true;
#endif
    }

    private bool IsPointerOverUi(Vector2 screenPosition)
    {
        if (EventSystem.current == null) return false;

        if (EventSystem.current.IsPointerOverGameObject())
        {
            return true;
        }

        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = screenPosition
        };

        uiRaycastResults.Clear();
        EventSystem.current.RaycastAll(pointerData, uiRaycastResults);
        return uiRaycastResults.Count > 0;
    }

    private bool IsPointerOverContextMenu(Vector2 screenPosition)
    {
        if (!contextMenuOpen) return false;

        // Input uses bottom-left origin, IMGUI Rect uses top-left origin.
        Vector2 guiPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
        return contextMenuRect.Contains(guiPosition);
    }

    private void OnGUI()
    {
        if (!enableDistrictSelection || !contextMenuOpen) return;

        GUILayout.BeginArea(contextMenuRect, GUI.skin.box);
        GUILayout.Label(contextMenuDistrict.ToString());

        if (GUILayout.Button("Seed"))
        {
            OnDistrictActionSelected?.Invoke(contextMenuDistrict, DistrictAction.Seed);
            Debug.Log($"DistrictMeshView: Seed action requested for {contextMenuDistrict}.", this);
            contextMenuOpen = false;
        }

        if (GUILayout.Button("Stats"))
        {
            OnDistrictActionSelected?.Invoke(contextMenuDistrict, DistrictAction.Stats);
            Debug.Log($"DistrictMeshView: Stats action requested for {contextMenuDistrict}.", this);
            contextMenuOpen = false;
        }

        if (GUILayout.Button("Close"))
        {
            contextMenuOpen = false;
        }

        GUILayout.EndArea();
    }

    [ContextMenu("Rebuild District Mesh")]
    public void Rebuild()
    {
        EnsureCoreComponents();

        if (buildMode == BuildMode.AllDistricts)
        {
            RebuildAllDistricts();
            return;
        }

        RebuildSingleDistrict();
    }

    private void RebuildSingleDistrict()
    {
        ClearGeneratedDistrictObjects();
        EnsureMainRendererEnabled(true);

        if (!TryBuildPolygon(district, out List<Vector2> polygon))
        {
            ClearMesh();
            return;
        }

        if (!TriangulateEarClipping(polygon, out List<int> triangles))
        {
            Debug.LogWarning($"DistrictMeshView: triangulation failed for {district} on {name}.", this);
            ClearMesh();
            return;
        }

        BuildMesh(district, polygon, triangles, out districtMesh);
        meshFilter.sharedMesh = districtMesh;
        ApplyMaterial(meshRenderer, district, color, ref singleRuntimeMaterial);
        RegisterSelectableCollider(gameObject, district, districtMesh);
    }

    private void RebuildAllDistricts()
    {
        ClearMesh();
        EnsureMainRendererEnabled(false);
        ClearGeneratedDistrictObjects();
        selectableDistrictByCollider.Clear();

        Array districtValues = Enum.GetValues(typeof(Districts));
        for (int i = 0; i < districtValues.Length; i++)
        {
            Districts districtValue = (Districts)districtValues.GetValue(i);
            if (!TryBuildPolygon(districtValue, out List<Vector2> polygon)) continue;

            if (!TriangulateEarClipping(polygon, out List<int> triangles))
            {
                Debug.LogWarning($"DistrictMeshView: triangulation failed for {districtValue} on {name}.", this);
                continue;
            }

            BuildMesh(districtValue, polygon, triangles, out Mesh mesh);

            if (!generatedDistrictObjects.TryGetValue(districtValue, out GameObject districtObject) || districtObject == null)
            {
                districtObject = new GameObject($"DistrictMesh_{districtValue}");
                districtObject.transform.SetParent(transform, false);
                districtObject.layer = gameObject.layer;
                generatedDistrictObjects[districtValue] = districtObject;
            }

            if (!districtObject.TryGetComponent(out MeshFilter childFilter))
            {
                childFilter = districtObject.AddComponent<MeshFilter>();
            }

            if (!districtObject.TryGetComponent(out MeshRenderer childRenderer))
            {
                childRenderer = districtObject.AddComponent<MeshRenderer>();
            }

            childFilter.sharedMesh = mesh;
            Color districtColor = GetDistrictColor(districtValue);

            Material materialRef = null;
            ApplyMaterial(childRenderer, districtValue, districtColor, ref materialRef);
            generatedDistrictMaterials[districtValue] = materialRef;
            RegisterSelectableCollider(districtObject, districtValue, mesh);
        }
    }

    private bool TryBuildPolygon(Districts targetDistrict, out List<Vector2> polygon)
    {
        polygon = new List<Vector2>();

        List<Node> nodes = GetNodesForDistrict(targetDistrict);
        if (nodes.Count < 3)
        {
            Debug.LogWarning($"DistrictMeshView: district {targetDistrict} has fewer than 3 nodes.", this);
            return false;
        }

        float mergeSqr = pointMergeEpsilon * pointMergeEpsilon;
        for (int i = 0; i < nodes.Count; i++)
        {
            Node node = nodes[i];
            if (node == null) continue;

            Vector3 pos = node.transform.position;
            Vector2 point = new Vector2(pos.x, pos.z);

            bool duplicate = false;
            for (int p = 0; p < polygon.Count; p++)
            {
                if ((polygon[p] - point).sqrMagnitude <= mergeSqr)
                {
                    duplicate = true;
                    break;
                }
            }

            if (!duplicate)
            {
                polygon.Add(point);
            }
        }

        if (polygon.Count < 3)
        {
            Debug.LogWarning($"DistrictMeshView: district {targetDistrict} has fewer than 3 unique points.", this);
            return false;
        }

        if (sortPointsByAngle)
        {
            SortByAngle(polygon);
        }

        if (HasSelfIntersections(polygon))
        {
            Debug.LogWarning($"DistrictMeshView: district {targetDistrict} polygon is self-intersecting. Adjust node order.", this);
            return false;
        }

        EnsureCounterClockwise(polygon);
        return true;
    }

    private List<Node> GetNodesForDistrict(Districts targetDistrict)
    {
        List<Node> nodes = DistrictsManager.GetDistrictNodes(targetDistrict);
        if (nodes.Count >= 3) return nodes;

        // Rebuild can be invoked from inspector/context menu in edit mode, before runtime caches are populated.
        if (Application.isPlaying) return nodes;

        Node[] sceneNodes = FindObjectsByType<Node>(FindObjectsSortMode.None);
        List<Node> fallbackNodes = new List<Node>(sceneNodes.Length);
        for (int i = 0; i < sceneNodes.Length; i++)
        {
            Node node = sceneNodes[i];
            if (node == null) continue;
            if (node.District != targetDistrict) continue;
            fallbackNodes.Add(node);
        }

        return fallbackNodes.Count > 0 ? fallbackNodes : nodes;
    }

    private void BuildMesh(Districts targetDistrict, List<Vector2> polygon, List<int> triangles, out Mesh mesh)
    {
        if (!generatedDistrictMeshes.TryGetValue(targetDistrict, out mesh) || mesh == null)
        {
            mesh = new Mesh { name = $"District_{targetDistrict}_Mesh" };
            generatedDistrictMeshes[targetDistrict] = mesh;
        }
        else
        {
            mesh.Clear();
        }

        Vector3[] vertices = new Vector3[polygon.Count];
        for (int i = 0; i < polygon.Count; i++)
        {
            vertices[i] = new Vector3(polygon[i].x, yOffset, polygon[i].y);
        }

        Vector2[] uv = BuildUVs(polygon);

        mesh.SetVertices(vertices);
        mesh.SetTriangles(BuildDoubleSidedTriangles(triangles), 0);
        mesh.SetUVs(0, new List<Vector2>(uv));
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
    }

    private static List<int> BuildDoubleSidedTriangles(List<int> triangles)
    {
        List<int> allTriangles = new List<int>(triangles.Count * 2);
        allTriangles.AddRange(triangles);

        for (int i = 0; i < triangles.Count; i += 3)
        {
            allTriangles.Add(triangles[i]);
            allTriangles.Add(triangles[i + 2]);
            allTriangles.Add(triangles[i + 1]);
        }

        return allTriangles;
    }

    private Vector2[] BuildUVs(List<Vector2> polygon)
    {
        Vector2 min = polygon[0];
        Vector2 max = polygon[0];

        for (int i = 1; i < polygon.Count; i++)
        {
            min = Vector2.Min(min, polygon[i]);
            max = Vector2.Max(max, polygon[i]);
        }

        Vector2 size = max - min;
        float width = Mathf.Max(size.x, 0.0001f);
        float height = Mathf.Max(size.y, 0.0001f);

        Vector2[] uv = new Vector2[polygon.Count];
        for (int i = 0; i < polygon.Count; i++)
        {
            uv[i] = new Vector2((polygon[i].x - min.x) / width, (polygon[i].y - min.y) / height);
        }

        return uv;
    }

    private void ApplyMaterial(MeshRenderer targetRenderer, Districts targetDistrict, Color targetColor, ref Material runtimeMaterial)
    {
        if (targetRenderer == null) return;

        if (districtMaterial != null)
        {
            targetRenderer.sharedMaterial = districtMaterial;
            return;
        }

        if (!createMaterialFromShader)
        {
            return;
        }

        Shader shader = ResolveShader();
        if (shader == null)
        {
            Debug.LogWarning($"DistrictMeshView: shader '{shaderName}' not found.", this);
            return;
        }

        if (runtimeMaterial == null || runtimeMaterial.shader != shader)
        {
            runtimeMaterial = new Material(shader);
            runtimeMaterial.name = $"District_{targetDistrict}_Mat_{name}";
        }

        if (runtimeMaterial.HasProperty("_Color"))
        {
            runtimeMaterial.SetColor("_Color", targetColor);
        }

        targetRenderer.sharedMaterial = runtimeMaterial;
    }

    private Shader ResolveShader()
    {
        if (districtShader != null)
        {
            return districtShader;
        }

        Shader resolved = Shader.Find(shaderName);
        if (resolved != null) return resolved;

        for (int i = 0; i < shaderFallbackNames.Count; i++)
        {
            string fallbackName = shaderFallbackNames[i];
            if (string.IsNullOrWhiteSpace(fallbackName)) continue;

            resolved = Shader.Find(fallbackName);
            if (resolved != null)
            {
                return resolved;
            }
        }

        return null;
    }

    private void ClearMesh()
    {
        EnsureCoreComponents();

        if (districtMesh != null)
        {
            districtMesh.Clear();
        }

        if (TryGetComponent(out MeshCollider meshCollider))
        {
            selectableDistrictByCollider.Remove(meshCollider);
            meshCollider.sharedMesh = null;
        }

        if (meshFilter == null)
        {
            return;
        }

        meshFilter.sharedMesh = null;
    }

    private void EnsureCoreComponents()
    {
        if (meshFilter == null)
        {
            meshFilter = GetComponent<MeshFilter>();
        }

        if (meshRenderer == null)
        {
            meshRenderer = GetComponent<MeshRenderer>();
        }
    }

    private void EnsureMainRendererEnabled(bool enabled)
    {
        if (meshRenderer != null)
        {
            meshRenderer.enabled = enabled;
        }
    }

    private void ClearGeneratedDistrictObjects()
    {
        foreach (KeyValuePair<Districts, GameObject> entry in generatedDistrictObjects)
        {
            if (entry.Value == null) continue;
            if (entry.Value.TryGetComponent(out Collider col))
            {
                selectableDistrictByCollider.Remove(col);
            }
            Destroy(entry.Value);
        }

        generatedDistrictObjects.Clear();
    }

    private void RegisterSelectableCollider(GameObject targetObject, Districts targetDistrict, Mesh targetMesh)
    {
        if (targetObject == null || targetMesh == null) return;

        if (!targetObject.TryGetComponent(out MeshCollider meshCollider))
        {
            meshCollider = targetObject.AddComponent<MeshCollider>();
        }

        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = targetMesh;
        meshCollider.isTrigger = false;
        meshCollider.enabled = true;
        selectableDistrictByCollider[meshCollider] = targetDistrict;

        if (verboseInteractionLogs)
        {
            Debug.Log($"DistrictMeshView: collider registered for {targetDistrict} on layer {LayerMask.LayerToName(targetObject.layer)}.", targetObject);
        }
    }

    private Camera GetSelectionCamera()
    {
        if (selectionCamera != null) return selectionCamera;

        // Prefer MainCamera first to avoid selecting UI/overlay cameras.
        if (Camera.main != null) return Camera.main;

        Vector2 pointerPosition = GetCurrentPointerPosition();
        Camera[] cameras = Camera.allCameras;
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera cam = cameras[i];
            if (cam == null || !cam.enabled || !cam.gameObject.activeInHierarchy) continue;
            if (!cam.pixelRect.Contains(pointerPosition)) continue;
            return cam;
        }

        return FindAnyObjectByType<Camera>();
    }

    private static Vector2 GetCurrentPointerPosition()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            return Mouse.current.position.ReadValue();
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.mousePosition;
#else
        return Vector2.zero;
#endif
    }

    private Color GetDistrictColor(Districts districtValue)
    {
        for (int i = 0; i < districtColorOverrides.Count; i++)
        {
            if (districtColorOverrides[i].district == districtValue)
            {
                return districtColorOverrides[i].color;
            }
        }

        float hue = (float)districtValue / Mathf.Max(1f, Enum.GetValues(typeof(Districts)).Length);
        Color generatedColor = Color.HSVToRGB(hue, 0.65f, 1f);
        generatedColor.a = color.a;
        return generatedColor;
    }

    private static void SortByAngle(List<Vector2> polygon)
    {
        Vector2 center = Vector2.zero;
        for (int i = 0; i < polygon.Count; i++)
        {
            center += polygon[i];
        }

        center /= polygon.Count;

        polygon.Sort((a, b) =>
        {
            float angleA = Mathf.Atan2(a.y - center.y, a.x - center.x);
            float angleB = Mathf.Atan2(b.y - center.y, b.x - center.x);
            return angleA.CompareTo(angleB);
        });
    }

    private static float SignedArea(List<Vector2> polygon)
    {
        float area = 0f;
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 current = polygon[i];
            Vector2 next = polygon[(i + 1) % polygon.Count];
            area += (current.x * next.y) - (next.x * current.y);
        }

        return area * 0.5f;
    }

    private static void EnsureCounterClockwise(List<Vector2> polygon)
    {
        if (SignedArea(polygon) < 0f)
        {
            polygon.Reverse();
        }
    }

    private static bool TriangulateEarClipping(List<Vector2> polygon, out List<int> triangles)
    {
        triangles = new List<int>();
        if (polygon.Count < 3) return false;

        List<int> indices = new List<int>(polygon.Count);
        for (int i = 0; i < polygon.Count; i++)
        {
            indices.Add(i);
        }

        int guard = polygon.Count * polygon.Count;
        while (indices.Count > 3 && guard > 0)
        {
            bool earFound = false;

            for (int i = 0; i < indices.Count; i++)
            {
                int prev = indices[(i - 1 + indices.Count) % indices.Count];
                int curr = indices[i];
                int next = indices[(i + 1) % indices.Count];

                if (!IsConvex(polygon[prev], polygon[curr], polygon[next])) continue;
                if (TriangleAreaAbs(polygon[prev], polygon[curr], polygon[next]) < 0.000001f) continue;

                bool containsAnyPoint = false;
                for (int p = 0; p < indices.Count; p++)
                {
                    int pointIndex = indices[p];
                    if (pointIndex == prev || pointIndex == curr || pointIndex == next) continue;

                    if (IsPointInTriangle(polygon[pointIndex], polygon[prev], polygon[curr], polygon[next]))
                    {
                        containsAnyPoint = true;
                        break;
                    }
                }

                if (containsAnyPoint) continue;

                triangles.Add(prev);
                triangles.Add(curr);
                triangles.Add(next);
                indices.RemoveAt(i);
                earFound = true;
                break;
            }

            if (!earFound)
            {
                return false;
            }

            guard--;
        }

        if (indices.Count != 3) return false;

        triangles.Add(indices[0]);
        triangles.Add(indices[1]);
        triangles.Add(indices[2]);
        return true;
    }

    private static float TriangleAreaAbs(Vector2 a, Vector2 b, Vector2 c)
    {
        return Mathf.Abs((a.x * (b.y - c.y)) + (b.x * (c.y - a.y)) + (c.x * (a.y - b.y))) * 0.5f;
    }

    private static bool IsConvex(Vector2 a, Vector2 b, Vector2 c)
    {
        float cross = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        return cross > 0f;
    }

    private static bool IsPointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Sign(p, a, b);
        float d2 = Sign(p, b, c);
        float d3 = Sign(p, c, a);

        bool hasNeg = (d1 < 0f) || (d2 < 0f) || (d3 < 0f);
        bool hasPos = (d1 > 0f) || (d2 > 0f) || (d3 > 0f);

        return !(hasNeg && hasPos);
    }

    private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
    }

    private static bool HasSelfIntersections(List<Vector2> polygon)
    {
        int count = polygon.Count;
        for (int i = 0; i < count; i++)
        {
            Vector2 a1 = polygon[i];
            Vector2 a2 = polygon[(i + 1) % count];

            for (int j = i + 1; j < count; j++)
            {
                int aNext = (i + 1) % count;
                int bNext = (j + 1) % count;

                if (i == j || aNext == j || bNext == i) continue;

                Vector2 b1 = polygon[j];
                Vector2 b2 = polygon[bNext];

                if (SegmentsIntersect(a1, a2, b1, b2))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool SegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
    {
        float o1 = Orientation(p1, p2, q1);
        float o2 = Orientation(p1, p2, q2);
        float o3 = Orientation(q1, q2, p1);
        float o4 = Orientation(q1, q2, p2);

        return o1 * o2 < 0f && o3 * o4 < 0f;
    }

    private static float Orientation(Vector2 a, Vector2 b, Vector2 c)
    {
        return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
    }
}
