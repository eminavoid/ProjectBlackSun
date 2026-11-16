using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button), typeof(Image))]
public class EndTurnButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Idle")]
    [Tooltip("Seconds between the arrow's nudges while the turn can end.")]
    [SerializeField, Min(0.5f)] private float nudgeEvery = 4f;
    [SerializeField] private float nudgeDistance = 18f;
    [SerializeField] private Color glowColor = new Color(1f, 0.78f, 0.36f, 1f);
    [SerializeField, Range(0f, 1f)] private float glowLow = 0.12f;
    [SerializeField, Range(0f, 1f)] private float glowHigh = 0.45f;
    [SerializeField, Min(1f)] private float glowScale = 1.12f;

    [Header("Hover")]
    [SerializeField] private float hoverShift = 12f;

    [Header("Turn")]
    [SerializeField] private float dashDistance = 70f;
    [Tooltip("Arrow tint while the turn resolves or can't be ended.")]
    [SerializeField] private Color restingTint = new Color(0.55f, 0.5f, 0.47f, 1f);

    private RectTransform arrow;
    private Image arrowImage;
    private Image glow;
    private Tween glowPulse;
    private float nudgeTimer;
    private bool hovered;
    private bool resolving;
    private bool resting;

    private void Awake()
    {
        Image hitArea = GetComponent<Image>();
        hitArea.alphaHitTestMinimumThreshold = 0.1f;

        glow = CreateLayer("Glow", hitArea);
        glow.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
        glow.rectTransform.localScale = Vector3.one * glowScale;

        arrowImage = CreateLayer("Arrow", hitArea);
        arrow = arrowImage.rectTransform;
        hitArea.color = Color.clear;

        Button button = GetComponent<Button>();
        button.targetGraphic = arrowImage;
    }

    private void OnEnable()
    {
        GameTime.OnTurnResolving += OnTurnResolving;
        GameTime.OnTurnResolved += OnTurnResolved;
        resting = !CanEnd();
        ApplyRest(true);
    }

    private void OnDisable()
    {
        GameTime.OnTurnResolving -= OnTurnResolving;
        GameTime.OnTurnResolved -= OnTurnResolved;
        glowPulse?.Kill();
        arrow.DOKill();
        arrowImage.DOKill();
        glow.DOKill();
    }

    private void Update()
    {
        bool shouldRest = !CanEnd();
        if (shouldRest != resting)
        {
            resting = shouldRest;
            ApplyRest(false);
        }

        if (resting || hovered) return;
        nudgeTimer += Time.unscaledDeltaTime;
        if (nudgeTimer < nudgeEvery) return;
        nudgeTimer = 0f;
        arrow.DOComplete();
        arrow.DOPunchAnchorPos(new Vector2(nudgeDistance, 0f), 0.6f, 3, 0.4f).SetUpdate(true).SetLink(gameObject);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        if (resting) return;
        arrow.DOKill(true);
        arrow.DOAnchorPosX(hoverShift, 0.15f).SetEase(Ease.OutQuad).SetUpdate(true).SetLink(gameObject);
        glowPulse?.Kill();
        glow.DOFade(glowHigh, 0.15f).SetUpdate(true).SetLink(gameObject);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        nudgeTimer = 0f;
        if (resting) return;
        arrow.DOKill(true);
        arrow.DOAnchorPosX(0f, 0.2f).SetEase(Ease.OutQuad).SetUpdate(true).SetLink(gameObject);
        StartGlowPulse();
    }

    private bool CanEnd() => !resolving && !InputLocks.EndTurn;

    private void OnTurnResolving()
    {
        resolving = true;
        resting = true;
        glowPulse?.Kill();
        arrow.DOKill();
        arrowImage.DOKill();
        glow.DOKill();
        glow.color = new Color(glowColor.r, glowColor.g, glowColor.b, glowHigh);

        DOTween.Sequence()
            .Append(arrow.DOAnchorPosX(dashDistance, 0.14f).SetEase(Ease.InQuad))
            .Join(arrowImage.DOFade(0f, 0.14f))
            .Join(glow.DOFade(0f, 0.25f))
            .AppendCallback(() =>
            {
                arrow.anchoredPosition = new Vector2(-dashDistance, 0f);
                arrowImage.color = new Color(restingTint.r, restingTint.g, restingTint.b, 0f);
            })
            .Append(arrow.DOAnchorPosX(0f, 0.35f).SetEase(Ease.OutBack))
            .Join(arrowImage.DOFade(1f, 0.25f))
            .SetUpdate(true).SetLink(gameObject);
    }

    private void OnTurnResolved()
    {
        resolving = false;
        resting = !CanEnd();
        ApplyRest(false);
        if (resting) return;
        arrow.DOComplete();
        arrow.DOPunchScale(Vector3.one * 0.18f, 0.45f, 6, 0.6f).SetUpdate(true).SetLink(gameObject);
        glow.DOKill();
        glow.color = new Color(glowColor.r, glowColor.g, glowColor.b, glowHigh);
        StartGlowPulse();
    }

    private void ApplyRest(bool instant)
    {
        glowPulse?.Kill();
        arrowImage.DOKill();
        Color target = resting ? restingTint : Color.white;
        if (instant) arrowImage.color = target;
        else arrowImage.DOColor(target, 0.25f).SetUpdate(true).SetLink(gameObject);

        if (resting)
        {
            glow.DOKill();
            glow.DOFade(0f, instant ? 0f : 0.2f).SetUpdate(true).SetLink(gameObject);
            return;
        }

        nudgeTimer = 0f;
        StartGlowPulse();
    }

    private void StartGlowPulse()
    {
        glowPulse?.Kill();
        glow.DOKill();
        glowPulse = DOTween.Sequence()
            .Append(glow.DOFade(glowHigh, 0.9f).SetEase(Ease.InOutSine))
            .Append(glow.DOFade(glowLow, 0.9f).SetEase(Ease.InOutSine))
            .SetLoops(-1)
            .SetUpdate(true).SetLink(gameObject);
    }

    private Image CreateLayer(string layerName, Image source)
    {
        GameObject go = new GameObject(layerName, typeof(RectTransform), typeof(Image));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = go.GetComponent<Image>();
        image.sprite = source.sprite;
        image.preserveAspect = source.preserveAspect;
        image.raycastTarget = false;
        return image;
    }
}
