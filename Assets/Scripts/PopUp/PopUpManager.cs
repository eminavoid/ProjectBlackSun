using UnityEngine;
using Zeke.UI;

public class PopUpManager : Singleton<PopUpManager>
{
    [SerializeField] private UIWindow popUpPrefab;
    [SerializeField] private Canvas canvas;

    public static UIWindow SpawnPopUp(RectTransform target, Direction direction, float distance)
    {
        if (Instance.popUpPrefab == null || target == null) return null;

        RectTransform canvasRect = Instance.canvas.GetComponent<RectTransform>();

        UIWindow popUpInstance = Instantiate(Instance.popUpPrefab, canvasRect);
        RectTransform popUpRect = popUpInstance.GetComponent<RectTransform>();

        Vector3[] targetCorners = new Vector3[4];
        target.GetWorldCorners(targetCorners);

        Vector3 targetCenter = (targetCorners[0] + targetCorners[2]) / 2f;

        float targetHalfWidth = Vector3.Distance(targetCorners[0], targetCorners[3]) / 2f;
        float targetHalfHeight = Vector3.Distance(targetCorners[0], targetCorners[1]) / 2f;

        Vector2 worldOffset = Vector2.zero;

        switch (direction)
        {
            case Direction.left:
                worldOffset = Vector3.left * (targetHalfWidth + distance + (popUpRect.rect.width * popUpRect.lossyScale.x / 2f));
                break;
            case Direction.right:
                worldOffset = Vector3.right * (targetHalfWidth + distance + (popUpRect.rect.width * popUpRect.lossyScale.x / 2f));
                break;
            case Direction.up:
                worldOffset = Vector3.up * (targetHalfHeight + distance + (popUpRect.rect.height * popUpRect.lossyScale.y / 2f));
                break;
            case Direction.down:
                worldOffset = Vector3.down * (targetHalfHeight + distance + (popUpRect.rect.height * popUpRect.lossyScale.y / 2f));
                break;
        }

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

        //Play the popUp animation here?

        return popUpInstance;
    }

    public static void RemovePopUp(UIWindow popUp)
    {
        //Play the popUp animation here and destroy it when it's finished with this:
        popUp.DestroyWindow();
    }

    public enum Direction
    {
        left,
        right,
        up,
        down,
    }
}
