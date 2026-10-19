using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MapStatsTab : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Districts district;
    [SerializeField] private Button button;
    [SerializeField] private Image image;
    [SerializeField] private float hoverLift = 5f;
    [SerializeField] private float selectedLift = 12f;
    [SerializeField] private float hiddenDrop = 46f;
    [SerializeField] private Color idleTint = new Color(0.82f, 0.8f, 0.78f);
    [SerializeField] private string hoverEvent = "Play_UI_Click_Generic";

    private RectTransform rect;
    private float restY;
    private bool hovered;
    private bool selected;

    public Districts District => district;
    public Button Button => button;
    public Sprite Sprite => image.sprite;

    private float TargetY => restY + (selected ? selectedLift : hovered ? hoverLift : 0f);

    private void Awake()
    {
        rect = (RectTransform)transform;
        restY = rect.anchoredPosition.y;
        image.color = idleTint;
    }

    public void SetSelected(bool value)
    {
        if (selected == value) return;
        selected = value;
        hovered = false;
        Apply();
    }

    public void Rise(float delay)
    {
        rect.DOKill();
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, restY - hiddenDrop);
        rect.DOAnchorPosY(TargetY, 0.32f).SetDelay(delay).SetEase(Ease.OutBack).SetLink(gameObject);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (selected) return;
        hovered = true;
        Apply();
        if (AudioManager.Instance != null) AudioManager.Instance.PlayUIEvent(hoverEvent);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!hovered) return;
        hovered = false;
        Apply();
    }

    private void Apply()
    {
        image.color = selected || hovered ? Color.white : idleTint;
        rect.DOKill();
        rect.DOAnchorPosY(TargetY, 0.16f).SetEase(Ease.OutQuad).SetLink(gameObject);
    }
}
