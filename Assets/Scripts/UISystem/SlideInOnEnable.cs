using DG.Tweening;
using UnityEngine;

public class SlideInOnEnable : MonoBehaviour
{
    [SerializeField] private Vector2 from = new Vector2(-80f, 0f);
    [SerializeField, Min(0.01f)] private float duration = 0.35f;

    private Vector2 rest;

    private void Awake() => rest = ((RectTransform)transform).anchoredPosition;

    private void OnEnable()
    {
        var rect = (RectTransform)transform;
        rect.DOKill();
        rect.anchoredPosition = rest + from;
        rect.DOAnchorPos(rest, duration).SetEase(Ease.OutCubic).SetLink(gameObject);

        if (!TryGetComponent(out CanvasGroup group)) return;
        group.DOKill();
        group.alpha = 0f;
        group.DOFade(1f, duration).SetLink(gameObject);
    }
}
