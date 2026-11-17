using UnityEngine;
using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine.UI;

public class OptionDisplay : MonoBehaviour
{
    [SerializeField] private Button button;
    [Tooltip("Icons for what the option spends or gives no matter the outcome.")]
    [SerializeField] private ResourceAmountRow resourceRow;
    [Tooltip("Cost text from the data. Only shown when the row has nothing to show (e.g. \"No cost\").")]
    [SerializeField] private GameObject costText;

    [Header("Success Chance")]
    [SerializeField] private TMP_Text title;
    [SerializeField] private Sprite chanceSeal;
    [SerializeField] private Sprite chanceRing;
    [SerializeField] private float chanceSize = 86f;
    [SerializeField] private float chanceInset = 34f;
    [Tooltip("Room kept on the left of the title while the chance seal shows.")]
    [SerializeField] private float titleIndent = 62f;
    [SerializeField] private Color lowChance = new Color(0.93f, 0.33f, 0.26f, 1f);
    [SerializeField] private Color midChance = new Color(0.98f, 0.76f, 0.3f, 1f);
    [SerializeField] private Color highChance = new Color(0.55f, 0.88f, 0.4f, 1f);
    [SerializeField] private Color boostRing = new Color(1f, 0.85f, 0.35f, 1f);
    [Tooltip("Seal opacity on an option the player can't pick.")]
    [SerializeField, Range(0f, 1f)] private float unavailableAlpha = 0.5f;

    public Action<OptionDisplay> onOptionChosen;

    private static readonly List<PlayerStats.PlayerStat> virtues = new List<PlayerStats.PlayerStat>();

    private Option option;
    private Seed seed;
    private readonly List<ResourceDelta> fixedChanges = new List<ResourceDelta>();
    private RectTransform chanceRoot;
    private Image ring;
    private Image seal;
    private TextMeshProUGUI percent;
    private Sequence reveal;
    private Tween boostLoop;
    private Vector4 titleMargin;
    private bool titleMarginStored;

    public Option Option => option;
    public Seed Seed => seed;

    public void InitializeData(Option optionReference, Seed seed, int order = 0)
    {
        this.seed = seed;
        option = optionReference;
        button.interactable = option.CanExecute();
        ShowFixedChanges();
        ShowChance(order);
    }

    public void ExecuteOptions()
    {
        if (option.CanExecute())
        {
            onOptionChosen?.Invoke(this);
        }
    }

    public void GetFixedChangeIcons(List<(RectTransform icon, ResourceDelta delta)> icons)
    {
        if (resourceRow != null) resourceRow.GetChipIcons(icons);
    }

    public string ChanceTip()
    {
        if (option == null) return null;
        OutcomeChances chances = option.GetOutcomeChances();
        if (!chances.IsUncertain) return null;

        int now = Mathf.RoundToInt(chances.GoodPercentage);
        int start = Mathf.RoundToInt(option.GetBaseOutcomeChances().GoodPercentage);
        string tip = $"Success chance: <color=#{ColorUtility.ToHtmlStringRGB(ChanceColor(chances.good))}><b>{now}%</b></color>\nBase chance: {start}%";

        virtues.Clear();
        option.GetTestedVirtues(virtues);
        PlayerStats stats = option.PlayerStats;
        virtues.RemoveAll(virtue => stats == null || stats.GetStat(virtue) == 0);
        if (virtues.Count > 0 && now != start) tip += $"\nYour {Virtues.Names(virtues)}: <b>{(Mathf.Sign(now - start) == 1 ? "+" : " - ")}{now - start}%</b>";
        else if (virtues.Count == 0)
        {
            option.GetTestedVirtues(virtues);
            if (virtues.Count > 0) tip += $"\n<size=80%>{Virtues.Names(virtues)} would raise it.</size>";
        }

        ResourceRewards possibleRewards = option.GetPosibleResourceRewards();

        string possibleRewardsText = "";

        for (int i = 0; i < possibleRewards.resources.Count; i++)
        {
            possibleRewardsText += possibleRewards.resources[i].ToString();
            if (i != possibleRewards.resources.Count - 1) possibleRewardsText += ", ";
        }

        return tip + $"\n<size=80%>Possible Rewards: {possibleRewardsText}.</size>";
    }

    private void OnDisable()
    {
        reveal?.Kill();
        boostLoop?.Kill();
    }

    private void ShowFixedChanges()
    {
        if (resourceRow == null) return;

        fixedChanges.Clear();
        option.GetFixedResourceChanges(fixedChanges);

        resourceRow.Show(fixedChanges, !button.interactable);
        bool showCost = fixedChanges.Count == 0;
        if (costText == null) return;
        costText.SetActive(showCost);
        TMP_Text costLabel = costText.GetComponent<TMP_Text>();
        if (showCost && costLabel != null) costLabel.text = CostLabel(option.Description);
    }

