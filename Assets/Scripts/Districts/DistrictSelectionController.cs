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

    public DistrictColorMapping ColorMapping => colorMapping;

    [Header("Input")]
    [SerializeField] private bool enableSelection = true;
    [SerializeField] private LayerMask selectionMask = ~0;
    [SerializeField] private Camera selectionCamera;
    [SerializeField] private bool blockClicksOverUI = true;
    [SerializeField] private bool verboseLogs;

    [Header("Map")]
    [SerializeField] private bool autoSetupMapOnStart = true;
    [SerializeField] private string mapObjectName = "mapa por distritos 1";
    [SerializeField] private DistrictColorMapping colorMapping;

    private bool mapSetupComplete;
    private bool warnedMissingCamera;
    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();

    private void Awake()
    {
        ResolveSelectionCamera();
        if (!autoSetupMapOnStart) return;
        TrySetupMapInScene();
    }

    private void Start()
    {
        ResolveSelectionCamera();
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

        if (hits == null || hits.Length == 0)
        {
            SetSelectedDistrict(null, null);
            return;
        }

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            DistrictZone zone = hits[i].collider.GetComponentInParent<DistrictZone>();
            if (zone == null) continue;

            string partColorName = ResolvePartColorName(zone);
            SetSelectedDistrict(zone.District, zone, zone.name, partColorName);

            if (verboseLogs)
            {
                Debug.Log(FormatSelectionLog(zone.District, partColorName, zone.name, colorMapping), this);
            }

            return;
        }

        SetSelectedDistrict(null, null, string.Empty, string.Empty);
        if (verboseLogs) Debug.Log("DistrictSelectionController: click sin DistrictZone.", this);
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
