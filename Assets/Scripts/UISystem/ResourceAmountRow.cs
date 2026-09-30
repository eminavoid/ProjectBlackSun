using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Row of resource icons with a signed amount each (-25 Wealth, +50 Zeal...).
/// Chips are copies of a disabled template child, so their look is edited in the prefab.
/// </summary>
public class ResourceAmountRow : MonoBehaviour
{
    [SerializeField] private ResourceIcons icons;
    [Tooltip("Disabled child with an Image (icon) and a TMP text (amount).")]
    [SerializeField] private GameObject chipTemplate;

    [Header("Colors")]
    [SerializeField] private Color gainColor = new Color(0.18f, 0.42f, 0.14f);
    [SerializeField] private Color lossColor = new Color(0.55f, 0.11f, 0.07f);
    [SerializeField, Range(0f, 1f)] private float dimmedFactor = 0.5f;

    [Header("Refunds")]
    [Tooltip("Resources given back from the option's cost: shown last, greyed out, so they don't read as gains.")]
    [SerializeField] private Color refundColor = new Color(0.36f, 0.34f, 0.31f);
    [SerializeField] private Color refundIconColor = new Color(0.6f, 0.6f, 0.6f, 0.8f);
    [SerializeField] private string refundLabel = "refund";

    [Header("Appear Animation")]
    [SerializeField] private bool animateIn;
    [SerializeField, Min(0f)] private float appearDelay = 0.2f;
    [SerializeField, Min(0f)] private float stagger = 0.12f;
    [SerializeField, Min(0.01f)] private float popDuration = 0.35f;

    private class Chip
    {
        public GameObject root;
        public Image icon;
        public TMP_Text amount;
        public int value;
        public bool refund;
    }

    private readonly List<Chip> chips = new List<Chip>();

    public void Show(IReadOnlyList<ResourceDelta> deltas, bool dimmed = false)
    {
        StopAllCoroutines();
        ClearChips();

        if (deltas != null)
        {
            AddChips(deltas, false, dimmed);
            AddChips(deltas, true, dimmed);
        }

        bool hasAny = chips.Count > 0;
        gameObject.SetActive(hasAny);
        if (hasAny && animateIn) StartCoroutine(AnimateIn());
    }

    private void AddChips(IReadOnlyList<ResourceDelta> deltas, bool refunds, bool dimmed)
    {
        for (int i = 0; i < deltas.Count; i++)
        {
            if (deltas[i].Amount == 0 || deltas[i].IsRefund != refunds) continue;
            chips.Add(CreateChip(deltas[i], dimmed));
        }
    }

    private Chip CreateChip(ResourceDelta delta, bool dimmed)
    {
        GameObject root = Instantiate(chipTemplate, chipTemplate.transform.parent);
        root.name = delta.IsRefund ? $"{delta.Resource} (refund)" : delta.Resource.ToString();
        root.SetActive(true);

        Chip chip = new Chip
        {
            root = root,
            icon = root.GetComponentInChildren<Image>(true),
            amount = root.GetComponentInChildren<TMP_Text>(true),
            value = delta.Amount,
            refund = delta.IsRefund
        };

        Color color = delta.IsRefund ? refundColor : delta.Amount > 0 ? gainColor : lossColor;
        Color iconColor = delta.IsRefund ? refundIconColor : Color.white;
        if (dimmed)
        {
            color = Dim(color);
            iconColor = Dim(iconColor);
        }

        if (chip.icon != null)
        {
            chip.icon.sprite = icons != null ? icons.Get(delta.Resource) : null;
            chip.icon.color = iconColor;
            chip.icon.enabled = chip.icon.sprite != null;
        }

        if (chip.amount != null)
        {
            chip.amount.color = color;
            chip.amount.text = FormatAmount(chip, delta.Amount);
        }

        return chip;
    }

    private Color Dim(Color color)
    {
        return new Color(color.r * dimmedFactor, color.g * dimmedFactor, color.b * dimmedFactor, color.a);
    }

    // Each chip pops in after the previous one while its number counts up from zero.
    private IEnumerator AnimateIn()
    {
        SetProgress(0f);
        float start = Time.unscaledTime + appearDelay;

        bool finished = false;
        while (!finished)
        {
            finished = true;
            float elapsed = Time.unscaledTime - start;

            for (int i = 0; i < chips.Count; i++)
            {
                float t = Mathf.Clamp01((elapsed - i * stagger) / popDuration);
                if (t < 1f) finished = false;
                ApplyProgress(chips[i], t);
            }

            yield return null;
        }
    }

    private void SetProgress(float t)
    {
        for (int i = 0; i < chips.Count; i++)
        {
            ApplyProgress(chips[i], t);
        }
    }

    private void ApplyProgress(Chip chip, float t)
    {
        chip.root.transform.localScale = Vector3.one * BackOut(t);
        if (chip.amount != null)
        {
            float counted = 1f - (1f - t) * (1f - t) * (1f - t);
            chip.amount.text = FormatAmount(chip, Mathf.RoundToInt(chip.value * counted));
        }
    }

    // Overshoots a little past 1 before settling, which reads as a "pop".
    private static float BackOut(float t)
    {
        if (t <= 0f) return 0f;
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    private string FormatAmount(Chip chip, int amount)
    {
        string number = amount > 0 ? $"+{amount}" : amount.ToString();
        return chip.refund && !string.IsNullOrEmpty(refundLabel) ? $"{number} <size=55%>{refundLabel}</size>" : number;
    }

    private void ClearChips()
    {
        for (int i = 0; i < chips.Count; i++)
        {
            if (chips[i].root != null) Destroy(chips[i].root);
        }

        chips.Clear();
    }
}
