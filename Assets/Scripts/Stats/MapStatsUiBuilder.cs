using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zeke.UI;

public class MapStatsWindowView
{
    public GameObject Root;
    public RectTransform RootRect;
    public UIWindow Window;
    public TextMeshProUGUI Title;
    public Button CloseButton;
    public RectTransform TabsRoot;
    public ScrollRect Scroll;
    public RectTransform Content;
    public GameObject FactionRowTemplate;
    public GameObject SeedRowTemplate;
    public GameObject NodeRowTemplate;
    public GameObject DistrictRowTemplate;
    public GameObject SectionLabelTemplate;
    public GameObject BodyTextTemplate;
    public GameObject ButtonRowTemplate;
}

public class MapContextMenuView
{
    public GameObject Root;
    public RectTransform RootRect;
    public TextMeshProUGUI Title;
    public Button ActionButton;
    public TextMeshProUGUI ActionLabel;
}

/// <summary>
/// Factory de uGUI para stats. Misma jerarquía nombrada que los prefabs de arte.
/// </summary>
public static class MapStatsUiBuilder
{
    private const string UiFontResourcePath = "Fonts & Materials/LiberationSans SDF";

    public static readonly Color PanelColor = new Color(0.07f, 0.08f, 0.1f, 0.94f);
    public static readonly Color HeaderColor = new Color(0.12f, 0.13f, 0.16f, 1f);
    public static readonly Color TabIdleColor = new Color(0.16f, 0.17f, 0.2f, 1f);
    public static readonly Color TabSelectedColor = new Color(0.38f, 0.18f, 0.48f, 1f);
    public static readonly Color RowColor = new Color(0.11f, 0.12f, 0.15f, 1f);
    public static readonly Color ButtonColor = new Color(0.22f, 0.23f, 0.28f, 1f);
    public static readonly Color BodyTextColor = new Color(0.92f, 0.93f, 0.94f, 1f);

    private static TMP_FontAsset uiFont;

    public static MapStatsWindowView BindWindow(GameObject root)
    {
        ApplyReadableFont(root);
        MapStatsWindowView view = new MapStatsWindowView
        {
            Root = root,
            RootRect = root.GetComponent<RectTransform>(),
            Window = root.GetComponent<UIWindow>()
        };

        view.Title = FindTmp(root.transform, "Title");
        Transform close = FindChild(root.transform, "Close");
        if (close != null) view.CloseButton = close.GetComponent<Button>();

        Transform tabs = FindChild(root.transform, "Tabs");
        if (tabs != null) view.TabsRoot = tabs.GetComponent<RectTransform>();

        Transform body = FindChild(root.transform, "Body");
        if (body != null) view.Scroll = body.GetComponent<ScrollRect>();

        Transform content = FindChild(root.transform, "Content");
        if (content != null) view.Content = content.GetComponent<RectTransform>();

        view.FactionRowTemplate = FindChild(root.transform, "FactionRow")?.gameObject;
        view.SeedRowTemplate = FindChild(root.transform, "SeedRow")?.gameObject;
        view.NodeRowTemplate = FindChild(root.transform, "NodeRow")?.gameObject;
        view.DistrictRowTemplate = FindChild(root.transform, "DistrictRow")?.gameObject;
        view.SectionLabelTemplate = FindChild(root.transform, "SectionLabel")?.gameObject;
        view.BodyTextTemplate = FindChild(root.transform, "BodyText")?.gameObject;
        view.ButtonRowTemplate = FindChild(root.transform, "ButtonRow")?.gameObject;
        return view;
    }

