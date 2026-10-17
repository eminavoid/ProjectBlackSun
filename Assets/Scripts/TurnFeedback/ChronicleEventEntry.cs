using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ChronicleEventEntry : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Button button;
    [SerializeField] private Image background;
    [SerializeField] private Image marker;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text choice;
    [SerializeField] private TMP_Text toggleSign;
    [SerializeField] private ResourceAmountRow changes;
    [SerializeField] private GameObject details;
    [SerializeField] private TMP_Text outcome;
    [SerializeField] private string noOutcomeText = "Nothing worth recording came of it.";

    [Header("Colors")]
    [SerializeField] private Color idleColor = new Color(0.36f, 0.25f, 0.12f, 0f);
    [SerializeField] private Color hoverColor = new Color(0.36f, 0.25f, 0.12f, 0.1f);
    [SerializeField] private Color openColor = new Color(0.36f, 0.25f, 0.12f, 0.16f);

    private bool hovered;

    public bool IsExpanded { get; private set; }

    public event Action<ChronicleEventEntry> Clicked;

    private void Awake()
    {
        button.onClick.AddListener(() => Clicked?.Invoke(this));
    }

    public void Bind(SettledEvent settled, Sprite districtMarker)
    {
        title.text = settled.Seed != null ? settled.Seed.Title : string.Empty;
        choice.text = settled.Option != null ? $"Your advisors chose: {settled.Option.Title}" : string.Empty;
        marker.sprite = districtMarker;
        marker.enabled = districtMarker != null;
        changes.Show(settled.Changes);
        outcome.text = string.IsNullOrWhiteSpace(settled.Outcome) ? noOutcomeText : settled.Outcome;
        SetExpanded(false);
    }

    public void SetExpanded(bool expanded)
    {
        IsExpanded = expanded;
        details.SetActive(expanded);
        toggleSign.text = expanded ? "-" : "+";
        choice.textWrappingMode = expanded ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        choice.overflowMode = expanded ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
        Refresh();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        Refresh();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        Refresh();
    }

    private void Refresh()
    {
        background.color = IsExpanded ? openColor : hovered ? hoverColor : idleColor;
    }
}
