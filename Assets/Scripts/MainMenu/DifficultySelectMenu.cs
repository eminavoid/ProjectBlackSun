using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Dificultad entre New Game y la escena de juego.
/// A la izquierda se confirma. A la derecha se elige.
/// </summary>
public class DifficultySelectMenu : MonoBehaviour
{
    private enum PresetChoice
    {
        Easy,
        Normal,
        Hard,
        Custom
    }

    private const float ColumnX = 205f;
    private const float RightX = 780f;
    private const float DescriptionY = -500f;
    private const float ActionY = -680f;
    private const float ActionStep = 92f;
    private const float OptionY = -584f;
    private const float OptionStep = 84f;
    private const float HeaderY = -578f;
    private const float MatrixY = -660f;
    private const float MatrixStep = 112f;
    private const float CellX = 1050f;
    private const float CellStep = 164f;

    private static readonly Vector2 MenuSize = new Vector2(430f, 61f);
    private static readonly Vector2 CellSize = new Vector2(156f, 52f);
    private static readonly string[] PresetLabels = { "EASY", "NORMAL", "HARD" };

    private MainMenu menu;
    private GameObject template;
    private TextMeshProUGUI style;
    private TextMeshProUGUI description;
    private TextMeshProUGUI guideNote;
    private CanvasGroup rootGroup;
    private CanvasGroup presetGroup;
    private CanvasGroup matrixGroup;
    private readonly MainMenuButton[] presetButtons = new MainMenuButton[4];
    private readonly MainMenuButton[,] cells = new MainMenuButton[3, 3];
    private readonly RivalDifficulty[] picked =
    {
        RivalDifficulty.Normal,
        RivalDifficulty.Normal,
        RivalDifficulty.Normal
    };

    private PresetChoice highlighted = PresetChoice.Normal;
    private bool matrixOpen;

    public void Initialize(MainMenu owner, GameObject buttonTemplate, Transform curtain)
    {
        menu = owner;
        template = buttonTemplate;
        style = buttonTemplate.GetComponentInChildren<TextMeshProUGUI>(true);

        Transform canvas = buttonTemplate.transform.parent;
        rootGroup = MakeGroup("Difficulty", canvas);
        rootGroup.transform.SetSiblingIndex(curtain.GetSiblingIndex());
        presetGroup = MakeGroup("Presets", rootGroup.transform);
        matrixGroup = MakeGroup("Custom Matrix", rootGroup.transform);

        description = MakeText(
            Blurb(PresetChoice.Normal),
            new Vector2(ColumnX, DescriptionY),
            new Vector2(460f, 72f),
            22f,
            TextAlignmentOptions.TopLeft,
            1f,
            rootGroup.transform,
            true);
        Spawn("BEGIN", new Vector2(ColumnX, ActionY), MenuSize, false, rootGroup.transform, OnBegin);
        Spawn("BACK", new Vector2(ColumnX, ActionY - ActionStep), MenuSize, false, rootGroup.transform, OnBack);

        BuildPresets();
        BuildMatrix();
        Highlight(PresetChoice.Normal);

        matrixGroup.gameObject.SetActive(false);
        rootGroup.alpha = 0f;
        rootGroup.blocksRaycasts = false;
        rootGroup.interactable = false;
        rootGroup.gameObject.SetActive(false);
    }

    public void OpenFromTitle()
    {
        matrixOpen = false;
        matrixGroup.gameObject.SetActive(false);
        presetGroup.gameObject.SetActive(true);
        presetGroup.alpha = 1f;
        presetGroup.blocksRaycasts = true;
        presetGroup.interactable = true;
        Highlight(PresetChoice.Normal);
        menu.HideTitleButtons();
        Fade(rootGroup, true);
        EnsureGuideNote();
    }

    public void Dismiss()
    {
        if (rootGroup == null) return;
        rootGroup.interactable = false;
        rootGroup.blocksRaycasts = false;
        if (presetGroup != null) presetGroup.blocksRaycasts = false;
        if (matrixGroup != null) matrixGroup.blocksRaycasts = false;
    }