    public static MapContextMenuView BindContextMenu(GameObject root)
    {
        ApplyReadableFont(root);
        MapContextMenuView view = new MapContextMenuView
        {
            Root = root,
            RootRect = root.GetComponent<RectTransform>()
        };

        view.Title = FindTmp(root.transform, "Title");
        Transform action = FindChild(root.transform, "Action");
        if (action != null)
        {
            view.ActionButton = action.GetComponent<Button>();
            view.ActionLabel = action.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        return view;
    }

    public static MapStatsWindowView BuildWindow(Transform canvas)
    {
        GameObject root = CreateUiObject("MapStatsWindow", canvas);
        root.SetActive(false);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(1f, 0.5f);
        rootRect.anchorMax = new Vector2(1f, 0.5f);
        rootRect.pivot = new Vector2(1f, 0.5f);
        rootRect.anchoredPosition = new Vector2(-24f, 0f);
        rootRect.sizeDelta = new Vector2(460f, 720f);

        Image image = AddImage(root, PanelColor, true);
        image.raycastTarget = true;
        UIWindow window = root.AddComponent<UIWindow>();
        root.AddComponent<MenuOpenSound>();

        GameObject header = CreateUiObject("Header", root.transform);
        StretchTop(header.GetComponent<RectTransform>(), 52f);
        AddImage(header, HeaderColor, true);
        DraggableWindow drag = header.AddComponent<DraggableWindow>();
        drag.SetTarget(rootRect);

        GameObject titleGo = CreateUiObject("Title", header.transform);
        RectTransform titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.anchorMin = Vector2.zero;
        titleRect.anchorMax = Vector2.one;
        titleRect.offsetMin = new Vector2(16f, 4f);
        titleRect.offsetMax = new Vector2(-72f, -4f);
        TextMeshProUGUI title = AddTmp(titleGo, 22, TextAlignmentOptions.MidlineLeft);
        title.text = "Estadísticas";
        Register(window, titleGo, "Title", title);

        GameObject closeGo = CreateUiObject("Close", header.transform);
        RectTransform closeRect = closeGo.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 0.5f);
        closeRect.anchorMax = new Vector2(1f, 0.5f);
        closeRect.pivot = new Vector2(1f, 0.5f);
        closeRect.anchoredPosition = new Vector2(-10f, 0f);
        closeRect.sizeDelta = new Vector2(48f, 36f);
        AddImage(closeGo, ButtonColor, true);
        Button closeButton = closeGo.AddComponent<Button>();
        closeGo.AddComponent<UIClickSound>();
        GameObject closeLabelGo = CreateUiObject("Label", closeGo.transform);
        Stretch(closeLabelGo.GetComponent<RectTransform>());
        TextMeshProUGUI closeLabel = AddTmp(closeLabelGo, 20, TextAlignmentOptions.Center);
        closeLabel.text = "X";
        Register(window, closeGo, "Close", closeButton);

        GameObject tabs = CreateUiObject("Tabs", root.transform);
        RectTransform tabsRect = tabs.GetComponent<RectTransform>();
        tabsRect.anchorMin = new Vector2(0f, 1f);
        tabsRect.anchorMax = new Vector2(1f, 1f);
        tabsRect.pivot = new Vector2(0.5f, 1f);
        tabsRect.anchoredPosition = new Vector2(0f, -52f);
        tabsRect.sizeDelta = new Vector2(0f, 40f);
        HorizontalLayoutGroup tabLayout = tabs.AddComponent<HorizontalLayoutGroup>();
        tabLayout.padding = new RectOffset(8, 8, 4, 4);
        tabLayout.spacing = 4f;
        tabLayout.childAlignment = TextAnchor.MiddleLeft;
        tabLayout.childControlWidth = true;
        tabLayout.childControlHeight = true;
        tabLayout.childForceExpandWidth = true;
        tabLayout.childForceExpandHeight = true;
        Register(window, tabs, "Tabs", tabLayout);

        GameObject body = CreateUiObject("Body", root.transform);
        RectTransform bodyRect = body.GetComponent<RectTransform>();
        bodyRect.anchorMin = Vector2.zero;
        bodyRect.anchorMax = Vector2.one;
        bodyRect.offsetMin = new Vector2(8f, 8f);
        bodyRect.offsetMax = new Vector2(-8f, -96f);
        Image bodyImage = AddImage(body, new Color(0.05f, 0.055f, 0.07f, 0.5f), true);
        bodyImage.raycastTarget = true;

        GameObject viewport = CreateUiObject("Viewport", body.transform);
        RectTransform viewportRect = Stretch(viewport.GetComponent<RectTransform>());
        viewport.AddComponent<RectMask2D>();
        Image viewportImage = AddImage(viewport, Color.clear, true);
        viewportImage.raycastTarget = true;

        GameObject content = CreateUiObject("Content", viewport.transform);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;
        VerticalLayoutGroup contentLayout = content.AddComponent<VerticalLayoutGroup>();
        contentLayout.padding = new RectOffset(10, 10, 10, 10);
        contentLayout.spacing = 6f;
        contentLayout.childAlignment = TextAnchor.UpperLeft;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        ContentSizeFitter contentFitter = content.AddComponent<ContentSizeFitter>();
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        Register(window, content, "Content", contentLayout);

        ScrollRect scroll = body.AddComponent<ScrollRect>();
        scroll.viewport = viewportRect;
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;
        Register(window, body, "Body", scroll);

        GameObject templates = CreateUiObject("Templates", root.transform);
        templates.SetActive(false);

        GameObject factionRow = BuildFactionRowTemplate(templates.transform);
        GameObject seedRow = BuildSeedRowTemplate(templates.transform);
        GameObject nodeRow = BuildNodeRowTemplate(templates.transform);
        GameObject districtRow = BuildDistrictRowTemplate(templates.transform);
        GameObject sectionLabel = BuildSectionLabelTemplate(templates.transform);
        GameObject bodyText = BuildBodyTextTemplate(templates.transform);
        GameObject buttonRow = BuildButtonRowTemplate(templates.transform);

        return new MapStatsWindowView
        {
            Root = root,
            RootRect = rootRect,
            Window = window,
            Title = title,
            CloseButton = closeButton,
            TabsRoot = tabsRect,
            Scroll = scroll,
            Content = contentRect,
            FactionRowTemplate = factionRow,
            SeedRowTemplate = seedRow,
            NodeRowTemplate = nodeRow,
            DistrictRowTemplate = districtRow,
            SectionLabelTemplate = sectionLabel,
            BodyTextTemplate = bodyText,
            ButtonRowTemplate = buttonRow
        };
    }

