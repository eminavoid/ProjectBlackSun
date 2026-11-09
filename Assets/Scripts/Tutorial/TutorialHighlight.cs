using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Borde animado sobre el control que toca, y Skip. Ninguno bloquea el resto de la UI.
/// </summary>
public class TutorialHighlight : MonoBehaviour
{
    public event Action SkipClicked;

    private RectTransform frame;
    private CanvasGroup frameGroup;
    private Material markMaterial;
    private Sprite markSprite;
    private RectTransform skipRoot;
    private RectTransform uiTarget;
    private DistrictZone zoneTarget;
    private Canvas canvas;

    public static TutorialHighlight Create(Transform parent)
    {
        GameObject root = new GameObject("Tutorial Highlight", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)root.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TutorialHighlight highlight = root.AddComponent<TutorialHighlight>();
        highlight.canvas = parent.GetComponentInParent<Canvas>();
        highlight.Build();
        return highlight;
    }

    public void Track(RectTransform target)
    {
        uiTarget = target;
        zoneTarget = null;
    }

    public void Track(Button button)
    {
        if (button == null)
        {
            Clear();
            return;
        }

        RectTransform rect = (RectTransform)button.transform;
        if (button.targetGraphic != null)
        {
            RectTransform graphic = button.targetGraphic.rectTransform;
            float buttonArea = Mathf.Abs(rect.rect.width * rect.rect.height);
            float graphicArea = Mathf.Abs(graphic.rect.width * graphic.rect.height);
            if (graphicArea > buttonArea && graphicArea > 64f) rect = graphic;
        }

        Track(rect);
    }

    public void TrackZone(DistrictZone zone)
    {
        uiTarget = null;
        zoneTarget = zone;
    }

    public void Clear()
    {
        uiTarget = null;
        zoneTarget = null;
        if (frame != null) frame.gameObject.SetActive(false);
    }

