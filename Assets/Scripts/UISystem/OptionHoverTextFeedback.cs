using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class OptionHoverTextFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private TextMeshProUGUI[] texts;
    [SerializeField] private Selectable selectable;
    [SerializeField] private Color hoverColor = new Color(1f, 0.85f, 0.4f);
    [SerializeField, Range(0f, 1f)] private float disabledDimFactor = 0.5f;

    private Color[] originalColors;
    private bool locked;

    /// <summary>Keeps the hover color for good (e.g. the option was chosen), whatever the pointer does next.</summary>
    public void LockHighlighted()
    {
        locked = true;
        SetColor(hoverColor);
    }

    private void Start()
    {
        originalColors = new Color[texts.Length];
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null) originalColors[i] = texts[i].color;
        }

        ApplyBaseAppearance();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!IsInteractable()) return;
        SetColor(hoverColor);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!IsInteractable()) return;
        RestoreOriginalColors();
    }

    private bool IsInteractable()
    {
        return !locked && (selectable == null || selectable.interactable);
    }

    private void ApplyBaseAppearance()
    {
        if (IsInteractable())
        {
            RestoreOriginalColors();
        }
        else
        {
            SetDimmedColors();
        }
    }

    private void SetColor(Color color)
    {
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null) texts[i].color = color;
        }
    }

    private void SetDimmedColors()
    {
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] == null) continue;
            Color c = originalColors[i];
            texts[i].color = new Color(c.r * disabledDimFactor, c.g * disabledDimFactor, c.b * disabledDimFactor, c.a);
        }
    }

    private void RestoreOriginalColors()
    {
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null) texts[i].color = originalColors[i];
        }
    }
}