    public static MapContextMenuView BuildContextMenu(Transform canvas)
    {
        GameObject root = CreateUiObject("MapContextMenu", canvas);
        root.SetActive(false);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0f, 1f);
        rootRect.sizeDelta = new Vector2(240f, 86f);
        AddImage(root, PanelColor, true);

        VerticalLayoutGroup layout = root.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        GameObject titleGo = CreateUiObject("Title", root.transform);
        LayoutElement titleLe = titleGo.AddComponent<LayoutElement>();
        titleLe.minHeight = 22f;
        titleLe.preferredHeight = 22f;
        TextMeshProUGUI title = AddTmp(titleGo, 14, TextAlignmentOptions.MidlineLeft);
        title.text = "Mapa";

        GameObject actionGo = CreateUiObject("Action", root.transform);
        LayoutElement actionLe = actionGo.AddComponent<LayoutElement>();
        actionLe.minHeight = 40f;
        actionLe.preferredHeight = 40f;
        AddImage(actionGo, ButtonColor, true);
        Button action = actionGo.AddComponent<Button>();
        actionGo.AddComponent<UIClickSound>();
        GameObject actionLabelGo = CreateUiObject("Label", actionGo.transform);
        Stretch(actionLabelGo.GetComponent<RectTransform>());
        TextMeshProUGUI actionLabel = AddTmp(actionLabelGo, 15, TextAlignmentOptions.Center);
        actionLabel.text = "Estadísticas";

