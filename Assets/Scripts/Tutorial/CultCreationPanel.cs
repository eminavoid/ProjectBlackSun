using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Nombre del culto y un atributo inicial. El mapa sigue usable detrás del panel.
/// </summary>
public class CultCreationPanel : MonoBehaviour
{
    private struct AttributeChoice
    {
        public string Label;
        public PlayerStats.PlayerStat Stat;
        public string Blurb;
    }

    private static readonly AttributeChoice[] Choices =
    {
        new AttributeChoice { Label = "DIPLOMACY", Stat = PlayerStats.PlayerStat.Diplomacy, Blurb = "Green streets yield more Flock." },
        new AttributeChoice { Label = "AGGRESSION", Stat = PlayerStats.PlayerStat.Aggresion, Blurb = "Red streets yield more." },
        new AttributeChoice { Label = "STEWARDSHIP", Stat = PlayerStats.PlayerStat.Stewardship, Blurb = "Yellow streets yield more Wealth." },
        new AttributeChoice { Label = "INTRIGUE", Stat = PlayerStats.PlayerStat.Intrigue, Blurb = "Violet and pink streets yield more Secrets." },
        new AttributeChoice { Label = "LEARNING", Stat = PlayerStats.PlayerStat.Learning, Blurb = "White streets yield more Zeal." }
    };

    private TMP_InputField input;
    private TMP_Text blurb;
    private Button confirm;
    private readonly Image[] choiceImages = new Image[Choices.Length];
    private int selected = -1;

    public RectTransform Card { get; private set; }
    public bool Confirmed { get; private set; }

    public static CultCreationPanel Create(Transform canvas)
    {
        ResetOpeningStats();

        TextMeshProUGUI sample = canvas.GetComponentInChildren<TextMeshProUGUI>(true);
        TMP_FontAsset font = sample != null ? sample.font : TMP_Settings.defaultFontAsset;
        Material material = sample != null ? sample.fontSharedMaterial : null;

        GameObject root = new GameObject("Cult Creation", typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(canvas, false);
        Stretch((RectTransform)root.transform);
        CanvasGroup group = root.GetComponent<CanvasGroup>();
        // True lets the card receive clicks. The root has no graphic, so the map still gets the rest.
        group.blocksRaycasts = true;
        group.interactable = true;

        CultCreationPanel panel = root.AddComponent<CultCreationPanel>();
        panel.Build(font, material);
        root.transform.SetAsLastSibling();
        return panel;
    }

    public static void ResetOpeningStats()
    {
        Apply(PlayerStats.PlayerStat.None);
    }

    public void Dismiss()
    {
        if (this != null) Destroy(gameObject);
    }

    private void Build(TMP_FontAsset font, Material material)
    {
        GameObject cardObject = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        cardObject.transform.SetParent(transform, false);
        Card = (RectTransform)cardObject.transform;
        Card.anchorMin = new Vector2(0f, 0.5f);
        Card.anchorMax = new Vector2(0f, 0.5f);
        Card.pivot = new Vector2(0f, 0.5f);
        Card.anchoredPosition = new Vector2(28f, -10f);
        Card.sizeDelta = new Vector2(440f, 0f);

        Image background = cardObject.GetComponent<Image>();
        background.color = new Color(0.09f, 0.06f, 0.04f, 0.94f);
        Outline outline = cardObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.88f, 0.71f, 0.38f, 0.7f);
        outline.effectDistance = new Vector2(2f, -2f);

        VerticalLayoutGroup layout = cardObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(22, 22, 20, 20);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = cardObject.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        AddLabel(Card, font, material, "NAME YOUR CULT", 26f, 36f);
        input = AddInput(Card, font, material);
        input.onValueChanged.AddListener(_ => RefreshConfirm());
        AddLabel(Card, font, material, "STARTING ATTRIBUTE", 16f, 24f);

        for (int i = 0; i < Choices.Length; i++)
        {
            int index = i;
            AddChoice(Card, font, material, Choices[i].Label, () => Select(index), out choiceImages[i]);
        }

        blurb = AddLabel(Card, font, material, "Choose the attribute your cult starts with.", 18f, 64f);
        confirm = AddChoice(Card, font, material, "CONFIRM", Confirm, out _);
        RefreshConfirm();
    }

