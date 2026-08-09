using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DefaultExecutionOrder(-20)]
public class DistrictSelectionController : MonoBehaviour
{
    public static Districts? SelectedDistrict { get; private set; }
    public static DistrictZone SelectedZone { get; private set; }
    public static string LastSelectedZoneName { get; private set; } = string.Empty;
    public static string LastSelectedPartColorName { get; private set; } = string.Empty;
    public static event Action<Districts?> OnSelectionChanged;
    public static event Action<DistrictZone> OnZoneDoubleClicked;

    public DistrictColorMapping ColorMapping => colorMapping;

    [Header("Input")]
    [SerializeField] private bool enableSelection = true;
    [SerializeField] private LayerMask selectionMask = ~0;
    [SerializeField] private Camera selectionCamera;
    [SerializeField] private bool blockClicksOverUI = true;
    [SerializeField] private bool verboseLogs;
    [SerializeField] private float doubleClickMaxDelay = 0.35f;

    [Header("Pick Feel")]
    [Tooltip("Near-ties along the ray: hits within this distance of the closest zone compete by screen proximity.")]
    [SerializeField] private float pickDepthSlack = 0.35f;
    [Tooltip("Small depth penalty so clearly closer hits still win inside the slack window.")]
    [SerializeField] private float pickDepthTieBreak = 8f;

    [Header("Street / Gap Pick")]
    [Tooltip("Max world distance outside a cuadra (past its size) to still select it from a street/gap click.")]
    [SerializeField] private float nearestZoneMaxDistance = 1.25f;

    [Header("Map")]
    [SerializeField] private bool autoSetupMapOnStart = true;
    [SerializeField] private string mapObjectName = "mapa por distritos 1";
    [SerializeField] private DistrictColorMapping colorMapping;

    private bool mapSetupComplete;
    private bool warnedMissingCamera;
    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();
    private float lastZoneClickTime = -999f;
    private DistrictZone lastClickedZone;

    private void Awake()
    {
        ResolveSelectionCamera();
        if (!autoSetupMapOnStart) return;
        TrySetupMapInScene();
    }

    private void Start()
    {
        ResolveSelectionCamera();
        MapCameraController.EnsureOnCamera(GetSelectionCamera());
        if (!mapSetupComplete && autoSetupMapOnStart)
        {
            TrySetupMapInScene();
        }
    }

    private void ResolveSelectionCamera()
    {
        if (selectionCamera != null) return;
        selectionCamera = Camera.main;
        if (selectionCamera == null)
        {
            selectionCamera = FindAnyObjectByType<Camera>();
        }
    }

    public void TrySetupMapInScene()
    {
        GameObject mapObject = FindMapRoot();
        if (mapObject == null)
        {
            Debug.LogWarning($"DistrictSelectionController: could not find map '{mapObjectName}'.", this);
            return;
        }

        DistrictMapBootstrap bootstrap = mapObject.GetComponent<DistrictMapBootstrap>();
        if (bootstrap == null) bootstrap = mapObject.AddComponent<DistrictMapBootstrap>();

        bootstrap.Configure(mapObject.transform, colorMapping);
        bootstrap.SetupMap();
        DistrictsManager.RefreshZones();
        mapSetupComplete = true;

        InfluenceSystemBootstrap.EnsureInScene();
        if (!InfluenceManager.IsNull)
        {
            InfluenceManager.Get.RefreshZoneCache();
            InfluenceManager.Get.EnsureZonesInitialized();
            InfluenceManager.Get.RebuildAdjacency();
        }

        if (verboseLogs)
        {
            Debug.Log($"DistrictSelectionController: map setup complete on '{mapObject.name}'.", this);
            Debug.Log(DistrictSelectionDebugOverlay.BuildSetupReport(colorMapping), this);
        }
    }

    private GameObject FindMapRoot()
    {
        if (!string.IsNullOrWhiteSpace(mapObjectName))
        {
            GameObject byName = GameObject.Find(mapObjectName);
            if (byName != null) return byName;

            string cloneName = mapObjectName + "(Clone)";
            byName = GameObject.Find(cloneName);
            if (byName != null) return byName;
        }

        DistrictMapBootstrap existing = FindAnyObjectByType<DistrictMapBootstrap>();
        if (existing != null) return existing.gameObject;

        DistrictZone anyZone = FindAnyObjectByType<DistrictZone>();
        if (anyZone != null)
        {
            DistrictPart part = anyZone.GetComponentInParent<DistrictPart>();
            if (part != null) return part.transform.root.gameObject;
        }

        return GameObject.Find("mapa");
    }