    private void Build()
    {
        GameObject frameObject = new GameObject("Frame", typeof(RectTransform), typeof(CanvasGroup));
        frameObject.transform.SetParent(transform, false);
        frame = (RectTransform)frameObject.transform;
        frame.anchorMin = new Vector2(0.5f, 0.5f);
        frame.anchorMax = new Vector2(0.5f, 0.5f);
        frame.pivot = new Vector2(0.5f, 0.5f);
        frameGroup = frameObject.GetComponent<CanvasGroup>();
        frameGroup.blocksRaycasts = false;
        frameGroup.interactable = false;
        frameGroup.alpha = 1f;

        Shader shader = Shader.Find("Custom/TutorialMark");
        if (shader != null)
        {
            markMaterial = new Material(shader) { name = "TutorialMark" };
            Image plate = frameObject.AddComponent<Image>();
            Texture2D white = Texture2D.whiteTexture;
            markSprite = Sprite.Create(white, new Rect(0f, 0f, white.width, white.height), new Vector2(0.5f, 0.5f), 100f);
            plate.sprite = markSprite;
            plate.material = markMaterial;
            plate.color = new Color(1f, 0.84f, 0.38f, 1f);
            plate.raycastTarget = false;
        }
        else
        {
            Color ink = new Color(1f, 0.86f, 0.42f, 1f);
            AddEdge("Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 8f), ink);
            AddEdge("Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), ink);
            AddEdge("Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), ink);
            AddEdge("Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(8f, 0f), ink);
            frameGroup.DOFade(0.72f, 0.55f).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(frameObject);
        }

        frameObject.SetActive(false);

        GameObject skip = new GameObject("Skip", typeof(RectTransform), typeof(Image), typeof(Button));
        skip.transform.SetParent(transform, false);
        skipRoot = (RectTransform)skip.transform;
        skipRoot.anchorMin = new Vector2(1f, 0f);
        skipRoot.anchorMax = new Vector2(1f, 0f);
        skipRoot.pivot = new Vector2(1f, 0f);
        skipRoot.anchoredPosition = new Vector2(-24f, 24f);
        skipRoot.sizeDelta = new Vector2(150f, 48f);

        Image image = skip.GetComponent<Image>();
        image.color = new Color(0.09f, 0.06f, 0.04f, 0.94f);
        Button button = skip.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => SkipClicked?.Invoke());

        GameObject labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.transform.SetParent(skip.transform, false);
        RectTransform labelRect = (RectTransform)labelObject.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        TextMeshProUGUI sample = canvas != null ? canvas.GetComponentInChildren<TextMeshProUGUI>(true) : null;
        if (sample != null)
        {
            label.font = sample.font;
            label.fontSharedMaterial = sample.fontSharedMaterial;
        }
        label.text = "SKIP";
        label.fontSize = 22f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.94f, 0.86f, 0.66f, 1f);
        label.raycastTarget = false;
    }

    private void AddEdge(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Color color)
    {
        GameObject edge = new GameObject(name, typeof(RectTransform), typeof(Image));
        edge.transform.SetParent(frame, false);
        RectTransform rect = (RectTransform)edge.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;
        Image image = edge.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private void OnDestroy()
    {
        if (frameGroup != null) frameGroup.DOKill();
        if (markMaterial != null) Destroy(markMaterial);
        if (markSprite != null) Destroy(markSprite);
    }

    private void LateUpdate()
    {
        if (uiTarget != null && uiTarget.gameObject.activeInHierarchy)
        {
            transform.SetAsLastSibling();
            PlaceOnRect(uiTarget);
        }
        else if (zoneTarget != null)
        {
            PlaceOnZone(zoneTarget);
        }
        else if (frame != null)
        {
            frame.gameObject.SetActive(false);
        }

        if (skipRoot != null) skipRoot.SetAsLastSibling();
    }

    private void PlaceOnRect(RectTransform target)
    {
        Vector3[] corners = new Vector3[4];
        target.GetWorldCorners(corners);
        frame.gameObject.SetActive(true);
        frame.position = (corners[0] + corners[2]) * 0.5f;
        Vector3 size = corners[2] - corners[0];
        float sx = Mathf.Abs(frame.lossyScale.x) < 0.001f ? 1f : Mathf.Abs(frame.lossyScale.x);
        float sy = Mathf.Abs(frame.lossyScale.y) < 0.001f ? 1f : Mathf.Abs(frame.lossyScale.y);
        frame.sizeDelta = new Vector2(Mathf.Abs(size.x) / sx + 36f, Mathf.Abs(size.y) / sy + 36f);
        PushSize();
    }

    private void PlaceOnZone(DistrictZone zone)
    {
        Camera cam = Camera.main;
        if (cam == null || frame.parent is not RectTransform parent)
        {
            frame.gameObject.SetActive(false);
            return;
        }

        Bounds bounds = BoundsOf(zone);
        Vector3 extents = bounds.extents;
        Vector3 center = bounds.center;
        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;
        bool any = false;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 world = center + Vector3.Scale(extents, new Vector3(x, y, z));
                    Vector3 screen = cam.WorldToScreenPoint(world);
                    if (screen.z < 0f) continue;
                    any = true;
                    minX = Mathf.Min(minX, screen.x);
                    minY = Mathf.Min(minY, screen.y);
                    maxX = Mathf.Max(maxX, screen.x);
                    maxY = Mathf.Max(maxY, screen.y);
                }
            }
        }

        if (!any)
        {
            frame.gameObject.SetActive(false);
            return;
        }

        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, new Vector2(minX, minY), uiCamera, out Vector2 localMin);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, new Vector2(maxX, maxY), uiCamera, out Vector2 localMax);
        frame.gameObject.SetActive(true);
        frame.anchoredPosition = (localMin + localMax) * 0.5f - parent.rect.center;
        frame.sizeDelta = new Vector2(Mathf.Abs(localMax.x - localMin.x) + 36f, Mathf.Abs(localMax.y - localMin.y) + 36f);
        PushSize();
    }

    private void PushSize()
    {
        if (markMaterial == null || frame == null) return;
        markMaterial.SetVector("_Size", new Vector4(frame.sizeDelta.x, frame.sizeDelta.y, 0f, 0f));
    }

    private static Bounds BoundsOf(DistrictZone zone)
    {
        Renderer renderer = zone.GetComponent<Renderer>();
        if (renderer == null) renderer = zone.GetComponentInChildren<Renderer>();
        if (renderer != null) return renderer.bounds;
        return new Bounds(zone.transform.position, Vector3.one * 2f);
    }
}
