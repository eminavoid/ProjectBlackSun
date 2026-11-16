using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Selectable))]
public class PressFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Tooltip("Graphics darkened on hover and press. Empty uses every graphic on the button, text included.")]
    [SerializeField] private Graphic[] graphics;

    [Header("Shade")]
    [SerializeField, Range(0f, 1f)] private float hoverShade = 0.78f;
    [SerializeField, Range(0f, 1f)] private float pressShade = 0.58f;
    [Tooltip("How much of the shade text takes, so it stays readable on a darkened button.")]
    [SerializeField, Range(0f, 1f)] private float textShadeStrength = 0.5f;
    [SerializeField, Range(0f, 1f)] private float disabledAlpha = 0.5f;
    [SerializeField, Min(0f)] private float fade = 0.08f;
    [SerializeField] private Color denyTint = new Color(1f, 0.55f, 0.5f, 1f);
    [Tooltip("Sideways shake when clicked while unavailable. 0 keeps it still.")]
    [SerializeField, Min(0f)] private float denyShake = 9f;

    [Header("Scale")]
    [SerializeField] private float hoverScale = 1.03f;
    [SerializeField] private float pressScale = 0.95f;
    [SerializeField] private float releasePunch = 0.08f;
    [Tooltip("Gentle breathing while the button waits to be used. 0 turns it off.")]
    [SerializeField, Range(0f, 0.1f)] private float idlePulse;

    public event Action Denied;

    private Selectable selectable;
    private RectTransform rect;
    private Vector3 restScale;
    private Vector2 restPosition;
    private Tween scaleTween;
    private Tween shakeTween;
    private bool hovered;
    private bool pressed;
    private bool shownInteractable;
    private bool pulsing;

    private void Awake()
    {
        selectable = GetComponent<Selectable>();
        rect = (RectTransform)transform;
        restScale = rect.localScale;
        if (graphics == null || graphics.Length == 0) graphics = GetComponentsInChildren<Graphic>(true);
        if (selectable.transition == Selectable.Transition.ColorTint)
        {
            selectable.transition = Selectable.Transition.None;
            if (selectable.targetGraphic != null) selectable.targetGraphic.CrossFadeColor(Color.white, 0f, true, true);
        }
    }

    private void OnEnable()
    {
        hovered = false;
        pressed = false;
        shownInteractable = selectable.IsInteractable();
        Apply(true);
    }

    private void OnDisable()
    {
        StopShake();
        scaleTween?.Kill();
        rect.localScale = restScale;
        pulsing = false;
    }

    private void LateUpdate()
    {
        bool interactable = selectable.IsInteractable();
        if (interactable == shownInteractable) return;
        shownInteractable = interactable;
        if (!interactable) pressed = false;
        Apply(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        Apply(false);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        pressed = false;
        Apply(false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (!selectable.IsInteractable())
        {
            Deny();
            return;
        }

        pressed = true;
        Apply(false);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || !pressed) return;
        pressed = false;
        Apply(false);
        if (!hovered || releasePunch <= 0f || !selectable.IsInteractable()) return;

        scaleTween?.Kill();
        pulsing = false;
        rect.localScale = restScale * hoverScale;
        scaleTween = rect.DOPunchScale(restScale * releasePunch, 0.28f, 7, 0.6f).SetUpdate(true).SetLink(gameObject)
            .OnComplete(() => Apply(false));
    }

    private void Deny()
    {
        StopShake();
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null) graphics[i].CrossFadeColor(denyTint, 0f, true, true);
        }

        restPosition = rect.anchoredPosition;
        Vector2 strength = new Vector2(denyShake, 0f);
        shakeTween = rect.DOShakeAnchorPos(0.35f, strength, 18, 0f).SetUpdate(true).SetLink(gameObject)
            .OnKill(() => rect.anchoredPosition = restPosition)
            .OnComplete(() => Apply(false));
        Denied?.Invoke();
    }

    private void StopShake()
    {
        if (shakeTween == null) return;
        Tween shake = shakeTween;
        shakeTween = null;
        shake.Kill();
    }

    private void Apply(bool instant)
    {
        bool interactable = selectable.IsInteractable();
        float shade = !interactable ? 1f : pressed ? pressShade : hovered ? hoverShade : 1f;
        float alpha = interactable ? 1f : disabledAlpha;
        float textShade = Mathf.Lerp(1f, shade, textShadeStrength);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null) continue;
            float value = graphic is TMPro.TMP_Text ? textShade : shade;
            graphic.CrossFadeColor(new Color(value, value, value, alpha), instant ? 0f : fade, true, true);
        }

        float scale = !interactable ? 1f : pressed ? pressScale : hovered ? hoverScale : 1f;
        bool wantsPulse = interactable && !hovered && !pressed && idlePulse > 0f;
        if (wantsPulse && pulsing) return;

        scaleTween?.Kill();
        pulsing = wantsPulse;
        if (instant)
        {
            rect.localScale = restScale * scale;
            if (wantsPulse) Breathe();
            return;
        }

        scaleTween = rect.DOScale(restScale * scale, fade * 1.5f).SetEase(Ease.OutQuad).SetUpdate(true).SetLink(gameObject);
        if (wantsPulse) scaleTween.OnComplete(Breathe);
    }

    private void Breathe()
    {
        scaleTween = rect.DOScale(restScale * (1f + idlePulse), 0.9f).SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(gameObject);
    }
}
