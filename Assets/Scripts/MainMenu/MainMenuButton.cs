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
    private bool chosen;
    private bool pointerInside;

    private void Awake()
    {
        restX = slab.anchoredPosition.x;
        marker.alpha = 0f;
    }

    private void OnEnable()
    {
        if (slab == null || marker == null) return;
        marker.alpha = chosen ? 1f : 0f;
        slab.anchoredPosition = new Vector2(restX, slab.anchoredPosition.y);
    }

    private void OnDisable()
    {
        pointerInside = false;
    }

    public void SetChosen(bool value)
    {
        if (chosen == value) return;
        chosen = value;
        HoverVisual(pointerInside);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
        HoverVisual(true);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayUIEvent(hoverEvent);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        HoverVisual(false);
    }

    private void HoverVisual(bool inside)
    {
        bool showMarker = inside || chosen;
        if (!isActiveAndEnabled)
        {
            marker.alpha = showMarker ? 1f : 0f;
            slab.anchoredPosition = new Vector2(restX, slab.anchoredPosition.y);
            return;
        }

        slab.DOKill();
        marker.DOKill();
        marker.transform.DOKill();
        slab.DOAnchorPosX(restX + (inside ? nudge : 0f), 0.2f).SetEase(Ease.OutCubic).SetLink(gameObject, LinkBehaviour.KillOnDisable);
        marker.DOFade(showMarker ? 1f : 0f, 0.15f).SetLink(gameObject, LinkBehaviour.KillOnDisable);
        if (inside) marker.transform.DOScale(1f, 0.25f).From(Vector3.one * 1.6f).SetEase(Ease.OutBack).SetLink(gameObject, LinkBehaviour.KillOnDisable);
    }
}
