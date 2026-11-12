using UnityEngine;
using Zeke.UI;

public class PopUpManager : Singleton<PopUpManager>
{
    [SerializeField] private UIWindow popUpPrefab;
    [SerializeField] private Canvas canvas;

    public static UIWindow SpawnPopUp(RectTransform target, Direction direction, float distance, bool attachToTarget = false)
    {
        if (Instance.popUpPrefab == null || target == null) return null;

        UIWindow popUpInstance = attachToTarget ? AttachTo(target, direction, distance) : PlaceBeside(target, direction, distance);

        if (popUpInstance.TryGetComponent(out PopUpAnimator animator)) animator.Show();

        return popUpInstance;
    }

    private static UIWindow AttachTo(RectTransform target, Direction direction, float distance)
    {
        UIWindow popUpInstance = Instantiate(Instance.popUpPrefab, target);
        RectTransform popUpRect = popUpInstance.GetComponent<RectTransform>();
        popUpRect.SetAsLastSibling();

        Vector2 side = Side(direction);
        popUpRect.anchorMin = popUpRect.anchorMax = (side + Vector2.one) / 2f;
        popUpRect.anchoredPosition = side * distance;

        return popUpInstance;
    }

    private static UIWindow PlaceBeside(RectTransform target, Direction direction, float distance)
    {
        RectTransform canvasRect = Instance.canvas.GetComponent<RectTransform>();

        UIWindow popUpInstance = Instantiate(Instance.popUpPrefab, canvasRect);
        RectTransform popUpRect = popUpInstance.GetComponent<RectTransform>();

        Vector3[] targetCorners = new Vector3[4];
        target.GetWorldCorners(targetCorners);

        Vector3 targetCenter = (targetCorners[0] + targetCorners[2]) / 2f;

        float targetHalfWidth = Vector3.Distance(targetCorners[0], targetCorners[3]) / 2f;
        float targetHalfHeight = Vector3.Distance(targetCorners[0], targetCorners[1]) / 2f;

        Vector2 side = Side(direction);
        Vector2 worldOffset = new Vector2(
            side.x * (targetHalfWidth + distance + (popUpRect.rect.width * popUpRect.lossyScale.x / 2f)),
            side.y * (targetHalfHeight + distance + (popUpRect.rect.height * popUpRect.lossyScale.y / 2f)));

        Vector3 finalWorldPos = targetCenter + (Vector3)worldOffset;

        RectTransform parentRect = popUpRect.parent as RectTransform;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect,
            RectTransformUtility.WorldToScreenPoint(null, finalWorldPos),
            null,
            out Vector2 localPoint))
        {
            popUpRect.anchoredPosition = localPoint;
        }

        return popUpInstance;
    }

    private static Vector2 Side(Direction direction)
    {
        switch (direction)
        {
            case Direction.left: return Vector2.left;
            case Direction.right: return Vector2.right;
            case Direction.up: return Vector2.up;
            case Direction.down: return Vector2.down;
            case Direction.upperLeft: return new Vector2(-1f, 1f);
            case Direction.upperRight: return new Vector2(1f, 1f);
            case Direction.lowerLeft: return new Vector2(-1f, -1f);
            case Direction.lowerRight: return new Vector2(1f, -1f);
            default: return Vector2.zero;
        }
    }

    public static void BumpPopUp(UIWindow popUp)
    {
        if (popUp != null && popUp.TryGetComponent(out PopUpAnimator animator)) animator.Bump();
    }

    public static void RemovePopUp(UIWindow popUp)
    {
        if (popUp == null) return;

        if (popUp.TryGetComponent(out PopUpAnimator animator)) animator.Hide(popUp.DestroyWindow);
        else popUp.DestroyWindow();
    }

    public enum Direction
    {
        left,
        right,
        up,
        down,
        upperLeft,
        upperRight,
        lowerLeft,
        lowerRight,
    }
}
