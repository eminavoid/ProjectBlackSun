using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Pregunta de primera vez y los tres avisos de mundo, victoria e identidad.
/// </summary>
public class FirstTimePrompt : MonoBehaviour
{
    private const float ColumnX = 205f;
    private const float DescriptionY = -500f;
    private const float ActionY = -680f;
    private const float ActionStep = 92f;

    private static readonly Vector2 MenuSize = new Vector2(430f, 61f);
    private static readonly string[] IntroLines =
    {
        "The city is divided among cults. The map is the territory of faith.",
        "The Faith Eclipse is won by the cult that controls the most zones. Your cult falls apart if your Flock runs out.",
        "You are the voice of a new cult. Crimson Choir, Rose Ledger, and Ochre Flock are already moving."
    };

    private MainMenu menu;
    private DifficultySelectMenu difficulty;
    private GameObject template;
    private TextMeshProUGUI style;
    private CanvasGroup rootGroup;
    private TextMeshProUGUI body;
    private GameObject questionButtons;
    private GameObject introButtons;
    private int introIndex = -1;

    public void Initialize(MainMenu owner, DifficultySelectMenu difficultyMenu, GameObject buttonTemplate, Transform curtain)
    {
        menu = owner;
        difficulty = difficultyMenu;
        template = buttonTemplate;
        style = buttonTemplate.GetComponentInChildren<TextMeshProUGUI>(true);

        Transform canvas = buttonTemplate.transform.parent;
        rootGroup = MakeGroup("First Time", canvas);
        rootGroup.transform.SetSiblingIndex(curtain.GetSiblingIndex());

        body = MakeText(
            "Is this your first time playing Faith Eclipse?",
            new Vector2(ColumnX, DescriptionY),
            new Vector2(560f, 140f),
            28f,
            rootGroup.transform);

        questionButtons = new GameObject("Question", typeof(RectTransform));
        questionButtons.transform.SetParent(rootGroup.transform, false);
        Stretch((RectTransform)questionButtons.transform);
        Spawn("YES", new Vector2(ColumnX, ActionY), questionButtons.transform, OnYes);
        Spawn("NO", new Vector2(ColumnX, ActionY - ActionStep), questionButtons.transform, OnNo);
        Spawn("BACK", new Vector2(ColumnX, ActionY - ActionStep * 2f), questionButtons.transform, OnBack);

        introButtons = new GameObject("Intro", typeof(RectTransform));
        introButtons.transform.SetParent(rootGroup.transform, false);
        Stretch((RectTransform)introButtons.transform);
        Spawn("CONTINUE", new Vector2(ColumnX, ActionY), introButtons.transform, OnContinue);
        Spawn("SKIP", new Vector2(ColumnX, ActionY - ActionStep), introButtons.transform, OnSkipIntro);
        introButtons.SetActive(false);

        rootGroup.alpha = 0f;
        rootGroup.blocksRaycasts = false;
        rootGroup.interactable = false;
        rootGroup.gameObject.SetActive(false);
    }

    public void Open()
    {
        introIndex = -1;
        body.text = "Is this your first time playing Faith Eclipse?";
        questionButtons.SetActive(true);
        introButtons.SetActive(false);
        menu.HideTitleButtons();
        Fade(rootGroup, true);
    }

    private void OnYes()
    {
        TutorialSession.BeginGuide();
        introIndex = 0;
        questionButtons.SetActive(false);
        introButtons.SetActive(true);
        body.text = IntroLines[0];
    }

    private void OnNo()
    {
        TutorialSession.BeginFree();
        LeaveForDifficulty();
    }

    private void OnBack()
    {
        Fade(rootGroup, false);
        menu.ShowTitleButtons();
    }

    private void OnContinue()
    {
        introIndex++;
        if (introIndex >= IntroLines.Length)
        {
            LeaveForDifficulty();
            return;
        }

        body.text = IntroLines[introIndex];
    }

    private void OnSkipIntro()
    {
        LeaveForDifficulty();
    }

    private void LeaveForDifficulty()
    {
        rootGroup.DOKill();
        rootGroup.interactable = false;
        rootGroup.blocksRaycasts = false;
        rootGroup.gameObject.SetActive(false);
        difficulty.OpenFromTitle();
    }

    private void Spawn(string caption, Vector2 position, Transform parent, UnityAction onClick)
    {
        GameObject clone = Instantiate(template, parent);
        clone.name = caption;

        RectTransform rect = (RectTransform)clone.transform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = MenuSize;

        TextMeshProUGUI label = clone.GetComponentInChildren<TextMeshProUGUI>(true);
        label.text = caption;

        Button button = clone.GetComponent<Button>();
        int persistent = button.onClick.GetPersistentEventCount();
        for (int i = 0; i < persistent; i++)
            button.onClick.SetPersistentListenerState(i, UnityEventCallState.Off);
        button.onClick.AddListener(onClick);
    }

    private TextMeshProUGUI MakeText(string value, Vector2 position, Vector2 size, float fontSize, Transform parent)
    {
        GameObject go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.font = style.font;
        text.fontSharedMaterial = style.fontSharedMaterial;
        text.color = new Color(0.93f, 0.88f, 0.8f, 1f);
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        text.text = value;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
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