    private void Update()
    {
        if (!enableSelection) return;
        if (!TryGetPrimaryClickDown(out Vector2 mousePosition)) return;
        if (blockClicksOverUI && IsPointerOverUi(mousePosition)) return;

        Camera targetCamera = GetSelectionCamera();
        if (targetCamera == null)
        {
            if (!warnedMissingCamera)
            {
                Debug.LogWarning("DistrictSelectionController: no camera found.", this);
                warnedMissingCamera = true;
            }
            return;
        }

        warnedMissingCamera = false;

        Ray ray = targetCamera.ScreenPointToRay(mousePosition);
        int mask = selectionMask.value == 0 ? Physics.DefaultRaycastLayers : selectionMask.value;
        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, mask, QueryTriggerInteraction.Collide);

        DistrictZone zone = null;
        bool hitZone = hits != null
            && hits.Length > 0
            && TryPickBestZone(hits, targetCamera, mousePosition, out zone);

        if (!hitZone && !TryPickNearestZoneFromMapPoint(ray, hits, out zone))
        {
            SetSelectedDistrict(null, null, string.Empty, string.Empty);
            lastClickedZone = null;
            if (verboseLogs) Debug.Log("DistrictSelectionController: click sin DistrictZone.", this);
            return;
        }

        string partColorName = ResolvePartColorName(zone);
        SetSelectedDistrict(zone.District, zone, zone.name, partColorName);
        RegisterZoneClick(zone);

