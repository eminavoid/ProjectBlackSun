using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

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

    private string defeatTitle;
    private Color defeatTitleColor;
    private string defeatAgainText;

    private void Awake()
    {
        window.SetActive(false);
        NodeTooltipMenuBlock.Ensure(window);
        defeatTitle = titleText.text;
        defeatTitleColor = titleText.color;
        defeatAgainText = againLabel.text;
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

    public void TryAgain() => Leave(SceneManager.GetActiveScene().name);

    public void ReturnToMainMenu() => Leave(mainMenuScene);

    private void Show(LoseEvent loseEvent)
    {
        string turns = loseEvent.turnsSurvived == 1 ? "Your cult endured a single turn." : $"Your cult endured {loseEvent.turnsSurvived} turns.";
        Reveal(loseEvent.title ?? defeatTitle, defeatTitleColor, loseEvent.message, loseEvent.detail ?? turns, defeatAgainText);
    }

    private void Show(VictoryEvent victoryEvent)
    {
        Reveal(victoryEvent.title, victoryTitleColor, victoryEvent.message, victoryEvent.detail, victoryAgainText);
    }

    private void Reveal(string title, Color titleColor, string message, string detail, string againText)
    {
        titleText.text = title;
        titleText.color = titleColor;
        messageText.text = message;
        turnsText.text = detail;
        againLabel.text = againText;

        transform.SetAsLastSibling();
        window.SetActive(true);
        group.alpha = 0f;
        group.DOFade(1f, 0.8f).SetUpdate(true).SetLink(gameObject);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayUIEvent(revealEvent);
    }

    private void Leave(string scene)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.StopBackgroundMusic(800);
        fade.LoadScene(scene);
    }
}
