using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zeke.Abilities;
using Zeke.Tooltips;

public class CultCreationPanel : MonoBehaviour
{
    private struct AttributeChoice
    {
        public PlayerStats.PlayerStat Stat;
        public string Tagline;
        public (Resource resource, int amount)[] StartingBonus;
    }

    private const int StartingPoints = 1;

    private static readonly AttributeChoice[] Choices =
    {
        new AttributeChoice
        {
            Stat = PlayerStats.PlayerStat.Diplomacy,
            Tagline = "Win over the poor with kind words and bread.",
            StartingBonus = new[] { (Resource.Flock, 50), (Resource.Materials, 20), (Resource.Authority, 1) }
        },
        new AttributeChoice
        {
            Stat = PlayerStats.PlayerStat.Aggresion,
            Tagline = "Rule the forges through fear and fire.",
            StartingBonus = new[] { (Resource.Flock, 25), (Resource.Zeal, 10), (Resource.Authority, 3), (Resource.Materials, -10), (Resource.Happiness, -25) }
        },
        new AttributeChoice
        {
            Stat = PlayerStats.PlayerStat.Stewardship,
            Tagline = "Make the faith pay its way in the markets.",
            StartingBonus = new[] { (Resource.Materials, 50), (Resource.Flock, 30) }
        },
        new AttributeChoice
        {
            Stat = PlayerStats.PlayerStat.Intrigue,
            Tagline = "Trade in whispers among revelers and nobles.",
            StartingBonus = new[] { (Resource.Zeal, 25), (Resource.Secrets, 30), (Resource.Materials, 10) }
        },
        new AttributeChoice
        {
            Stat = PlayerStats.PlayerStat.Learning,
            Tagline = "Win the scholars who read the heavens.",
            StartingBonus = new[] { (Resource.Wealth, 30), (Resource.Secrets, 30), (Resource.Materials, 30) }
        }
    };

    private const string Intro = "A new faith rises in Beheld. Give it a name, then choose the virtue it is known for. "
        + "Your virtue decides where your faith spreads fastest and what you start with.";

    private static readonly Color Ink = new Color(0.27f, 0.16f, 0.11f, 1f);
    private static readonly Color InkSoft = new Color(0.27f, 0.16f, 0.11f, 0.75f);
    private static readonly Color InkFaint = new Color(0.27f, 0.16f, 0.11f, 0.4f);
    private static readonly Color TitleInk = new Color(0.45f, 0.16f, 0.1f, 1f);
    private static readonly Color Selected = new Color(1f, 0.84f, 0.48f, 1f);
    private const string GainHex = "2E6B24";
    private const string LossHex = "8C1C12";

    private TooltipLinksDB glossary;
    private TMP_FontAsset font;
    private Material material;
    private TMP_InputField input;
    private TMP_Text detailTitle;
    private TMP_Text detailTagline;
    private TMP_Text detailEffects;
    private TMP_Text detailBonus;
    private TMP_Text detailNote;
    private TMP_Text hint;
    private Button confirm;
    private readonly Image[] choiceImages = new Image[Choices.Length];
    private int selected = -1;
    private int recommended = -1;
    private Districts? home;

    public RectTransform Card { get; private set; }
    public bool Confirmed { get; private set; }

