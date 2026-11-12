using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class PopUpAnimator : MonoBehaviour
{
    [Header("Stamp In")]
    [SerializeField, Min(0.01f)] private float stampDuration = 0.22f;
    [SerializeField, Min(1f)] private float stampStartScale = 1.9f;
    [SerializeField] private float stampStartAngle = -16f;
    [SerializeField] private float impactPunch = 0.22f;
    [SerializeField] private float impactShake = 3f;

    [Header("Bump")]
    [SerializeField] private float bumpPunch = 0.28f;
    [SerializeField] private float bumpTwist = 12f;
    [SerializeField, Min(0.01f)] private float bumpDuration = 0.4f;

    [Header("Break Out")]
    [SerializeField, Min(0.01f)] private float breakDuration = 0.3f;
    [SerializeField] private float breakSwell = 1.18f;
    [SerializeField] private float breakAngle = 28f;

    [Header("Ripple")]
    [SerializeField] private Graphic ripple;
    [SerializeField, Min(1f)] private float rippleScale = 1.8f;
    [SerializeField, Min(0.01f)] private float rippleDuration = 1.1f;
    [SerializeField, Min(0f)] private float rippleInterval = 1.5f;

    private CanvasGroup group;
    private RectTransform rect;
    private Vector3 restScale;
    private Vector3 rippleRestScale;
    private float rippleAlpha;
    private Sequence motion;
    private Sequence rippleLoop;
    private bool stamping;
    private bool hiding;

    private void Awake()
    {
        rect = (RectTransform)transform;
        group = GetComponent<CanvasGroup>();
        restScale = rect.localScale;
        if (ripple == null) return;
        rippleRestScale = ripple.rectTransform.localScale;
        rippleAlpha = ripple.color.a;
    }

    public void Show()
    {
        hiding = false;
        stamping = true;
        KillMotion();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        rect.localScale = restScale * stampStartScale;
        rect.localRotation = Quaternion.Euler(0f, 0f, stampStartAngle);
        SetRippleAlpha(0f);

        motion = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
            .Append(group.DOFade(1f, stampDuration * 0.6f))
            .Join(rect.DOScale(restScale, stampDuration).SetEase(Ease.InQuad))
            .Join(rect.DOLocalRotateQuaternion(Quaternion.identity, stampDuration).SetEase(Ease.OutQuad))
            .Append(rect.DOPunchScale(Vector3.one * impactPunch, 0.32f, 5, 0.5f))
            .Join(rect.DOShakeAnchorPos(0.25f, impactShake, 18, 90f, true, true))
            .OnComplete(() =>
            {
                stamping = false;
                group.blocksRaycasts = true;
                StartRipple();
            });
    }

    public void Bump()
    {
        if (stamping || hiding) return;

        motion?.Kill();
        rect.localScale = restScale;
        rect.localRotation = Quaternion.identity;
        motion = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
            .Append(rect.DOPunchScale(Vector3.one * bumpPunch, bumpDuration, 6, 0.6f))
            .Join(rect.DOPunchRotation(new Vector3(0f, 0f, bumpTwist), bumpDuration, 8, 0.6f));
    }

    public void Hide(Action onHidden)
    {
        if (hiding) return;

        hiding = true;
        stamping = false;
        KillMotion();
        group.blocksRaycasts = false;
        motion = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
            .Append(rect.DOScale(restScale * breakSwell, breakDuration * 0.35f).SetEase(Ease.OutQuad))
            .Append(rect.DOScale(Vector3.zero, breakDuration * 0.65f).SetEase(Ease.InBack))
            .Join(rect.DOLocalRotate(new Vector3(0f, 0f, breakAngle), breakDuration * 0.65f).SetEase(Ease.InQuad))
            .Join(group.DOFade(0f, breakDuration * 0.65f).SetEase(Ease.InQuad))
            .OnComplete(() => onHidden?.Invoke());
    }

    private void StartRipple()
    {
        if (ripple == null) return;

        RectTransform ring = ripple.rectTransform;
        ring.localScale = rippleRestScale;
        SetRippleAlpha(rippleAlpha);
        rippleLoop = DOTween.Sequence().SetUpdate(true).SetLink(ripple.gameObject)
            .Append(ring.DOScale(rippleRestScale * rippleScale, rippleDuration).SetEase(Ease.OutCubic))
            .Join(ripple.DOFade(0f, rippleDuration).SetEase(Ease.InQuad))
            .AppendInterval(rippleInterval)
            .SetLoops(-1, LoopType.Restart);
    }

    private void KillMotion()
    {
        motion?.Kill();
        rippleLoop?.Kill();
        if (ripple != null) ripple.rectTransform.localScale = rippleRestScale;
    }

    private void SetRippleAlpha(float alpha)
    {
        if (ripple == null) return;
        Color color = ripple.color;
        color.a = alpha;
        ripple.color = color;
    }
}