        if (verboseLogs)
        {
            Debug.Log(FormatSelectionLog(zone.District, partColorName, zone.name, colorMapping), this);
        }
    }

    /// <summary>
    /// Each cuadra is its own Curve mesh. On street/gap misses, project the click onto the map
    /// and select the nearest DistrictZone only if it is within nearestZoneMaxDistance.
    /// </summary>
    private bool TryPickNearestZoneFromMapPoint(Ray ray, RaycastHit[] hits, out DistrictZone nearestZone)
    {
        nearestZone = null;

        DistrictZone[] zones = FindObjectsByType<DistrictZone>(FindObjectsSortMode.None);
        if (zones == null || zones.Length == 0) return false;
        if (!TryResolveMapHitPoint(ray, hits, zones, out Vector3 point)) return false;

        float maxDistance = Mathf.Max(0.01f, nearestZoneMaxDistance);
        DistrictZone best = null;
        float bestOutsideDist = float.PositiveInfinity;
        float bestCenterDist = float.PositiveInfinity;

        for (int i = 0; i < zones.Length; i++)
        {
            DistrictZone candidate = zones[i];
            if (candidate == null || !candidate.isActiveAndEnabled) continue;
            if (!candidate.IsPlayable) continue;

            Bounds bounds = ResolveZoneBounds(candidate);
            float centerDist = HorizontalDistance(point, bounds.center);
            float halfExtent = 0.5f * Mathf.Max(bounds.size.x, bounds.size.z);
            // How far the click is past the cuadra's footprint (0 = on/inside the block).
            float outsideDist = Mathf.Max(0f, centerDist - halfExtent);
            if (outsideDist > maxDistance) continue;

            if (outsideDist < bestOutsideDist - 0.0001f
                || (Mathf.Abs(outsideDist - bestOutsideDist) <= 0.0001f && centerDist < bestCenterDist))
            {
                best = candidate;
                bestOutsideDist = outsideDist;
                bestCenterDist = centerDist;
            }
        }

        if (best == null) return false;

        nearestZone = best;
        return true;
    }

    private static bool TryResolveMapHitPoint(Ray ray, RaycastHit[] hits, DistrictZone[] zones, out Vector3 point)
    {
        point = default;

        // Prefer a real collider hit on the map that isn't a district cuadra (e.g. ground plane).
        if (hits != null && hits.Length > 0)
        {
            float bestDist = float.PositiveInfinity;
            bool found = false;
            Vector3 bestPoint = default;

            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider == null) continue;
                if (hits[i].collider.GetComponentInParent<DistrictZone>() != null) continue;
                if (hits[i].distance >= bestDist) continue;

                bestDist = hits[i].distance;
                bestPoint = hits[i].point;
                found = true;
            }

            if (found)
            {
                point = bestPoint;
                return true;
            }
        }

        float groundY = EstimateMapGroundY(zones);
        Plane ground = new Plane(Vector3.up, new Vector3(0f, groundY, 0f));
        if (ground.Raycast(ray, out float enter) && enter >= 0f)
        {
            point = ray.GetPoint(enter);
            return true;
        }

        ground = new Plane(Vector3.up, Vector3.zero);
        if (ground.Raycast(ray, out enter) && enter >= 0f)
        {
            point = ray.GetPoint(enter);
            return true;
        }

        return false;
    }

    private static float EstimateMapGroundY(DistrictZone[] zones)
    {
        float sum = 0f;
        int count = 0;
        for (int i = 0; i < zones.Length; i++)
        {
            if (zones[i] == null) continue;
            sum += ResolveZoneBounds(zones[i]).min.y;
            count++;
            if (count >= 8) break;
        }

        return count > 0 ? sum / count : 0f;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private bool TryPickBestZone(RaycastHit[] hits, Camera cam, Vector2 clickScreen, out DistrictZone bestZone)
    {
        bestZone = null;

        float closestDistance = float.PositiveInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            DistrictZone zone = hits[i].collider.GetComponentInParent<DistrictZone>();
            if (zone == null || !zone.IsPlayable) continue;
            if (hits[i].distance < closestDistance) closestDistance = hits[i].distance;
        }

        if (float.IsPositiveInfinity(closestDistance)) return false;

        float bestScore = float.PositiveInfinity;
        float depthLimit = closestDistance + Mathf.Max(0f, pickDepthSlack);

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.distance > depthLimit) continue;

            DistrictZone zone = hit.collider.GetComponentInParent<DistrictZone>();
            if (zone == null || !zone.IsPlayable) continue;

            float score = ScoreZoneHit(zone, hit, cam, clickScreen);
            if (score >= bestScore) continue;

            bestScore = score;
            bestZone = zone;
        }

        return bestZone != null;
    }

    private float ScoreZoneHit(DistrictZone zone, RaycastHit hit, Camera cam, Vector2 clickScreen)
    {
        // Screen proximity breaks near-ties (angled cam / overlapping edges).
        // Depth still matters so a clearly closer district keeps the click.
        Bounds bounds = ResolveZoneBounds(zone);
        Vector3 screenCenter = cam.WorldToScreenPoint(bounds.center);
        float screenDist = Vector2.Distance(clickScreen, new Vector2(screenCenter.x, screenCenter.y));

        float depthPenalty = (hit.distance) * Mathf.Max(0f, pickDepthTieBreak);
        float colliderBias = hit.collider is MeshCollider ? 0f : 80f;
        float facing = Mathf.Clamp01(Vector3.Dot(hit.normal.normalized, -cam.transform.forward));
        float facingBias = (1f - facing) * 30f;

        return screenDist + depthPenalty + colliderBias + facingBias;
    }

    private static Bounds ResolveZoneBounds(DistrictZone zone)
    {
        Renderer renderer = zone.GetComponent<Renderer>();
        if (renderer == null) renderer = zone.GetComponentInChildren<Renderer>();
        if (renderer != null) return renderer.bounds;

        Collider col = zone.GetComponent<Collider>();
        if (col == null) col = zone.GetComponentInChildren<Collider>();
        if (col != null) return col.bounds;

        return new Bounds(zone.transform.position, Vector3.one);
    }

    private void RegisterZoneClick(DistrictZone zone)
    {
        float now = Time.unscaledTime;
        bool isDoubleClick = zone != null
            && zone == lastClickedZone
            && (now - lastZoneClickTime) <= doubleClickMaxDelay;

        lastZoneClickTime = now;
        lastClickedZone = zone;

        if (isDoubleClick)
        {
            OnZoneDoubleClicked?.Invoke(zone);
        }
    }

    public static void SetSelectedDistrict(Districts? district)
    {
        SetSelectedDistrict(district, null, null, null);
    }

    public static void SetSelectedDistrict(Districts? district, string hitObjectName)
    {
        SetSelectedDistrict(district, null, hitObjectName, null);
    }

    public static void SetSelectedDistrict(Districts? district, string hitObjectName, string partColorName)
    {
        SetSelectedDistrict(district, null, hitObjectName, partColorName);
    }

    public static void SetSelectedDistrict(Districts? district, DistrictZone zone, string hitObjectName, string partColorName)
    {
        if (zone != null && !zone.IsPlayable) zone = null;

        DistrictZone previousZone = SelectedZone;
        if (previousZone != null && previousZone != zone)
        {
            previousZone.SetSelected(false);
        }

        SelectedZone = zone;

        if (!string.IsNullOrEmpty(hitObjectName))
        {
            LastSelectedZoneName = hitObjectName;
        }
        else if (!district.HasValue)
        {
            LastSelectedZoneName = string.Empty;
        }

        if (!string.IsNullOrEmpty(partColorName))
        {
            LastSelectedPartColorName = partColorName;
        }
        else if (!district.HasValue)
        {
            LastSelectedPartColorName = string.Empty;
        }

        SelectedDistrict = district;

        if (SelectedZone != null)
        {
            SelectedZone.SetSelected(true);
        }

        OnSelectionChanged?.Invoke(SelectedDistrict);
    }

    public static string ResolvePartColorName(DistrictZone zone)
    {
        if (zone == null) return string.Empty;

        DistrictPart part = zone.GetComponentInParent<DistrictPart>();
        if (part != null) return part.gameObject.name;

        if (DistrictColorMapping.TryParseColorKeyFromObjectName(zone.name, out string colorKey))
        {
            return colorKey;
        }

        return string.Empty;
    }

    public static string FormatSelectionLog(Districts district, string partColorName, string zoneName, DistrictColorMapping mapping)
    {
        string mappingLine = "mapping asset: (sin asset)";
        if (mapping != null && !string.IsNullOrEmpty(partColorName))
        {
            if (mapping.TryGetDistrictForPart(partColorName, out Districts expectedFromColor))
            {
                bool ok = expectedFromColor == district;
                mappingLine = ok
                    ? $"mapping asset: {partColorName} → {expectedFromColor} ✓"
                    : $"mapping asset: {partColorName} → {expectedFromColor} ✗ (zona tiene {district})";
            }
            else
            {
                mappingLine = $"mapping asset: '{partColorName}' NO está en District Color Mapping ✗";
            }
        }

        string colorsForDistrict = mapping != null
            ? mapping.FormatDistrictWithColors(district)
            : district.ToString();

        return $"[District click] {colorsForDistrict} | carpeta/color: {partColorName} | mesh: {zoneName} | {mappingLine}";
    }

    private static bool TryGetPrimaryClickDown(out Vector2 mousePosition)
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            mousePosition = Mouse.current.position.ReadValue();
            return true;
        }

        if (Touchscreen.current != null)
        {
            UnityEngine.InputSystem.Controls.TouchControl touch = Touchscreen.current.primaryTouch;
            if (touch.press.wasPressedThisFrame)
            {
                mousePosition = touch.position.ReadValue();
                return true;
            }
        }

        if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
        {
            mousePosition = Pointer.current.position.ReadValue();
            return true;
        }

        mousePosition = default;
        return false;
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
        // OnGUI panels (influence debug / cleric assign) are not EventSystem graphics.
        if (OnGuiClickBlocker.IsPointerOverBlockedArea(screenPosition)) return true;

        if (EventSystem.current == null) return false;

        PointerEventData pointerData = new PointerEventData(EventSystem.current) { position = screenPosition };
        uiRaycastResults.Clear();
        EventSystem.current.RaycastAll(pointerData, uiRaycastResults);

        for (int i = 0; i < uiRaycastResults.Count; i++)
        {
            RaycastResult result = uiRaycastResults[i];
            if (result.gameObject == null) continue;
            if (!result.gameObject.TryGetComponent(out Graphic graphic)) continue;
            if (!graphic.raycastTarget) continue;
            return true;
        }

        return false;
    }

    private Camera GetSelectionCamera()
    {
        if (selectionCamera != null) return selectionCamera;
        if (Camera.main != null) return Camera.main;

        Camera[] cameras = Camera.allCameras;
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera cam = cameras[i];
            if (cam != null && cam.enabled && cam.gameObject.activeInHierarchy) return cam;
        }

        return FindAnyObjectByType<Camera>();
    }
}
