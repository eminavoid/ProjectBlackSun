using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Cámara top-down del mapa: pan (WASD / arrastre), zoom con rueda y focus al doble clic.
/// Arranca en la pose actual; todo movimiento usa SmoothDamp (acelera/decelera).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class MapCameraController : MonoBehaviour
{
    [Header("Smoothing")]
    [SerializeField] private float moveSmoothTime = 0.16f;
    [SerializeField] private float zoomSmoothTime = 0.28f;

    [Header("Pan")]
    [SerializeField] private float keyboardPanSpeed = 8f;
    [SerializeField] private bool enableKeyboardPan = true;
    [SerializeField] private bool enableMousePan = true;
    [SerializeField] private bool panWithMiddleMouse = true;
    [SerializeField] private bool panWithRightMouse = true;

    [Header("Zoom")]
    [SerializeField] private float zoomStep = 0.45f;
    [SerializeField] private float minHeight = 2f;
    [SerializeField] private float maxHeight = 18f;
    [SerializeField] private bool blockZoomOverUi = true;

    [Header("Focus")]
    [SerializeField] private float focusPadding = 1.35f;
    [SerializeField] private bool adjustHeightOnFocus = true;

    private Camera cam;
    private Vector3 targetPosition;
    private Vector3 moveVelocity;
    private float zoomVelocity;
    private bool isMousePanning;
    private Vector2 lastPanPointerPosition;
    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();

    public static MapCameraController Instance { get; private set; }

    private void Awake()
    {
        cam = GetComponent<Camera>();
        targetPosition = transform.position;
        Instance = this;
    }

    private void OnEnable()
    {
        DistrictSelectionController.OnZoneDoubleClicked += FocusOnZone;
    }

    private void OnDisable()
    {
        DistrictSelectionController.OnZoneDoubleClicked -= FocusOnZone;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        HandleKeyboardPan();
        HandleMousePan();
        HandleZoomInput();
    }

    private void LateUpdate()
    {
        Vector3 current = transform.position;

        float newX = Mathf.SmoothDamp(current.x, targetPosition.x, ref moveVelocity.x, moveSmoothTime);
        float newZ = Mathf.SmoothDamp(current.z, targetPosition.z, ref moveVelocity.z, moveSmoothTime);
        float newY = Mathf.SmoothDamp(current.y, targetPosition.y, ref zoomVelocity, zoomSmoothTime);

        transform.position = new Vector3(newX, newY, newZ);
    }

    public void FocusOnZone(DistrictZone zone)
    {
        if (zone == null) return;

        Bounds bounds = ResolveZoneBounds(zone);
        Vector3 center = bounds.center;

        float height = targetPosition.y;
        if (adjustHeightOnFocus && cam != null)
        {
            float footprint = Mathf.Max(bounds.size.x, bounds.size.z, 0.5f) * focusPadding;
            float halfFovRad = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
            height = (footprint * 0.5f) / Mathf.Max(0.01f, Mathf.Tan(halfFovRad));
            height = Mathf.Clamp(height, minHeight, maxHeight);
        }

        targetPosition = new Vector3(center.x, height, center.z);
    }

    public static MapCameraController EnsureOnCamera(Camera camera)
    {
        if (camera == null) return null;

        MapCameraController existing = camera.GetComponent<MapCameraController>();
        if (existing != null) return existing;

        return camera.gameObject.AddComponent<MapCameraController>();
    }

    private void HandleKeyboardPan()
    {
        if (!enableKeyboardPan) return;
        if (IsTextInputFocused()) return;

        Vector2 input = ReadMoveAxes();
        if (input.sqrMagnitude < 0.0001f) return;

        input = Vector2.ClampMagnitude(input, 1f);

        Vector3 right;
        Vector3 forward;
        GetPanAxes(out right, out forward);

        // Move a bit faster when zoomed out.
        float speed = keyboardPanSpeed * Mathf.Lerp(0.65f, 1.6f, Mathf.InverseLerp(minHeight, maxHeight, targetPosition.y));
        Vector3 delta = (right * input.x + forward * input.y) * speed * Time.unscaledDeltaTime;
        targetPosition += new Vector3(delta.x, 0f, delta.z);
    }

    private void HandleMousePan()
    {
        if (!enableMousePan) return;

        Vector2 pointer = ReadPointerPosition();
        bool panHeld = IsPanButtonHeld();
        bool panPressedThisFrame = IsPanButtonPressedThisFrame();

        if (panPressedThisFrame)
        {
            if (IsPointerOverUi(pointer))
            {
                isMousePanning = false;
            }
            else
            {
                isMousePanning = true;
                lastPanPointerPosition = pointer;
            }
        }

        if (!panHeld)
        {
            isMousePanning = false;
            return;
        }

        if (!isMousePanning) return;

        if (TryGetGroundPoint(lastPanPointerPosition, out Vector3 previousGround)
            && TryGetGroundPoint(pointer, out Vector3 currentGround))
        {
            Vector3 delta = previousGround - currentGround;
            targetPosition += new Vector3(delta.x, 0f, delta.z);
        }

        lastPanPointerPosition = pointer;
    }

    private void HandleZoomInput()
    {
        float scroll = ReadScrollDelta();
        if (Mathf.Abs(scroll) < 0.0001f) return;

        if (ShouldBlockZoom(ReadPointerPosition())) return;

        float nextHeight = targetPosition.y - scroll * zoomStep;
        targetPosition.y = Mathf.Clamp(nextHeight, minHeight, maxHeight);
    }

    private void GetPanAxes(out Vector3 right, out Vector3 forward)
    {
        right = transform.right;
        right.y = 0f;
        if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
        else right.Normalize();

        // Top-down camera: transform.up points along the map plane "north".
        forward = transform.up;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        else forward.Normalize();
    }

    private bool TryGetGroundPoint(Vector2 screenPosition, out Vector3 worldPoint)
    {
        worldPoint = default;
        if (cam == null) return false;

        Ray ray = cam.ScreenPointToRay(screenPosition);
        Plane ground = new Plane(Vector3.up, new Vector3(0f, 0f, 0f));
        if (!ground.Raycast(ray, out float enter)) return false;

        worldPoint = ray.GetPoint(enter);
        return true;
    }

    private bool IsPanButtonHeld()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current == null) return false;
        bool middle = panWithMiddleMouse && Mouse.current.middleButton.isPressed;
        bool right = panWithRightMouse && Mouse.current.rightButton.isPressed;
        return middle || right;
