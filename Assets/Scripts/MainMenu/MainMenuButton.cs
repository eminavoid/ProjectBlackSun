using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

public class MainMenuButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private RectTransform slab;
    [SerializeField] private CanvasGroup marker;
    [SerializeField] private float nudge = 16f;
    [SerializeField] private string hoverEvent = "Play_UI_Click_Generic";

    private float restX;

    private void Awake()
    {
        restX = slab.anchoredPosition.x;
        marker.alpha = 0f;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Hover(true);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayUIEvent(hoverEvent);
    }

    public void OnPointerExit(PointerEventData eventData) => Hover(false);

    private void Hover(bool hovered)
    {
        slab.DOKill();
        marker.DOKill();
        marker.transform.DOKill();
        slab.DOAnchorPosX(restX + (hovered ? nudge : 0f), 0.2f).SetEase(Ease.OutCubic).SetLink(gameObject);
        marker.DOFade(hovered ? 1f : 0f, 0.15f).SetLink(gameObject);
        if (hovered) marker.transform.DOScale(1f, 0.25f).From(Vector3.one * 1.6f).SetEase(Ease.OutBack).SetLink(gameObject);
    }
}
