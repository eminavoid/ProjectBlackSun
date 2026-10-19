using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

public class HoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private float hoverScale = 1.08f;
    [SerializeField] private float pressScale = 0.94f;
    [SerializeField, Min(0.01f)] private float duration = 0.12f;

    private Vector3 rest;
    private bool hovered;

    private void Awake() => rest = transform.localScale;

    private void OnDisable()
    {
        transform.DOKill();
        transform.localScale = rest;
        hovered = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        ScaleTo(hoverScale);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        ScaleTo(1f);
    }

    public void OnPointerDown(PointerEventData eventData) => ScaleTo(pressScale);

    public void OnPointerUp(PointerEventData eventData) => ScaleTo(hovered ? hoverScale : 1f);

    private void ScaleTo(float factor)
    {
        transform.DOKill();
        transform.DOScale(rest * factor, duration).SetEase(Ease.OutQuad).SetUpdate(true).SetLink(gameObject);
    }
}