    private void EnsureGuideNote()
    {
        bool show = TutorialSession.Guide;
        if (!show)
        {
            if (guideNote != null) guideNote.gameObject.SetActive(false);
            return;
        }

        if (guideNote == null)
        {
            guideNote = MakeText(
                "Easy wastes plays. Hard acts more and hunts you. Begin whenever you want.",
                new Vector2(ColumnX, DescriptionY - 96f),
                new Vector2(520f, 72f),
                20f,
                TextAlignmentOptions.TopLeft,
                1f,
                rootGroup.transform,
                true);
        }

        guideNote.gameObject.SetActive(true);
    }

    private void BuildPresets()
    {
        MakeText(
            "DIFFICULTY",
            new Vector2(RightX, DescriptionY),
            new Vector2(430f, 28f),
            18f,
            TextAlignmentOptions.MidlineLeft,
            0.72f,
            presetGroup.transform);

        string[] labels = { "EASY", "NORMAL", "HARD", "CUSTOM" };
        for (int i = 0; i < labels.Length; i++)
        {
            int index = i;
            presetButtons[i] = Spawn(
                labels[i],
                new Vector2(RightX, OptionY - i * OptionStep),
                MenuSize,
                false,
                presetGroup.transform,
                () => OnPreset(index));
        }
    }

    private void BuildMatrix()
    {
        for (int preset = 0; preset < PresetLabels.Length; preset++)
        {
            MakeText(
                PresetLabels[preset],
                new Vector2(CellX + preset * CellStep + CellSize.x * 0.5f, HeaderY),
                new Vector2(CellSize.x, 28f),
                18f,
                TextAlignmentOptions.Center,
                0.72f,
                matrixGroup.transform);
        }

        for (int rival = 0; rival < MatchSettings.Rivals.Length; rival++)
        {
            FactionId faction = MatchSettings.Rivals[rival];
            float rowY = MatrixY - rival * MatrixStep;
            MakeText(
                FactionIdUtil.DisplayName(faction),
                new Vector2(RightX, rowY + 14f),
                new Vector2(250f, 32f),
                26f,
                TextAlignmentOptions.MidlineLeft,
                1f,
                matrixGroup.transform);
            MakeText(
                RivalAIDirector.PersonalityLabel(RivalAIDirector.DefaultPersonality(faction)),
                new Vector2(RightX, rowY - 16f),
                new Vector2(250f, 24f),
                16f,
                TextAlignmentOptions.MidlineLeft,
                0.72f,
                matrixGroup.transform);

            for (int preset = 0; preset < PresetLabels.Length; preset++)
            {
                int rivalIndex = rival;
                int presetIndex = preset;
                cells[rival, preset] = Spawn(
                    PresetLabels[preset],
                    new Vector2(CellX + preset * CellStep, rowY),
                    CellSize,
                    true,
                    matrixGroup.transform,
                    () => OnCell(rivalIndex, presetIndex));
                cells[rival, preset].SetChosen(picked[rival] == (RivalDifficulty)preset);
            }
        }
    }

    private void OnPreset(int index)
    {
        PresetChoice choice = (PresetChoice)index;
        Highlight(choice);
        if (choice == PresetChoice.Custom) ShowMatrix();
    }

    private void OnBegin()
    {
        if (matrixOpen)
        {
            MatchSettings.UsePerRival(picked[0], picked[1], picked[2]);
            menu.BeginGame();
            return;
        }

        if (highlighted == PresetChoice.Custom)
        {
            ShowMatrix();
            return;
        }

        MatchSettings.UseShared(ToDifficulty(highlighted));
        menu.BeginGame();
    }

    private void OnBack()
    {
        if (matrixOpen) BackToPresets();
        else BackToTitle();
    }

    private void OnCell(int rival, int preset)
    {
        picked[rival] = (RivalDifficulty)preset;
        for (int i = 0; i < PresetLabels.Length; i++)
            cells[rival, i].SetChosen(i == preset);
    }

    private void ShowMatrix()
    {
        matrixOpen = true;
        description.text = Blurb(PresetChoice.Custom);
        HideNow(presetGroup);
        Fade(matrixGroup, true);
    }

