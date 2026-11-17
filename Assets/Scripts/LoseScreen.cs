using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class LoseScreen : MonoBehaviour
{
    [SerializeField] private GameObject window;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private TextMeshProUGUI turnsText;
    [SerializeField] private TextMeshProUGUI againLabel;
    [SerializeField] private string mainMenuScene = "MainMenu";
    [SerializeField] private string revealEvent = "Play_UI_Outcome";
    [SerializeField] private ScreenFade fade;

    [Header("Victory")]
    [SerializeField] private Color victoryTitleColor = new Color(0.62f, 0.42f, 0.06f, 1f);
    [SerializeField] private string victoryAgainText = "PLAY AGAIN";

    [Header("Reveal")]
    [SerializeField, Min(0.1f)] private float pace = 1f;
    [SerializeField] private float standingsY = -92f;
    [SerializeField] private float standingsSize = 24f;

    private string defeatTitle;
    private Color defeatTitleColor;
    private string defeatAgainText;

    private RectTransform sheet;
    private RectTransform divider;
    private RectTransform seal;
    private UnityEngine.UI.Image sealImage;
    private TextMeshProUGUI standings;
    private readonly List<CanvasGroup> buttons = new List<CanvasGroup>();
    private float dividerWidth;
    private Sequence reveal;

    private void Awake()
    {
        window.SetActive(false);
        NodeTooltipMenuBlock.Ensure(window);
        defeatTitle = titleText.text;
        defeatTitleColor = titleText.color;
        defeatAgainText = againLabel.text;

        sheet = (RectTransform)titleText.transform.parent;
        divider = sheet.Find("Divider") as RectTransform;
        seal = sheet.Find("Seal") as RectTransform;
        if (seal != null) sealImage = seal.GetComponent<UnityEngine.UI.Image>();
        if (divider != null) dividerWidth = divider.sizeDelta.x;

        foreach (UnityEngine.UI.Button button in sheet.GetComponentsInChildren<UnityEngine.UI.Button>(true))
        {
            CanvasGroup buttonGroup = button.GetComponent<CanvasGroup>();
            if (buttonGroup == null) buttonGroup = button.gameObject.AddComponent<CanvasGroup>();
            buttons.Add(buttonGroup);
        }

        standings = Instantiate(turnsText, sheet);
        standings.name = "Standings";
        standings.rectTransform.anchoredPosition = new Vector2(0f, standingsY);
        standings.rectTransform.sizeDelta = new Vector2(1000f, standingsSize * 1.8f);
        standings.fontSize = standingsSize;
        standings.richText = true;
    }

    private void OnEnable()
    {
        GlobalEventBus.Subscribe<LoseEvent>(Show);
        GlobalEventBus.Subscribe<VictoryEvent>(Show);
    }

    private void OnDisable()
    {
        GlobalEventBus.Unsubscribe<LoseEvent>(Show);
        GlobalEventBus.Unsubscribe<VictoryEvent>(Show);
    }

    private void Update()
    {
        if (reveal == null || !reveal.IsActive() || !reveal.IsPlaying() || !SkipPressed()) return;
        reveal.Complete(true);
    }

    public void TryAgain() => Leave(SceneManager.GetActiveScene().name);

    public void ReturnToMainMenu() => Leave(mainMenuScene);

    private void Show(LoseEvent loseEvent)
    {
        string turns = loseEvent.turnsSurvived == 1 ? "Your cult endured a single turn." : $"Your cult endured {loseEvent.turnsSurvived} turns.";
        Reveal(loseEvent.title ?? defeatTitle, defeatTitleColor, loseEvent.message, loseEvent.detail ?? turns, defeatAgainText, false);
    }

    private void Show(VictoryEvent victoryEvent)
    {
        Reveal(victoryEvent.title, victoryTitleColor, victoryEvent.message, victoryEvent.detail, victoryAgainText, true);
    }

    private void Reveal(string title, Color titleColor, string message, string detail, string againText, bool victory)
    {
        titleText.text = title;
        titleText.color = titleColor;
        messageText.text = message;
        turnsText.text = detail;
        againLabel.text = againText;
        standings.text = Standings();

        TopScreenNotification.Release();
        InputLocks.Lock(true, true, true);
        transform.SetAsLastSibling();
        window.SetActive(true);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayUIEvent(revealEvent);

        PrepareReveal();
        reveal = BuildReveal(victory);
    }

    private void PrepareReveal()
    {
        reveal?.Kill(true);
        group.alpha = 0f;
        titleText.alpha = 0f;
        titleText.rectTransform.localScale = Vector3.one * 1.6f;
        messageText.alpha = 0f;
        turnsText.alpha = 0f;
        standings.alpha = 0f;
        if (divider != null) divider.sizeDelta = new Vector2(0f, divider.sizeDelta.y);
        if (seal != null) seal.localScale = Vector3.one * 2.4f;
        if (sealImage != null) sealImage.color = new Color(sealImage.color.r, sealImage.color.g, sealImage.color.b, 0f);

        foreach (CanvasGroup button in buttons)
        {
            button.alpha = 0f;
            button.interactable = false;
            button.blocksRaycasts = false;
        }
    }

    private Sequence BuildReveal(bool victory)
    {
        RectTransform titleRect = titleText.rectTransform;
        Sequence sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);

        sequence.Append(group.DOFade(1f, 0.6f * pace));
        sequence.Append(Fade(titleText, 0.45f));
        sequence.Join(titleRect.DOScale(1f, 0.6f * pace).SetEase(Ease.OutBack));
        if (!victory) sequence.Append(titleRect.DOShakeAnchorPos(0.4f * pace, 10f, 22, 90f, false, true));
        if (divider != null) sequence.Append(divider.DOSizeDelta(new Vector2(dividerWidth, divider.sizeDelta.y), 0.4f * pace).SetEase(Ease.OutCubic));
        sequence.Append(Fade(messageText, 0.5f));
        sequence.Append(Fade(turnsText, 0.35f));
        sequence.Append(Fade(standings, 0.35f));

        if (seal != null)
        {
            sequence.Append(seal.DOScale(1f, 0.22f * pace).SetEase(Ease.InCubic));
            if (sealImage != null) sequence.Join(sealImage.DOFade(1f, 0.12f * pace));
            sequence.Append(sheet.DOShakeAnchorPos(0.25f * pace, 7f, 25, 90f, false, true));
            sequence.InsertCallback(sequence.Duration(false) - 0.25f * pace, () =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayOutcomeResources(victory, !victory);
            });
        }

        foreach (CanvasGroup button in buttons)
        {
            sequence.Join(button.DOFade(1f, 0.35f * pace));
        }

        sequence.OnComplete(() =>
        {
            foreach (CanvasGroup button in buttons)
            {
                button.interactable = true;
                button.blocksRaycasts = true;
            }

            if (victory) titleRect.DOScale(1.04f, 1.4f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(titleRect.gameObject);
        });

        return sequence;
    }

    private Tween Fade(TMP_Text text, float duration)
    {
        return DOTween.To(() => text.alpha, value => text.alpha = value, 1f, duration * pace);
    }

    private static string Standings()
    {
        var rows = new List<(FactionId faction, int zones)>();
        foreach (FactionId faction in FactionIdUtil.All)
        {
            if (!FactionIdUtil.IsPlayer(faction) && !RivalAIDirector.IsActiveRival(faction)) continue;
            rows.Add((faction, FaithEclipseTracker.GetControlledZoneCount(faction)));
        }

        rows.Sort((a, b) => b.zones.CompareTo(a.zones));
        var parts = new List<string>();
        foreach ((FactionId faction, int zones) in rows)
        {
            Color ink = Color.Lerp(TerritoryView.MapColor(faction), Color.black, 0.35f);
            parts.Add($"<b><color=#{ColorUtility.ToHtmlStringRGB(ink)}>{FactionIdUtil.DisplayName(faction)}</color></b> {zones}");
        }

        return "Zones ruled:   " + string.Join("   ·   ", parts);
    }

    private static bool SkipPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            || (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame);
#else
        return Input.anyKeyDown;
#endif
    }

    private void Leave(string scene)
    {
        InputLocks.Clear();
        if (AudioManager.Instance != null) AudioManager.Instance.StopBackgroundMusic(800);
        fade.LoadScene(scene);
    }
}
