using UnityEngine;
using TMPro;

public class Tooltip : Singleton<Tooltip>
{
    [SerializeField] private GameObject tooltipObject;
    [SerializeField] private TMP_Text tooltipText;
    [SerializeField] private RectTransform tooltipRect;

    [Space]

    [SerializeField] private float offsetX = 0;
    [SerializeField] private float offsetY = -15f;

    private void Update()
    {
        if (!tooltipObject.activeSelf)
            return;

        Canvas canvas = tooltipRect.GetComponentInParent<Canvas>();

        float scale = canvas.scaleFactor;

        Vector2 offset = new Vector2(
            offsetX * scale,
            offsetY * scale
        );

        tooltipRect.position = (Vector2)Input.mousePosition + offset;

        Canvas.ForceUpdateCanvases();

        Vector3[] corners = new Vector3[4];
        tooltipRect.GetWorldCorners(corners);

        Vector3 position = tooltipRect.position;

        // Left
        if (corners[0].x < 0)
            position.x += -corners[0].x;

        // Right
        if (corners[2].x > Screen.width)
            position.x -= corners[2].x - Screen.width;

        // Bottom
        if (corners[0].y < 0)
            position.y += -corners[0].y;

        // Top
        if (corners[2].y > Screen.height)
            position.y -= corners[2].y - Screen.height;

        tooltipRect.position = position;
    }

    protected override void OnInitialization()
    {
        Hide();
    }

    public static void Show(string text)
    {
        Instance.tooltipText.text = text;
        Instance.tooltipObject.SetActive(true);
    }

    public static void Hide()
    {
        Instance.tooltipObject.SetActive(false);
    }
}