    private void BackToPresets()
    {
        matrixOpen = false;
        HideNow(matrixGroup);
        Fade(presetGroup, true);
        Highlight(PresetChoice.Custom);
    }

    private void BackToTitle()
    {
        matrixOpen = false;
        Fade(rootGroup, false);
        menu.ShowTitleButtons();
    }

    private void Highlight(PresetChoice choice)
    {
        highlighted = choice;
        for (int i = 0; i < presetButtons.Length; i++)
            presetButtons[i].SetChosen(i == (int)choice);

        if (!matrixOpen) description.text = Blurb(choice);
    }

    private static RivalDifficulty ToDifficulty(PresetChoice choice)
    {
        switch (choice)
        {
            case PresetChoice.Easy: return RivalDifficulty.Easy;
            case PresetChoice.Hard: return RivalDifficulty.Hard;
            default: return RivalDifficulty.Normal;
        }
    }

    private static string Blurb(PresetChoice choice)
    {
        switch (choice)
        {
            case PresetChoice.Easy: return "Fewer actions, timid, often wastes plays.";
            case PresetChoice.Hard: return "More actions, hunts you, no wasted plays.";
            case PresetChoice.Custom: return "Set a preset for each rival.";
            default: return "The current balance.";
        }
    }

    private MainMenuButton Spawn(
        string caption,
        Vector2 position,
        Vector2 size,
        bool compact,
        Transform parent,
        UnityAction onClick)
    {
        GameObject clone = Instantiate(template, parent);
        clone.name = caption;

        RectTransform rect = (RectTransform)clone.transform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        if (compact)
        {
            RectTransform slab = clone.transform.Find("Slab") as RectTransform;
            if (slab != null) slab.sizeDelta = new Vector2(size.x - 16f, size.y);

            RectTransform marker = clone.transform.Find("Marker") as RectTransform;
            if (marker != null)
            {
                marker.sizeDelta = new Vector2(32f, 32f);
                marker.anchoredPosition = new Vector2(0f, 0f);
            }
        }

        TextMeshProUGUI label = clone.GetComponentInChildren<TextMeshProUGUI>(true);
        label.text = caption;
        if (compact)
        {
            label.enableAutoSizing = true;
            label.fontSizeMin = 12f;
            label.fontSizeMax = 20f;
        }

        Button button = clone.GetComponent<Button>();
        int persistent = button.onClick.GetPersistentEventCount();
        for (int i = 0; i < persistent; i++)
            button.onClick.SetPersistentListenerState(i, UnityEventCallState.Off);
        button.onClick.AddListener(onClick);

        return clone.GetComponent<MainMenuButton>();
    }

    private TextMeshProUGUI MakeText(
        string value,
        Vector2 position,
        Vector2 size,
        float fontSize,
        TextAlignmentOptions alignment,
        float alpha,
        Transform parent,
        bool wrap = false)
    {
        GameObject go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = alignment == TextAlignmentOptions.Center ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.font = style.font;
        text.fontSharedMaterial = style.fontSharedMaterial;
        // The button ink is dark because it sits on the slab. This text sits on the shade.
        text.color = new Color(0.93f, 0.88f, 0.8f, alpha);
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.text = value;
        return text;
    }

    private static CanvasGroup MakeGroup(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        CanvasGroup group = go.GetComponent<CanvasGroup>();
        group.alpha = 1f;
        group.blocksRaycasts = true;
        group.interactable = true;
        return group;
    }

    private static void HideNow(CanvasGroup group)
    {
        group.DOKill();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        group.gameObject.SetActive(false);
    }

    private static void Fade(CanvasGroup group, bool show)
    {
        group.DOKill();
        group.gameObject.SetActive(true);
        group.interactable = show;
        group.blocksRaycasts = show;
        group.DOFade(show ? 1f : 0f, show ? 0.35f : 0.22f)
            .SetEase(Ease.OutCubic)
            .SetLink(group.gameObject)
            .OnComplete(() =>
            {
                if (show) return;
                group.blocksRaycasts = false;
                group.gameObject.SetActive(false);
            });
    }
}
