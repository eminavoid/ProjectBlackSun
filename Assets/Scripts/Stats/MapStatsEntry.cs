using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class MapStatsEntry : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image background;
    [SerializeField] private OptionHoverTextFeedback hover;
    [SerializeField] private GameObject marker;
    [SerializeField] private Image icon;
    [SerializeField] private Outline outline;
    [SerializeField] private float iconScale = 0.29296875f;
    [SerializeField] private Vector2 swatchSize = new Vector2(16f, 16f);
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text detail;

    public void Bind(Sprite sprite, Color swatch, string titleText, string detailText, UnityAction onClick)
    {
        marker.SetActive(sprite != null || swatch.a > 0f);
        icon.sprite = sprite;
        icon.color = sprite != null ? Color.white : swatch;
        icon.rectTransform.sizeDelta = sprite != null ? sprite.rect.size * iconScale : swatchSize;
        outline.enabled = sprite == null;

        title.text = titleText;
        detail.text = detailText;
        detail.gameObject.SetActive(!string.IsNullOrEmpty(detailText));

        bool clickable = onClick != null;
        button.enabled = clickable;
        background.enabled = clickable;
        hover.enabled = clickable;
        if (clickable) button.onClick.AddListener(onClick);
    }
}