#else
        bool middle = panWithMiddleMouse && Input.GetMouseButton(2);
        bool right = panWithRightMouse && Input.GetMouseButton(1);
        return middle || right;
#endif
    }

    private bool IsPanButtonPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current == null) return false;
        bool middle = panWithMiddleMouse && Mouse.current.middleButton.wasPressedThisFrame;
        bool right = panWithRightMouse && Mouse.current.rightButton.wasPressedThisFrame;
        return middle || right;
#else
        bool middle = panWithMiddleMouse && Input.GetMouseButtonDown(2);
        bool right = panWithRightMouse && Input.GetMouseButtonDown(1);
        return middle || right;
#endif
    }

    private static Vector2 ReadMoveAxes()
    {
        float x = 0f;
        float y = 0f;

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) y -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) y += 1f;
            return new Vector2(x, y);
        }
#endif
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) y -= 1f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) y += 1f;
        return new Vector2(x, y);
    }

    private static float ReadScrollDelta()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            Vector2 raw = Mouse.current.scroll.ReadValue();
            float value = raw.y;

            // Some devices report horizontal wheel on X when Y is unused.
            if (Mathf.Abs(value) < 0.0001f) value = raw.x;

            // Windows notch ticks are often ~±120; trackpads / some drivers already give ~±1.
            if (Mathf.Abs(value) >= 10f) value /= 120f;

            return value;
        }
#endif
        return Input.mouseScrollDelta.y;
    }

    private bool ShouldBlockZoom(Vector2 screenPosition)
    {
        if (!blockZoomOverUi) return false;
        if (EventSystem.current == null) return false;

        PointerEventData pointerData = new PointerEventData(EventSystem.current) { position = screenPosition };
        uiRaycastResults.Clear();
        EventSystem.current.RaycastAll(pointerData, uiRaycastResults);

        for (int i = 0; i < uiRaycastResults.Count; i++)
        {
            GameObject hitObject = uiRaycastResults[i].gameObject;
            if (hitObject == null) continue;

            // Only ignore zoom over actual scrollable lists, not the whole HUD.
            if (hitObject.GetComponentInParent<ScrollRect>() != null) return true;
        }

        return false;
    }

    private static Vector2 ReadPointerPosition()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null) return Mouse.current.position.ReadValue();
#endif
        return Input.mousePosition;
    }

    private static bool IsTextInputFocused()
    {
        if (EventSystem.current == null) return false;
        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null) return false;
        return selected.GetComponent<TMP_InputField>() != null
            || selected.GetComponent<InputField>() != null;
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
}