    private static string CostLabel(string description)
    {
        string text = string.IsNullOrWhiteSpace(description) ? "No cost" : description.Trim();
        if (text.StartsWith("COST:", StringComparison.OrdinalIgnoreCase)) text = text.Substring(5).Trim();
        return text.ToUpperInvariant();
    }

    private void ShowChance(int order)
    {
        reveal?.Kill();
        boostLoop?.Kill();

        OutcomeChances chances = option.GetOutcomeChances();
        bool show = chances.IsUncertain && chanceSeal != null;
        if (title != null)
        {
            if (!titleMarginStored)
            {
                titleMargin = title.margin;
                titleMarginStored = true;
            }

            title.margin = new Vector4(titleMargin.x + (show ? titleIndent : 0f), titleMargin.y, titleMargin.z, titleMargin.w);
        }
        if (!show)
        {
            if (chanceRoot != null) chanceRoot.gameObject.SetActive(false);
            return;
        }

        if (chanceRoot == null) BuildChance();
        chanceRoot.gameObject.SetActive(true);

        int target = Mathf.RoundToInt(chances.GoodPercentage);
        Color color = ChanceColor(chances.good);
        bool available = button.interactable;
        bool boosted = available && target > Mathf.RoundToInt(option.GetBaseOutcomeChances().GoodPercentage);
        float alpha = available ? 1f : unavailableAlpha;
        seal.color = new Color(1f, 1f, 1f, alpha);
        color.a = alpha;
        percent.color = color;
        percent.text = "0%";
        chanceRoot.localScale = Vector3.zero;
        ring.color = Clear(color);

        reveal = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
            .AppendInterval(0.3f + order * 0.12f)
            .Append(chanceRoot.DOScale(1f, 0.4f).SetEase(Ease.OutBack))
            .Join(DOVirtual.Int(0, target, 0.7f, value => percent.text = value + "%").SetEase(Ease.OutCubic))
            .AppendCallback(() =>
            {
                if (available) Ripple(color);
                chanceRoot.DOPunchScale(Vector3.one * 0.12f, 0.3f, 6, 0.6f).SetUpdate(true).SetLink(gameObject);
                if (boosted) StartBoost();
            });
    }

    private void StartBoost()
    {
        boostLoop = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
            .AppendInterval(2.4f)
            .AppendCallback(() => Ripple(boostRing))
            .SetLoops(-1);
    }

    private void Ripple(Color color)
    {
        ring.DOKill();
        ring.rectTransform.DOKill();
        ring.rectTransform.localScale = Vector3.one;
        ring.color = new Color(color.r, color.g, color.b, 0.95f);
        ring.rectTransform.DOScale(1.75f, 0.8f).SetEase(Ease.OutCubic).SetUpdate(true).SetLink(gameObject);
        ring.DOFade(0f, 0.8f).SetEase(Ease.OutQuad).SetUpdate(true).SetLink(gameObject);
    }

    private void BuildChance()
    {
        chanceRoot = new GameObject("Chance", typeof(RectTransform)).GetComponent<RectTransform>();
        chanceRoot.SetParent(transform, false);
        chanceRoot.anchorMin = new Vector2(0f, 0.5f);
        chanceRoot.anchorMax = new Vector2(0f, 0.5f);
        chanceRoot.pivot = new Vector2(0.5f, 0.5f);
        chanceRoot.anchoredPosition = new Vector2(chanceInset, 0f);
        chanceRoot.sizeDelta = new Vector2(chanceSize, chanceSize);

        ring = Layer("Ring", chanceRing != null ? chanceRing : chanceSeal);
        seal = Layer("Seal", chanceSeal);

        GameObject textObject = new GameObject("Percent", typeof(RectTransform));
        textObject.transform.SetParent(chanceRoot, false);
        RectTransform textRect = (RectTransform)textObject.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        percent = textObject.AddComponent<TextMeshProUGUI>();
        if (title != null)
        {
            percent.font = title.font;
            percent.fontSharedMaterial = title.fontSharedMaterial;
        }

        percent.fontSize = chanceSize * 0.3f;
        percent.fontStyle = FontStyles.Bold;
        percent.alignment = TextAlignmentOptions.Center;
        percent.textWrappingMode = TextWrappingModes.NoWrap;
        percent.raycastTarget = false;
    }

    private Image Layer(string layerName, Sprite sprite)
    {
        GameObject go = new GameObject(layerName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(chanceRoot, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private Color ChanceColor(float good)
    {
        return good < 0.5f
            ? Color.Lerp(lowChance, midChance, good / 0.5f)
            : Color.Lerp(midChance, highChance, (good - 0.5f) / 0.5f);
    }

    private static Color Clear(Color color) => new Color(color.r, color.g, color.b, 0f);
}