        return new MapContextMenuView
        {
            Root = root,
            RootRect = rootRect,
            Title = title,
            ActionButton = action,
            ActionLabel = actionLabel
        };
    }

    public static Button CreateTabButton(Transform parent, string label)
    {
        GameObject go = CreateUiObject("Tab_" + label, parent);
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.minWidth = 48f;
        le.preferredHeight = 32f;
        le.flexibleWidth = 1f;
        AddImage(go, TabIdleColor, true);
        Button button = go.AddComponent<Button>();
        go.AddComponent<UIClickSound>();
        GameObject labelGo = CreateUiObject("Label", go.transform);
        Stretch(labelGo.GetComponent<RectTransform>()).offsetMin = new Vector2(2f, 2f);
        labelGo.GetComponent<RectTransform>().offsetMax = new Vector2(-2f, -2f);
        TextMeshProUGUI tmp = AddTmp(labelGo, 13, TextAlignmentOptions.Center);
        tmp.text = label;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 11;
        tmp.fontSizeMax = 14;
        return button;
    }

    public static void SetTabSelected(Button button, bool selected)
    {
        if (button == null) return;
        Image image = button.GetComponent<Image>();
        if (image != null) image.color = selected ? TabSelectedColor : TabIdleColor;
    }

    public static GameObject SpawnSection(MapStatsWindowView view, string text)
    {
        GameObject go = Object.Instantiate(view.SectionLabelTemplate, view.Content);
        go.SetActive(true);
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        if (tmp != null) tmp.text = text;
        return go;
    }

    public static GameObject SpawnBody(MapStatsWindowView view, string text)
    {
        GameObject go = Object.Instantiate(view.BodyTextTemplate, view.Content);
        go.SetActive(true);
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        if (tmp != null) tmp.text = text;
        return go;
    }

    public static GameObject SpawnFactionRow(MapStatsWindowView view, MapStatsFactionRow row, string value)
    {
        GameObject go = Object.Instantiate(view.FactionRowTemplate, view.Content);
        go.SetActive(true);
        Transform swatch = FindChild(go.transform, "Swatch");
        if (swatch != null)
        {
            Image image = swatch.GetComponent<Image>();
            if (image != null) image.color = row.Color;
        }

        SetNamedText(go.transform, "Name", row.DisplayName);
        SetNamedText(go.transform, "Value", value);
        return go;
    }

    public static Button SpawnSeedRow(MapStatsWindowView view, MapStatsSeedRow row)
    {
        GameObject go = Object.Instantiate(view.SeedRowTemplate, view.Content);
        go.SetActive(true);
        SetNamedText(go.transform, "Title", row.Title);
        SetNamedText(go.transform, "Meta",
            $"{row.DistrictName} / {row.SectorName} · {row.TurnsRemaining} turno(s) · {row.EventType} {row.Difficulty}");

        Image image = go.GetComponent<Image>();
        if (image != null) image.raycastTarget = true;
        Button button = go.GetComponent<Button>();
        if (button == null) button = go.AddComponent<Button>();
        if (go.GetComponent<UIClickSound>() == null) go.AddComponent<UIClickSound>();
        return button;
    }

    public static Button SpawnNodeRow(MapStatsWindowView view, MapStatsNodeRow row)
    {
        GameObject go = Object.Instantiate(view.NodeRowTemplate, view.Content);
        go.SetActive(true);
        SetNamedText(go.transform, "Name", row.SectorName);
        SetNamedText(go.transform, "Control", row.ControlLabel + "  " + row.Influence + "/" + row.Cap);
        SetNamedText(go.transform, "Seed", row.SeedTitle);
        Button button = go.GetComponent<Button>();
        if (button != null && go.GetComponent<UIClickSound>() == null)
        {
            go.AddComponent<UIClickSound>();
        }
        return button;
    }

    public static Button SpawnDistrictRow(MapStatsWindowView view, MapStatsDistrictRow row)
    {
        GameObject go = Object.Instantiate(view.DistrictRowTemplate, view.Content);
        go.SetActive(true);
        SetNamedText(go.transform, "Name", row.DisplayName);
        SetNamedText(go.transform, "Control",
            row.ControlLabel + "  ·  " + row.ControlledCount + "/" + row.ZoneCount + " zonas · seeds " + row.OccupiedCount);
        Button button = go.GetComponent<Button>();
        if (button != null && go.GetComponent<UIClickSound>() == null)
        {
            go.AddComponent<UIClickSound>();
        }
        return button;
    }

    public static Button SpawnActionButton(MapStatsWindowView view, string label)
    {
        GameObject go = Object.Instantiate(view.ButtonRowTemplate, view.Content);
        go.SetActive(true);
        SetNamedText(go.transform, "Label", label);
        Button button = go.GetComponent<Button>();
        if (button != null && go.GetComponent<UIClickSound>() == null)
        {
            go.AddComponent<UIClickSound>();
        }
        return button;
    }

    private static GameObject BuildFactionRowTemplate(Transform parent)
    {
        GameObject go = CreateUiObject("FactionRow", parent);
        AddRowLayout(go, 32f);
        AddImage(go, RowColor, false);
        HorizontalLayoutGroup row = go.AddComponent<HorizontalLayoutGroup>();
        row.padding = new RectOffset(8, 8, 4, 4);
        row.spacing = 8f;
        row.childAlignment = TextAnchor.MiddleLeft;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = true;

        GameObject swatch = CreateUiObject("Swatch", go.transform);
        LayoutElement swatchLe = swatch.AddComponent<LayoutElement>();
        swatchLe.minWidth = 12f;
        swatchLe.preferredWidth = 12f;
        swatchLe.flexibleWidth = 0f;
        AddImage(swatch, Color.white, false);

        GameObject nameGo = CreateUiObject("Name", go.transform);
        LayoutElement nameLe = nameGo.AddComponent<LayoutElement>();
        nameLe.preferredWidth = 140f;
        nameLe.flexibleWidth = 0.6f;
        AddTmp(nameGo, 15, TextAlignmentOptions.MidlineLeft);

        GameObject valueGo = CreateUiObject("Value", go.transform);
        LayoutElement valueLe = valueGo.AddComponent<LayoutElement>();
        valueLe.flexibleWidth = 1f;
        AddTmp(valueGo, 14, TextAlignmentOptions.MidlineRight);
        return go;
    }

    private static GameObject BuildSeedRowTemplate(Transform parent)
    {
        GameObject go = CreateUiObject("SeedRow", parent);
        AddRowLayout(go, 48f);
        AddImage(go, RowColor, false);
        VerticalLayoutGroup col = go.AddComponent<VerticalLayoutGroup>();
        col.padding = new RectOffset(8, 8, 4, 4);
        col.spacing = 0f;
        col.childAlignment = TextAnchor.UpperLeft;
        col.childControlWidth = true;
        col.childControlHeight = true;
        col.childForceExpandWidth = true;
        col.childForceExpandHeight = false;

        GameObject titleGo = CreateUiObject("Title", go.transform);
        titleGo.AddComponent<LayoutElement>().preferredHeight = 20f;
        AddTmp(titleGo, 15, TextAlignmentOptions.MidlineLeft);

        GameObject metaGo = CreateUiObject("Meta", go.transform);
        metaGo.AddComponent<LayoutElement>().preferredHeight = 18f;
        TextMeshProUGUI meta = AddTmp(metaGo, 13, TextAlignmentOptions.MidlineLeft);
        meta.color = new Color(0.75f, 0.76f, 0.8f);
        return go;
    }

    private static GameObject BuildNodeRowTemplate(Transform parent)
    {
        GameObject go = CreateUiObject("NodeRow", parent);
        AddRowLayout(go, 52f);
        AddImage(go, RowColor, true);
        go.AddComponent<Button>();
        VerticalLayoutGroup col = go.AddComponent<VerticalLayoutGroup>();
        col.padding = new RectOffset(8, 8, 4, 4);
        col.spacing = 0f;
        col.childControlWidth = true;
        col.childControlHeight = true;
        col.childForceExpandWidth = true;
        col.childForceExpandHeight = false;

        GameObject nameGo = CreateUiObject("Name", go.transform);
        nameGo.AddComponent<LayoutElement>().preferredHeight = 18f;
        AddTmp(nameGo, 15, TextAlignmentOptions.MidlineLeft);

        GameObject controlGo = CreateUiObject("Control", go.transform);
        controlGo.AddComponent<LayoutElement>().preferredHeight = 14f;
        AddTmp(controlGo, 13, TextAlignmentOptions.MidlineLeft);

        GameObject seedGo = CreateUiObject("Seed", go.transform);
        seedGo.AddComponent<LayoutElement>().preferredHeight = 14f;
        TextMeshProUGUI seed = AddTmp(seedGo, 13, TextAlignmentOptions.MidlineLeft);
        seed.color = new Color(0.75f, 0.76f, 0.8f);
        return go;
    }

    private static GameObject BuildDistrictRowTemplate(Transform parent)
    {
        GameObject go = CreateUiObject("DistrictRow", parent);
        AddRowLayout(go, 44f);
        AddImage(go, RowColor, true);
        go.AddComponent<Button>();
        VerticalLayoutGroup col = go.AddComponent<VerticalLayoutGroup>();
        col.padding = new RectOffset(8, 8, 4, 4);
        col.spacing = 0f;
        col.childControlWidth = true;
        col.childControlHeight = true;
        col.childForceExpandWidth = true;
        col.childForceExpandHeight = false;

        GameObject nameGo = CreateUiObject("Name", go.transform);
        nameGo.AddComponent<LayoutElement>().preferredHeight = 18f;
        AddTmp(nameGo, 15, TextAlignmentOptions.MidlineLeft);

        GameObject controlGo = CreateUiObject("Control", go.transform);
        controlGo.AddComponent<LayoutElement>().preferredHeight = 16f;
        AddTmp(controlGo, 13, TextAlignmentOptions.MidlineLeft);
        return go;
    }

    private static GameObject BuildSectionLabelTemplate(Transform parent)
    {
        GameObject go = CreateUiObject("SectionLabel", parent);
        AddRowLayout(go, 24f);
        TextMeshProUGUI tmp = AddTmp(go, 16, TextAlignmentOptions.BottomLeft);
        tmp.fontStyle = FontStyles.Bold;
        return go;
    }

    private static GameObject BuildBodyTextTemplate(Transform parent)
    {
        GameObject go = CreateUiObject("BodyText", parent);
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.minHeight = 18f;
        le.preferredHeight = 18f;
        le.flexibleHeight = 0f;
        TextMeshProUGUI tmp = AddTmp(go, 15, TextAlignmentOptions.TopLeft);
        tmp.textWrappingMode = TextWrappingModes.Normal;
        ContentSizeFitter fitter = go.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return go;
    }

    private static GameObject BuildButtonRowTemplate(Transform parent)
    {
        GameObject go = CreateUiObject("ButtonRow", parent);
        AddRowLayout(go, 36f);
        AddImage(go, ButtonColor, true);
        go.AddComponent<Button>();
        GameObject labelGo = CreateUiObject("Label", go.transform);
        Stretch(labelGo.GetComponent<RectTransform>());
        AddTmp(labelGo, 14, TextAlignmentOptions.Center).text = "Acción";
        return go;
    }

    private static void AddRowLayout(GameObject go, float height)
    {
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.minHeight = height;
        le.preferredHeight = height;
        le.flexibleHeight = 0f;
    }

    private static void Register(UIWindow window, GameObject go, string name, Component element)
    {
        UIElement ui = go.GetComponent<UIElement>();
        if (ui == null) ui = go.AddComponent<UIElement>();
        ui.Bind(window, name, element);
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = parent != null ? parent.gameObject.layer : 5;
        go.transform.SetParent(parent, false);
        return go;
    }

    private static RectTransform Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        return rt;
    }

    private static RectTransform StretchTop(RectTransform rt, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, height);
        return rt;
    }

    private static Image AddImage(GameObject go, Color color, bool raycast)
    {
        Image image = go.GetComponent<Image>();
        if (image == null) image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = raycast;
        return image;
    }

    private static TextMeshProUGUI AddTmp(GameObject go, float size, TextAlignmentOptions align)
    {
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        if (tmp == null) tmp = go.AddComponent<TextMeshProUGUI>();
        ApplyReadableFont(tmp);
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = BodyTextColor;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.text = string.Empty;
        return tmp;
    }

    private static void ApplyReadableFont(GameObject root)
    {
        if (root == null) return;
        TextMeshProUGUI[] labels = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            ApplyReadableFont(labels[i]);
        }
    }

    private static void ApplyReadableFont(TextMeshProUGUI tmp)
    {
        if (tmp == null) return;

        TMP_FontAsset font = ResolveUiFont();
        if (font != null)
        {
            tmp.font = font;
            if (font.material != null) tmp.fontSharedMaterial = font.material;
        }

        tmp.extraPadding = true;
        tmp.characterSpacing = 2f;
        tmp.lineSpacing = 8f;
    }

    private static TMP_FontAsset ResolveUiFont()
    {
        if (uiFont != null) return uiFont;
        uiFont = Resources.Load<TMP_FontAsset>(UiFontResourcePath);
        return uiFont;
    }

    private static Transform FindChild(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChild(root.GetChild(i), name);
            if (found != null) return found;
        }

        return null;
    }

    private static TextMeshProUGUI FindTmp(Transform root, string name)
    {
        Transform child = FindChild(root, name);
        return child != null ? child.GetComponent<TextMeshProUGUI>() : null;
    }

    private static void SetNamedText(Transform root, string name, string text)
    {
        Transform child = FindChild(root, name);
        if (child == null) return;
        TextMeshProUGUI tmp = child.GetComponent<TextMeshProUGUI>();
        if (tmp != null) tmp.text = text ?? string.Empty;
    }
}