    public static CultCreationPanel Create(Transform canvas)
    {
        ResetOpeningStats();

        TextMeshProUGUI sample = canvas.GetComponentInChildren<TextMeshProUGUI>(true);

        GameObject root = new GameObject("Cult Creation", typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(canvas, false);
        Stretch((RectTransform)root.transform);
        CanvasGroup group = root.GetComponent<CanvasGroup>();
        group.blocksRaycasts = true;
        group.interactable = true;

        CultCreationPanel panel = root.AddComponent<CultCreationPanel>();
        panel.glossary = Tooltip.Glossary;
        panel.font = sample != null ? sample.font : TMP_Settings.defaultFontAsset;
        panel.material = sample != null ? sample.fontSharedMaterial : null;
        panel.Build();
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

    private void Build()
    {
        Image dim = ImageOf("Dim", (RectTransform)transform, null, new Color(0.04f, 0.03f, 0.02f, 0.72f));
        Stretch(dim.rectTransform);

        RectTransform paper = BuildTemple();

        RectTransform content = RectOf("Content", paper);
        Stretch(content);
        content.offsetMin = new Vector2(64f, 34f);
        content.offsetMax = new Vector2(-64f, -54f);
        VerticalLayoutGroup layout = Column(content, 6f, TextAnchor.UpperCenter);
        layout.padding = new RectOffset(8, 8, 4, 0);

        AddLabel(content, "FOUND YOUR CULT", 32f, 40f, TitleInk, TextAlignmentOptions.Center);
        TMP_Text intro = AddLabel(content, Intro, 17f, 48f, InkSoft, TextAlignmentOptions.Center);
        Link(intro, Intro);

        RectTransform body = RectOf("Body", content);
        body.gameObject.AddComponent<LayoutElement>().preferredHeight = 344f;
        HorizontalLayoutGroup row = body.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 24f;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = true;

        BuildChoices(body);
        BuildDetails(body);

        hint = AddLabel(content, string.Empty, 15f, 22f, InkSoft, TextAlignmentOptions.Center);
        confirm = AddButton(content, "FOUND THE CULT", Confirm, 50f, out _);

        home = HomeDistrict();
        recommended = Recommended();
        MarkRecommended();
        Select(recommended >= 0 ? recommended : 0);
    }

    private RectTransform BuildTemple()
    {
        RectTransform temple = RectOf("Temple", (RectTransform)transform);
        temple.anchorMin = new Vector2(0.5f, 0.5f);
        temple.anchorMax = new Vector2(0.5f, 0.5f);
        temple.pivot = new Vector2(0.5f, 0.5f);
        Card = temple;

        const float baseHeight = 64f;
        const float columnHeight = 660f;
        const float columnX = 560f;
        Sprite columnSprite = UiArt.Find("Torre_Columna");
        float columnAspect = columnSprite != null && columnSprite.rect.height > 1f
            ? columnSprite.rect.width / columnSprite.rect.height
            : 0.16f;
        float columnWidth = columnHeight * columnAspect;
        float columnTop = baseHeight + columnHeight;
        float outerSpan = columnX * 2f + columnWidth;

        Sprite pedimentSprite = UiArt.Find("Torre_Fronton");
        float pedimentAspect = pedimentSprite != null && pedimentSprite.rect.height > 1f
            ? pedimentSprite.rect.width / pedimentSprite.rect.height
            : 6.2f;
        float pedimentHeight = outerSpan / pedimentAspect;
        temple.sizeDelta = new Vector2(outerSpan + 24f, columnTop + pedimentHeight + 8f);

        Image body = Piece(temple, "Body", "Torre_Cuerpo", false);
        SetRect(body.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, baseHeight), new Vector2(columnX * 2f - columnWidth, columnHeight));
        body.raycastTarget = false;

        Image parchment = Piece(temple, "Parchment", "Papel_Con_Borde", false);
        SetRect(parchment.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, baseHeight + 10f), new Vector2(columnX * 2f - columnWidth - 16f, columnHeight - 20f));

        Image plinth = Piece(temple, "Base", "Torre_Base", false);
        SetRect(plinth.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(outerSpan + 48f, baseHeight));
        plinth.raycastTarget = false;

        PlaceColumn(temple, "Column Left", -columnX, baseHeight, columnWidth, columnHeight);
        PlaceColumn(temple, "Column Right", columnX, baseHeight, columnWidth, columnHeight);

        Image pediment = Piece(temple, "Pediment", "Torre_Fronton", true);
        SetRect(pediment.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, columnTop), new Vector2(outerSpan, pedimentHeight));
        pediment.raycastTarget = false;