    private void Select(int index)
    {
        selected = index;
        blurb.text = Choices[index].Blurb;
        for (int i = 0; i < choiceImages.Length; i++)
        {
            if (choiceImages[i] == null) continue;
            choiceImages[i].color = i == index
                ? new Color(0.45f, 0.32f, 0.14f, 1f)
                : new Color(0.18f, 0.12f, 0.08f, 1f);
        }

        RefreshConfirm();
    }

    private void RefreshConfirm()
    {
        if (confirm == null) return;
        confirm.interactable = selected >= 0 && !string.IsNullOrWhiteSpace(input.text);
    }

    private void Confirm()
    {
        if (selected < 0) return;
        string cultName = input.text.Trim();
        if (cultName.Length == 0) return;

        Apply(Choices[selected].Stat);
        TutorialSession.SetCult(cultName, Choices[selected].Stat);
        FaithEclipseBar bar = FindAnyObjectByType<FaithEclipseBar>();
        if (bar != null) bar.RefreshNames();
        Confirmed = true;
        Dismiss();
    }

    private static void Apply(PlayerStats.PlayerStat stat)
    {
        HashSet<PlayerStats> seen = new HashSet<PlayerStats>();
        if (!InfluenceManager.IsNull) Once(InfluenceManager.Get.Stats);
        DoctrinesController doctrines = FindAnyObjectByType<DoctrinesController>();
        if (doctrines != null) Once(doctrines.Stats);
        TheologyMenu.PrepareNewMatch();

        void Once(PlayerStats stats)
        {
            if (stats == null || !seen.Add(stats)) return;
            stats.ResetValues();
            stats.ClearDoctrines();
            if (stat != PlayerStats.PlayerStat.None) stats.ChangeStat(stat, 1);
        }
    }

    private static TMP_Text AddLabel(Transform parent, TMP_FontAsset font, Material material, string value, float size, float height)
    {
        GameObject go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        LayoutElement element = go.GetComponent<LayoutElement>();
        element.preferredHeight = height;
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.font = font;
        if (material != null) text.fontSharedMaterial = material;
        text.text = value;
        text.fontSize = size;
        text.color = new Color(0.94f, 0.86f, 0.66f, 1f);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private static TMP_InputField AddInput(Transform parent, TMP_FontAsset font, Material material)
    {
        GameObject go = new GameObject("Name", typeof(RectTransform), typeof(Image), typeof(TMP_InputField), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = 52f;
        Image image = go.GetComponent<Image>();
        image.color = new Color(0.16f, 0.11f, 0.07f, 1f);

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(go.transform, false);
        Stretch((RectTransform)viewport.transform);
        ((RectTransform)viewport.transform).offsetMin = new Vector2(12f, 6f);
        ((RectTransform)viewport.transform).offsetMax = new Vector2(-12f, -6f);

        TextMeshProUGUI text = CreateText("Text", viewport.transform, font, material, string.Empty, 22f, new Color(0.94f, 0.88f, 0.74f, 1f));
        TextMeshProUGUI placeholder = CreateText("Placeholder", viewport.transform, font, material, "Cult name", 22f, new Color(0.94f, 0.86f, 0.66f, 0.35f));

        TMP_InputField field = go.GetComponent<TMP_InputField>();
        field.textViewport = (RectTransform)viewport.transform;
        field.textComponent = text;
        field.placeholder = placeholder;
        field.characterLimit = 22;
        field.lineType = TMP_InputField.LineType.SingleLine;
        field.targetGraphic = image;
        return field;
    }

    private static Button AddChoice(
        Transform parent,
        TMP_FontAsset font,
        Material material,
        string caption,
        UnityEngine.Events.UnityAction onClick,
        out Image image)
    {
        GameObject go = new GameObject(caption, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = 46f;
        image = go.GetComponent<Image>();
        image.color = new Color(0.18f, 0.12f, 0.08f, 1f);
        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);
        CreateText("Label", go.transform, font, material, caption, 20f, new Color(0.94f, 0.86f, 0.66f, 1f));
        return button;
    }

    private static TextMeshProUGUI CreateText(
        string name,
        Transform parent,
        TMP_FontAsset font,
        Material material,
        string value,
        float size,
        Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Stretch((RectTransform)go.transform);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.font = font;
        if (material != null) text.fontSharedMaterial = material;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
