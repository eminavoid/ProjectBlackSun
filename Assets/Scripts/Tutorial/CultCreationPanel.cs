using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pantalla completa para nombrar el culto. Usa el frontón, las columnas y el papel de los otros menús.
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

    private static readonly Color Ink = new Color(0.27f, 0.16f, 0.11f, 1f);
    private static readonly Color InkFaint = new Color(0.27f, 0.16f, 0.11f, 0.4f);
    private static readonly Color TitleInk = new Color(0.45f, 0.16f, 0.1f, 1f);
    private static readonly Dictionary<string, Sprite> ArtCache = new Dictionary<string, Sprite>();

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
        Image dim = ImageOf("Dim", (RectTransform)transform, null, new Color(0.04f, 0.03f, 0.02f, 0.72f));
        Stretch(dim.rectTransform);

        RectTransform temple = RectOf("Temple", (RectTransform)transform);
        temple.anchorMin = new Vector2(0.5f, 0.5f);
        temple.anchorMax = new Vector2(0.5f, 0.5f);
        temple.pivot = new Vector2(0.5f, 0.5f);
        Card = temple;

        const float baseHeight = 64f;
        const float columnHeight = 700f;
        Sprite columnSprite = Art("Torre_Columna");
        float columnAspect = columnSprite != null && columnSprite.rect.height > 1f
            ? columnSprite.rect.width / columnSprite.rect.height
            : 0.16f;
        float columnWidth = columnHeight * columnAspect;
        float columnX = 390f;
        float columnFoot = baseHeight;
        float columnTop = columnFoot + columnHeight;
        float outerSpan = columnX * 2f + columnWidth;
        const float roofOverhang = 0f;

        Sprite pedimentSprite = Art("Torre_Fronton");
        float pedimentWidth = outerSpan + roofOverhang * 2f;
        float pedimentAspect = pedimentSprite != null && pedimentSprite.rect.height > 1f
            ? pedimentSprite.rect.width / pedimentSprite.rect.height
            : 6.2f;
        float pedimentHeight = pedimentWidth / pedimentAspect;
        temple.sizeDelta = new Vector2(pedimentWidth + 24f, columnTop + pedimentHeight + 8f);

        Image body = Piece(temple, "Body", "Torre_Cuerpo", false);
        SetRect(body.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, baseHeight), new Vector2(columnX * 2f - columnWidth, columnHeight));
        body.raycastTarget = false;

        Image parchment = Piece(temple, "Parchment", "Papel_Con_Borde", false);
        float paperBottom = baseHeight + 10f;
        float paperHeight = columnHeight - 20f;
        SetRect(parchment.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, paperBottom), new Vector2(columnX * 2f - columnWidth - 16f, paperHeight));

        Image plinth = Piece(temple, "Base", "Torre_Base", false);
        SetRect(plinth.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(outerSpan + 48f, baseHeight));
        plinth.raycastTarget = false;

        PlaceColumn(temple, "Column Left", -columnX, columnFoot, columnWidth, columnHeight);
        PlaceColumn(temple, "Column Right", columnX, columnFoot, columnWidth, columnHeight);

        Image pediment = Piece(temple, "Pediment", "Torre_Fronton", true);
        SetRect(pediment.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, columnTop), new Vector2(pedimentWidth, pedimentHeight));
        pediment.raycastTarget = false;

        RectTransform content = RectOf("Content", parchment.rectTransform);
        Stretch(content);
        content.offsetMin = new Vector2(64f, 56f);
        content.offsetMax = new Vector2(-64f, -52f);

        VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 8, 4);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        AddLabel(content, font, material, "NAME YOUR CULT", 32f, 42f, TitleInk, TextAlignmentOptions.Center);
        input = AddInput(content, font, material);
        input.onValueChanged.AddListener(_ => RefreshConfirm());
        AddLabel(content, font, material, "STARTING ATTRIBUTE", 16f, 24f, Ink, TextAlignmentOptions.Center);

        for (int i = 0; i < Choices.Length; i++)
        {
            int index = i;
            AddChoice(content, font, material, Choices[i].Label, () => Select(index), out choiceImages[i]);
        }

        blurb = AddLabel(content, font, material, "Choose the attribute your cult starts with.", 18f, 52f, Ink, TextAlignmentOptions.Center);
        confirm = AddChoice(content, font, material, "CONFIRM", Confirm, out _);
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
                ? new Color(1f, 0.84f, 0.48f, 1f)
                : Color.white;
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

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMin;
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
        Sprite sprite = Art(spriteName);
        Image image = ImageOf(name, parent, sprite, Color.white);
        image.preserveAspect = keepAspect && sprite != null;
        image.raycastTarget = sprite != null;
        return image;
    }

    private static TMP_Text AddLabel(
        Transform parent,
        TMP_FontAsset font,
        Material material,
        string value,
        float size,
        float height,
        Color color,
        TextAlignmentOptions alignment)
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

    private static TMP_InputField AddInput(Transform parent, TMP_FontAsset font, Material material)
    {
        GameObject go = new GameObject("Name", typeof(RectTransform), typeof(Image), typeof(TMP_InputField), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = 58f;
        Image image = go.GetComponent<Image>();
        image.sprite = Art("Ladrillo_Gris_Largo_1");
        image.type = Image.Type.Simple;
        image.color = Color.white;

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(go.transform, false);
        Stretch((RectTransform)viewport.transform);
        ((RectTransform)viewport.transform).offsetMin = new Vector2(18f, 8f);
        ((RectTransform)viewport.transform).offsetMax = new Vector2(-18f, -8f);

        TextMeshProUGUI text = CreateText("Text", viewport.transform, font, material, string.Empty, 24f, Ink, TextAlignmentOptions.Center);
        TextMeshProUGUI placeholder = CreateText("Placeholder", viewport.transform, font, material, "Cult name", 24f, InkFaint, TextAlignmentOptions.Center);

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
        go.GetComponent<LayoutElement>().preferredHeight = 52f;
        image = go.GetComponent<Image>();
        image.sprite = Art("Ladrillo_Gris_Largo_1");
        image.type = Image.Type.Simple;
        image.color = Color.white;
        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
        button.colors = colors;
        button.onClick.AddListener(onClick);
        CreateText("Label", go.transform, font, material, caption, 20f, Ink, TextAlignmentOptions.Center);
        return button;
    }

    private static TextMeshProUGUI CreateText(
        string name,
        Transform parent,
        TMP_FontAsset font,
        Material material,
        string value,
        float size,
        Color color,
        TextAlignmentOptions alignment)
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

    private static Sprite Art(string spriteName)
    {
        if (ArtCache.TryGetValue(spriteName, out Sprite cached)) return cached;

        Sprite[] sprites = Resources.FindObjectsOfTypeAll<Sprite>();
        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite sprite = sprites[i];
            if (sprite == null || ArtCache.ContainsKey(sprite.name)) continue;
            ArtCache[sprite.name] = sprite;
        }

        ArtCache.TryGetValue(spriteName, out cached);
        if (!ArtCache.ContainsKey(spriteName)) ArtCache[spriteName] = cached;
        return cached;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