        return parchment.rectTransform;
    }

    private void BuildChoices(RectTransform body)
    {
        RectTransform left = RectOf("Choices", body);
        LayoutElement leftSize = left.gameObject.AddComponent<LayoutElement>();
        leftSize.minWidth = 330f;
        leftSize.preferredWidth = 330f;
        leftSize.flexibleWidth = 0f;
        Column(left, 6f, TextAnchor.UpperCenter);

        AddLabel(left, "NAME", 14f, 18f, TitleInk, TextAlignmentOptions.Center);
        input = AddInput(left);
        input.onValueChanged.AddListener(_ => RefreshConfirm());
        AddLabel(left, "STARTING VIRTUE", 14f, 18f, TitleInk, TextAlignmentOptions.Center);

        MapStatsBook book = FindAnyObjectByType<MapStatsBook>(FindObjectsInactive.Include);
        for (int i = 0; i < Choices.Length; i++)
        {
            int index = i;
            Button button = AddButton(left, Virtues.Name(Choices[i].Stat).ToUpperInvariant(), () => Select(index), 42f, out choiceImages[i]);
            AddRibbons(button.transform, Virtues.DistrictsOf(Choices[i].Stat), book);
            AddHover(button.gameObject, () => ShowDetails(index), () => ShowDetails(selected));
        }
    }

    private void BuildDetails(RectTransform body)
    {
        Image card = ImageOf("Details", body, null, new Color(0.27f, 0.16f, 0.11f, 0.07f));
        card.raycastTarget = false;
        LayoutElement cardSize = card.gameObject.AddComponent<LayoutElement>();
        cardSize.preferredWidth = 0f;
        cardSize.flexibleWidth = 1f;
        VerticalLayoutGroup layout = Column(card.rectTransform, 4f, TextAnchor.UpperLeft);
        layout.padding = new RectOffset(20, 20, 14, 12);

        detailTitle = AddLabel(card.rectTransform, string.Empty, 28f, 34f, TitleInk, TextAlignmentOptions.Left);
        detailTagline = AddLabel(card.rectTransform, string.Empty, 16f, 26f, InkSoft, TextAlignmentOptions.Left);
        AddLabel(card.rectTransform, "WHAT IT CHANGES", 14f, 22f, TitleInk, TextAlignmentOptions.Left);
        detailEffects = AddLabel(card.rectTransform, string.Empty, 17f, 128f, Ink, TextAlignmentOptions.TopLeft);
        AddLabel(card.rectTransform, "YOU START WITH", 14f, 22f, TitleInk, TextAlignmentOptions.Left);
        detailBonus = AddLabel(card.rectTransform, string.Empty, 17f, 26f, Ink, TextAlignmentOptions.Left);
        detailNote = AddLabel(card.rectTransform, string.Empty, 15f, 40f, InkSoft, TextAlignmentOptions.TopLeft);
    }

    private void Select(int index)
    {
        selected = index;
        for (int i = 0; i < choiceImages.Length; i++)
        {
            if (choiceImages[i] != null) choiceImages[i].color = i == index ? Selected : Color.white;
        }

        ShowDetails(index);
        RefreshConfirm();
    }

    private void ShowDetails(int index)
    {
        if (index < 0 || index >= Choices.Length) return;

        AttributeChoice choice = Choices[index];
        List<Districts> districts = Virtues.DistrictsOf(choice.Stat);

        detailTitle.text = Virtues.Name(choice.Stat).ToUpperInvariant();
        detailTagline.text = choice.Tagline;
        Link(detailEffects, string.Join("\n", Effects(choice.Stat, districts)));
        detailBonus.text = Bonus(choice.StartingBonus);
        Link(detailNote, index == recommended && home.HasValue
            ? $"A good first pick: your cult starts in {Virtues.DistrictList(new List<Districts> { home.Value })}."
            : "Doctrines can raise any virtue later in the game.");
    }

    private static List<string> Effects(PlayerStats.PlayerStat virtue, List<Districts> districts)
    {
        var lines = new List<string>();
        string where = Virtues.DistrictList(districts);

        int percent = Mathf.RoundToInt(StartingPoints * Virtues.InfluencePercentPerPoint);
        if (districts.Count > 0 && percent > 0)
        {
            lines.Add($"- Your clerics gain {percent}% more influence in {where}.");
        }

        string yields = Yields(districts);
        if (yields.Length > 0)
        {
            lines.Add($"- {Capitalized(where)} {(districts.Count == 1 ? "yields" : "yield")} {yields}.");
        }

        int learning = virtue == PlayerStats.PlayerStat.Learning ? StartingPoints : 0;
        int stewardship = virtue == PlayerStats.PlayerStat.Stewardship ? StartingPoints : 0;
        int training = PlayerClergy.TrainingFor(learning, stewardship) - PlayerClergy.TrainingFor(0, 0);
        if (training > 0)
        {
            PlayerClergy clergy = FindAnyObjectByType<PlayerClergy>();
            string cost = clergy != null ? $" ({clergy.OrdinationCost} makes a cleric)" : string.Empty;
            lines.Add($"- New clerics train faster: +{training} training each turn{cost}.");
        }

        lines.Add($"- Better odds in event choices that test {Virtues.Name(virtue)}.");
        return lines;
    }

    private static string Yields(List<Districts> districts)
    {
        DistrictProductionConfig config = InfluenceManager.IsNull ? null : InfluenceManager.Get.ProductionConfig;
        if (config == null) return string.Empty;

        var parts = new Dictionary<Resource, int>();
        var order = new List<Resource>();
        foreach (Districts district in districts)
        {
            if (!config.TryGetEntry(district, out DistrictProductionConfig.ProductionEntry entry) || entry.yields == null) continue;
            foreach (DistrictProductionConfig.ResourceYield yield in entry.yields)
            {
                if (!parts.ContainsKey(yield.resource)) order.Add(yield.resource);
                parts.TryGetValue(yield.resource, out int current);
                parts[yield.resource] = current + yield.parts;
            }
        }

        order.Sort((a, b) => parts[b].CompareTo(parts[a]));
        var names = order.ConvertAll(resource => resource.ToString());
        if (names.Count <= 1) return names.Count == 1 ? names[0] : string.Empty;
        return $"{string.Join(", ", names.GetRange(0, names.Count - 1))} and {names[names.Count - 1]}";
    }

    private static string Bonus((Resource resource, int amount)[] bonus)
    {
        var parts = new List<string>();
        foreach ((Resource resource, int amount) in bonus)
        {
            parts.Add($"<color=#{(amount >= 0 ? GainHex : LossHex)}>{(amount >= 0 ? "+" : "")}{amount} {resource}</color>");
        }

        return parts.Count > 0 ? string.Join("   ", parts) : "Nothing extra.";
    }

    private static string Capitalized(string text)
    {
        return string.IsNullOrEmpty(text) ? text : char.ToUpper(text[0], CultureInfo.InvariantCulture) + text.Substring(1);
    }

    private static Districts? HomeDistrict()
    {
        DistrictZone zone = TutorialContent.FindPlayerZone();
        return zone != null ? zone.District : null;
    }

    private int Recommended()
    {
        if (!home.HasValue) return -1;
        for (int i = 0; i < Choices.Length; i++)
        {
            if (Virtues.DistrictsOf(Choices[i].Stat).Contains(home.Value)) return i;
        }

        return -1;
    }

    private void MarkRecommended()
    {
        if (recommended < 0 || choiceImages[recommended] == null) return;

        TextMeshProUGUI tag = CreateText("First Pick", choiceImages[recommended].transform, "first pick", 12f, TitleInk, TextAlignmentOptions.Right);
        tag.rectTransform.offsetMax = new Vector2(-14f, 0f);
    }

    private void RefreshConfirm()
    {
        if (confirm == null) return;

        bool named = input != null && !string.IsNullOrWhiteSpace(input.text);
        confirm.interactable = selected >= 0 && named;
        if (hint != null) hint.text = named ? string.Empty : "Type a name for your cult to begin.";
    }

    private void Confirm()
    {
        if (selected < 0) return;
        string cultName = input.text.Trim();
        if (cultName.Length == 0) return;

        AttributeChoice choice = Choices[selected];
        Apply(choice.Stat);
        TutorialSession.SetCult(cultName, choice.Stat);
        foreach ((Resource resource, int amount) in choice.StartingBonus) ResourceManager.AddResource(resource, amount);

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
            if (stat != PlayerStats.PlayerStat.None) stats.ChangeStat(stat, StartingPoints);
        }
    }

    private void Link(TMP_Text text, string value)
    {
        if (glossary == null)
        {
            text.text = value;
            return;
        }

        text.text = glossary.Highlight(value);
        if (text.GetComponent<HoverLinkHandlerTMPro>() != null) return;
        text.gameObject.AddComponent<HoverLinkHandlerTMPro>();
        text.gameObject.AddComponent<LinkTooltipTrigger>().Database = glossary;
    }

    private static void AddRibbons(Transform button, List<Districts> districts, MapStatsBook book)
    {
        if (book == null) return;

        for (int i = 0; i < districts.Count && i < 2; i++)
        {
            Sprite marker = book.MarkerFor(districts[i]);
            if (marker == null) continue;

            Image ribbon = ImageOf("Ribbon", (RectTransform)button, marker, Color.white);
            ribbon.preserveAspect = true;
            ribbon.raycastTarget = false;
            SetRect(ribbon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f + i * 22f, 0f), new Vector2(24f, 28f));
        }
    }

    private static void AddHover(GameObject target, UnityEngine.Events.UnityAction onEnter, UnityEngine.Events.UnityAction onExit)
    {
        EventTrigger trigger = target.AddComponent<EventTrigger>();
        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => onEnter());
        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => onExit());
        trigger.triggers.Add(enter);
        trigger.triggers.Add(exit);
    }

    private static VerticalLayoutGroup Column(RectTransform rect, float spacing, TextAnchor alignment)
    {
        VerticalLayoutGroup layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = alignment;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return layout;
    }

    private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void PlaceColumn(RectTransform parent, string name, float x, float foot, float width, float height)
    {
        Image image = Piece(parent, name, "Torre_Columna", true);
        SetRect(image.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(x, foot), new Vector2(width, height));
        image.raycastTarget = false;
    }

    private static Image Piece(RectTransform parent, string name, string spriteName, bool keepAspect)
    {
        Sprite sprite = UiArt.Find(spriteName);
        Image image = ImageOf(name, parent, sprite, Color.white);
        image.preserveAspect = keepAspect && sprite != null;
        image.raycastTarget = sprite != null;
        return image;
    }

    private TMP_Text AddLabel(Transform parent, string value, float size, float height, Color color, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = height;
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.font = font;
        if (material != null) text.fontSharedMaterial = material;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private TMP_InputField AddInput(Transform parent)
    {
        GameObject go = new GameObject("Name", typeof(RectTransform), typeof(Image), typeof(TMP_InputField), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = 52f;
        Image image = go.GetComponent<Image>();
        image.sprite = UiArt.Find("Ladrillo_Gris_Largo_1");
        image.type = Image.Type.Simple;
        image.color = Color.white;

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(go.transform, false);
        Stretch((RectTransform)viewport.transform);
        ((RectTransform)viewport.transform).offsetMin = new Vector2(18f, 8f);
        ((RectTransform)viewport.transform).offsetMax = new Vector2(-18f, -8f);

        TextMeshProUGUI text = CreateText("Text", viewport.transform, string.Empty, 24f, Ink, TextAlignmentOptions.Center);
        TextMeshProUGUI placeholder = CreateText("Placeholder", viewport.transform, "Type a name", 24f, InkFaint, TextAlignmentOptions.Center);

        TMP_InputField field = go.GetComponent<TMP_InputField>();
        field.textViewport = (RectTransform)viewport.transform;
        field.textComponent = text;
        field.placeholder = placeholder;
        field.characterLimit = 22;
        field.lineType = TMP_InputField.LineType.SingleLine;
        field.targetGraphic = image;
        return field;
    }

    private Button AddButton(Transform parent, string caption, UnityEngine.Events.UnityAction onClick, float height, out Image image)
    {
        GameObject go = new GameObject(caption, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = height;
        image = go.GetComponent<Image>();
        image.sprite = UiArt.Find("Ladrillo_Gris_Largo_1");
        image.type = Image.Type.Simple;
        image.color = Color.white;
        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
        button.colors = colors;
        button.onClick.AddListener(onClick);
        TextMeshProUGUI label = CreateText("Label", go.transform, caption, 20f, Ink, TextAlignmentOptions.Center);
        label.enableAutoSizing = true;
        label.fontSizeMin = 12f;
        label.fontSizeMax = 20f;
        return button;
    }

    private TextMeshProUGUI CreateText(string name, Transform parent, string value, float size, Color color, TextAlignmentOptions alignment)
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
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private static Image ImageOf(string name, RectTransform parent, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = true;
        return image;
    }

    private static RectTransform RectOf(string name, RectTransform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }


    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
