using System.Threading;
using TMPro;
using UnityEngine;

public class Tooltip : Singleton<Tooltip>
{
    [SerializeField] private GameObject tooltipObject;
    [SerializeField] private TMP_Text tooltipText;
    [SerializeField] private RectTransform tooltipRect;

    [Space]

    [SerializeField] private float offsetX = 0;
    [SerializeField] private float offsetY = -15f;

    [SerializeField] private float maxWidth = 400f;
    [SerializeField] private float maxHeight = 300f;
    [SerializeField] private Vector2 padding = new Vector2(20f, 10f);

    [Space]

    [SerializeField] private float showDelay = 1f;

    private State state = State.Hide;
    private float timer = 0f;

    private Canvas canvas;

    private enum State
    {
        Show,
        Hide,
    }

    public static void SetText(string text)
    {
        Instance.tooltipText.text = text;

        // Force TMP to update its measurements.
        Instance.tooltipText.ForceMeshUpdate();

        Vector2 textSize = Instance.tooltipText.GetPreferredValues(text, Instance.maxWidth, Instance.maxHeight);

        // Add padding for the tooltip background/box.
        Vector2 finalSize = textSize + Instance.padding;

        // Cap the size.
        finalSize.x = Mathf.Min(finalSize.x, Instance.maxWidth);
        finalSize.y = Mathf.Min(finalSize.y, Instance.maxHeight);

        Instance.tooltipRect.sizeDelta = finalSize;
    }

    private void Update()
    {
        UpdateState();

        if (!tooltipObject.activeSelf)
            return;

        float scale = canvas.scaleFactor;

        UpdatePosition(scale);
    }

    private void UpdatePosition(float scale)
    {
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

    private void UpdateState()
    {
        if (state == State.Show && !tooltipObject.activeSelf)
        {
            timer += Time.deltaTime;

            if (timer > showDelay)
            {
                Instance.tooltipObject.SetActive(true);
            }
        }
    }

    protected override void OnInitialization()
    {
        canvas = tooltipRect.GetComponentInParent<Canvas>();

        Hide();
    }

    public static void Show(string text)
    {
        SetText(text);
        Instance.state = State.Show;
    }

    public static void Hide()
    {
        Instance.timer = 0f;
        Instance.state = State.Hide;
        Instance.tooltipObject.SetActive(false);
    }
}