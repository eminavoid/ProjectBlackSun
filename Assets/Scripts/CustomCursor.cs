using UnityEngine;
using UnityEngine.InputSystem;
using Zeke.Tooltips;

public class CustomCursor : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("The RectTransform of the cursor UI element. If left blank, it uses the attached RectTransform.")]
    [SerializeField] private RectTransform cursorRectTransform;

    [Tooltip("Canvas containing the cursor. Required if Canvas is set to Screen Space - Camera.")]
    [SerializeField] private Canvas canvas;

    private Camera uiCamera;

    void Awake()
    {
        Tooltip.onTooltipShow += () => gameObject.SetActive(false);
        Tooltip.onTooltipHide += () => SyncAndEnable();

        Cursor.visible = false;

        if (cursorRectTransform == null)
        {
            cursorRectTransform = GetComponent<RectTransform>();
        }

        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
        }

        if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera)
        {
            uiCamera = canvas.worldCamera;
        }

        // Optional: Hide the default OS hardware cursor if you want to rely entirely on this UI cursor
        // Cursor.visible = false;
    }

    private void SyncAndEnable()
    {
        UpdateCursorPosition();
        gameObject.SetActive(true);
    }

    void Update()
    {
        UpdateCursorPosition();
    }

    private void UpdateCursorPosition()
    {
        // Check if the New Input System Mouse is active
        if (Mouse.current == null) return;

        // Get current mouse position from the New Input System
        Vector2 mousePosition = Mouse.current.position.ReadValue();

        if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            // Direct screen space mapping for Overlay canvas
            cursorRectTransform.position = mousePosition;
        }
        else
        {
            // Screen space camera or world space canvas mapping
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform,
                mousePosition,
                uiCamera,
                out Vector2 localPoint
            );
            cursorRectTransform.anchoredPosition = localPoint;
        }
    }
}