using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Menú contextual del mapa: zoom cercano = nodo, zoom lejano = distrito.
/// </summary>
public class MapContextMenu : MonoBehaviour
{
    [SerializeField] private GameObject menuPrefab;
    [SerializeField] private Vector2 menuOffset = new Vector2(12f, -12f);

    private MapContextMenuView view;
    private DistrictSelectionController selection;
    private bool isOpen;
    private int openedFrame = -1;
    private DistrictZone pendingZone;
    private bool pendingNodeLevel;

    private void Start()
    {
        selection = FindAnyObjectByType<DistrictSelectionController>();
        EnsureView();
    }

    private void OnEnable()
    {
        MapCameraController.OnMapContextRequested += OnMapContextRequested;
    }

    private void OnDisable()
    {
        MapCameraController.OnMapContextRequested -= OnMapContextRequested;
    }

    private void Update()
    {
        if (!isOpen) return;

        if (Time.frameCount <= openedFrame + 1) return;

        if (WasEscapePressed() || WasPrimaryPressedOutside())
        {
            Close();
        }
    }

    public void Close()
    {
        isOpen = false;
        pendingZone = null;
        if (view != null && view.Root != null) view.Root.SetActive(false);
    }

    private void OnMapContextRequested(Vector2 screenPosition)
    {
        if (selection == null) selection = FindAnyObjectByType<DistrictSelectionController>();
        if (selection == null) return;

        if (!selection.TryPickPlayableZone(screenPosition, out DistrictZone zone) || zone == null)
        {
            Close();
            return;
        }

        pendingZone = zone;
        pendingNodeLevel = IsNodeZoom();
        EnsureView();
        if (view == null || view.Root == null) return;

        string districtName = MapStatsQuery.DistrictLabel(zone.District);
        if (view.Title != null)
        {
            view.Title.text = pendingNodeLevel ? zone.SectorName : districtName;
        }

        if (view.ActionLabel != null)
        {
            view.ActionLabel.text = pendingNodeLevel
                ? "Estadísticas de " + zone.SectorName
                : "Estadísticas de " + districtName;
        }

        PlaceAtScreen(screenPosition);
        view.Root.SetActive(true);
        view.Root.transform.SetAsLastSibling();
        isOpen = true;
        openedFrame = Time.frameCount;
    }

    private void OnActionClicked()
    {
        DistrictZone zone = pendingZone;
        bool nodeLevel = pendingNodeLevel;
        Close();

        if (zone == null) return;

        MapStatsPanel panel = MapStatsPanel.Instance;
        if (panel == null)
        {
            panel = FindAnyObjectByType<MapStatsPanel>();
        }

        if (panel == null) return;

        if (nodeLevel) panel.OpenNode(zone);
        else panel.OpenDistrict(zone.District);
    }

    private static bool IsNodeZoom()
    {
        InfluenceOverlaySettings settings = InfluenceOverlaySettings.LoadOrCreate();
        return MapViewLod.IsClose(settings);
    }

    private void PlaceAtScreen(Vector2 screenPosition)
    {
        if (view == null || view.RootRect == null) return;

        Canvas canvas = view.RootRect.GetComponentInParent<Canvas>();
        RectTransform canvasRect = canvas != null ? canvas.transform as RectTransform : view.RootRect.parent as RectTransform;
        if (canvasRect == null) return;

        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, screenPosition, eventCamera, out Vector2 local))
        {
            return;
        }

        Vector2 position = local + menuOffset;
        Vector2 size = view.RootRect.sizeDelta;
        Rect canvasLocal = canvasRect.rect;
        position.x = Mathf.Clamp(position.x, canvasLocal.xMin + 8f, canvasLocal.xMax - size.x - 8f);
        position.y = Mathf.Clamp(position.y, canvasLocal.yMin + size.y + 8f, canvasLocal.yMax - 8f);
        view.RootRect.anchoredPosition = position;
    }

    private void EnsureView()
    {
        if (view != null && view.Root != null)
        {
            EnsureActionWired();
            return;
        }

        Transform canvas = ResolveCanvas();
        if (canvas == null)
        {
            Debug.LogWarning("MapContextMenu: no hay ScreenCanvas.", this);
            return;
        }

        if (menuPrefab != null)
        {
            GameObject instance = Instantiate(menuPrefab, canvas);
            instance.name = "MapContextMenu";
            view = MapStatsUiBuilder.BindContextMenu(instance);
        }
        else
        {
            view = MapStatsUiBuilder.BuildContextMenu(canvas);
        }

        EnsureActionWired();
        if (view != null && view.Root != null) view.Root.SetActive(false);
    }

    private void EnsureActionWired()
    {
        if (view == null || view.ActionButton == null) return;
        view.ActionButton.onClick.RemoveListener(OnActionClicked);
        view.ActionButton.onClick.AddListener(OnActionClicked);
    }

    private static Transform ResolveCanvas()
    {
        if (!GlobalReferences.IsNull && GlobalReferences.ScreenCanvas != null)
        {
            return GlobalReferences.ScreenCanvas.transform;
        }

        Canvas canvas = FindAnyObjectByType<Canvas>();
        return canvas != null ? canvas.transform : null;
    }

    private bool WasPrimaryPressedOutside()
    {
        if (!WasPrimaryPressedThisFrame(out Vector2 screenPosition)) return false;
        if (view == null || view.RootRect == null) return true;
        if (!view.Root.activeInHierarchy) return true;

        Canvas canvas = view.RootRect.GetComponentInParent<Canvas>();
        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        return !RectTransformUtility.RectangleContainsScreenPoint(view.RootRect, screenPosition, eventCamera);
    }

    private static bool WasPrimaryPressedThisFrame(out Vector2 screenPosition)
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            screenPosition = Mouse.current.position.ReadValue();
            return true;
        }

        screenPosition = default;
        return false;
#else
        if (!Input.GetMouseButtonDown(0))
        {
            screenPosition = default;
            return false;
        }

        screenPosition = Input.mousePosition;
        return true;
#endif
    }

    private static bool WasEscapePressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }
}
