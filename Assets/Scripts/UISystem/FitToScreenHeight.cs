using UnityEngine;

public class FitToScreenHeight : MonoBehaviour
{
    private void OnEnable()
    {
        var rect = (RectTransform)transform;
        rect.localScale = Vector3.one * (((RectTransform)rect.parent).rect.height / (rect.rect.height * (1f - rect.pivot.y)));
    }
}
