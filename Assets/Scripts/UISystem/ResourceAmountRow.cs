using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
    [SerializeField, Min(0f)] private float appearDelay = 0.25f;
    [SerializeField, Min(0f)] private float stagger = 0.15f;
    [SerializeField, Min(0.01f)] private float popDuration = 0.42f;

    private class Chip
    {
        public GameObject root;
        public Image icon;
        public TMP_Text amount;
        public ResourceDelta delta;
    }

    public event Action<IReadOnlyList<ResourceDelta>> Shown;

    public event Action<IReadOnlyList<ResourceDelta>> ChipsAppeared;

    private readonly List<Chip> chips = new List<Chip>();
    private Sequence appearing;

    public void Show(IReadOnlyList<ResourceDelta> deltas, bool dimmed = false)
    {
        appearing?.Kill();
        ClearChips();

        if (deltas != null)
        {
            AddChips(deltas, false, dimmed);
            AddChips(deltas, true, dimmed);
        }

        bool hasAny = chips.Count > 0;
        gameObject.SetActive(hasAny);
        if (!hasAny) return;

        Shown?.Invoke(deltas);
        if (animateIn) AnimateIn(deltas);
        else ChipsAppeared?.Invoke(deltas);
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
            delta = delta
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

    private void AnimateIn(IReadOnlyList<ResourceDelta> deltas)
    {
        appearing = DOTween.Sequence()
            .InsertCallback(appearDelay, () => ChipsAppeared?.Invoke(deltas))
            .SetLink(gameObject).SetUpdate(true);

        for (int i = 0; i < chips.Count; i++)
        {
            Chip chip = chips[i];
            float at = appearDelay + i * stagger;

            chip.root.transform.localScale = Vector3.zero;
            appearing.Insert(at, chip.root.transform.DOScale(1f, popDuration).SetEase(Ease.OutBack));

            if (chip.amount == null) continue;
            chip.amount.text = FormatAmount(chip, 0);
            appearing.Insert(at, DOVirtual.Float(0f, chip.delta.Amount, popDuration,
                value => chip.amount.text = FormatAmount(chip, Mathf.RoundToInt(value))).SetEase(Ease.OutCubic));
        }
    }

    private string FormatAmount(Chip chip, int amount)
    {
        string number = amount > 0 ? $"+{amount}" : amount.ToString();
        return chip.delta.IsRefund && !string.IsNullOrEmpty(refundLabel) ? $"{number} <size=55%>{refundLabel}</size>" : number;
    }

    public void GetChipIcons(List<(RectTransform icon, ResourceDelta delta)> icons)
    {
        for (int i = 0; i < chips.Count; i++)
        {
            if (chips[i].icon != null) icons.Add((chips[i].icon.rectTransform, chips[i].delta));
        }
    }

    public List<(RectTransform rect, ResourceDelta delta)> TakeChips()
    {
        appearing?.Complete();

        var taken = new List<(RectTransform rect, ResourceDelta delta)>(chips.Count);
        for (int i = 0; i < chips.Count; i++)
        {
            taken.Add(((RectTransform)chips[i].root.transform, chips[i].delta));
        }

        chips.Clear();
        return taken;
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
