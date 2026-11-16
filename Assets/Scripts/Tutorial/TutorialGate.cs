using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class TutorialGate : MonoBehaviour, ICanvasRaycastFilter
{
    private const int SortingOrder = 450;
    private const int CanvasRefreshFrames = 30;
    private const string OptionsMenuName = "Options Menu";

    private readonly HashSet<Transform> allowed = new HashSet<Transform>();
    private readonly List<Graphic> graphics = new List<Graphic>();
    private readonly HashSet<Graphic> locked = new HashSet<Graphic>();
    private readonly List<Canvas> canvases = new List<Canvas>();
    private readonly Dictionary<Transform, bool> verdicts = new Dictionary<Transform, bool>();
    private Canvas ownCanvas;
    private int canvasAge = CanvasRefreshFrames;

    public static TutorialGate Create(Transform screenCanvas)
    {
        GameObject root = new GameObject("Tutorial Gate", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(Image));
        root.transform.SetParent(screenCanvas, false);
        RectTransform rect = (RectTransform)root.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = SortingOrder;

        Image image = root.GetComponent<Image>();
        image.color = Color.clear;
        image.raycastTarget = true;

        TutorialGate gate = root.AddComponent<TutorialGate>();
        gate.ownCanvas = canvas;
        return gate;
    }

    public void Allow(params Component[] roots)
    {
        allowed.Clear();
        foreach (Component root in roots)
        {
            if (root != null) allowed.Add(root.transform);
        }
    }

    private void LateUpdate()
    {
        if (++canvasAge >= CanvasRefreshFrames)
        {
            canvasAge = 0;
            canvases.Clear();
            canvases.AddRange(FindObjectsByType<Canvas>());
        }

        graphics.Clear();
        locked.Clear();
        verdicts.Clear();
        foreach (Canvas canvas in canvases)
        {
            if (canvas == null || canvas == ownCanvas || !canvas.isActiveAndEnabled) continue;
            if (canvas.sortingOrder > SortingOrder) continue;

            IList<Graphic> registered = GraphicRegistry.GetRaycastableGraphicsForCanvas(canvas);
            for (int i = 0; i < registered.Count; i++)
            {
                Graphic graphic = registered[i];
                if (graphic == null || !graphic.isActiveAndEnabled || graphic.canvasRenderer.cull) continue;
                graphics.Add(graphic);
                if (!IsAllowed(graphic.transform)) locked.Add(graphic);
            }
        }
    }

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        Graphic top = null;
        for (int i = 0; i < graphics.Count; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null || !graphic.isActiveAndEnabled) continue;
            if (top != null && !Above(graphic, top)) continue;

            Canvas canvas = graphic.canvas;
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (!RectTransformUtility.RectangleContainsScreenPoint(graphic.rectTransform, screenPoint, camera)) continue;
            if (graphic.Raycast(screenPoint, camera)) top = graphic;
        }

        return top != null && locked.Contains(top);
    }

    private static bool Above(Graphic candidate, Graphic current)
    {
        Canvas a = candidate.canvas;
        Canvas b = current.canvas;
        if (a == null || b == null) return false;
        if (a.sortingOrder != b.sortingOrder) return a.sortingOrder > b.sortingOrder;

        Canvas rootA = a.rootCanvas;
        Canvas rootB = b.rootCanvas;
        if (rootA != rootB) return rootA.renderOrder > rootB.renderOrder;
        return candidate.depth > current.depth;
    }

    private bool IsAllowed(Transform target)
    {
        if (target == null) return false;
        if (verdicts.TryGetValue(target, out bool known)) return known;

        bool result = allowed.Contains(target)
            || target.name == OptionsMenuName
            || target.GetComponent<TopScreenNotification>() != null
            || target.GetComponent<TutorialHighlight>() != null
            || target.GetComponent<Zeke.Tooltips.Tooltip>() != null
            || IsAllowed(target.parent);
        verdicts[target] = result;
        return result;
    }
}
