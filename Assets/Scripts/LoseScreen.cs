using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoseScreen : MonoBehaviour
{
    [SerializeField] private GameObject window;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private TextMeshProUGUI turnsText;
    [SerializeField] private string mainMenuScene = "MainMenu";
    [SerializeField] private string revealEvent = "Play_UI_Outcome";

    private void Awake()
    {
        window.SetActive(false);
        NodeTooltipMenuBlock.Ensure(window);
    }

    private void OnEnable() => GlobalEventBus.Subscribe<LoseEvent>(Show);

    private void OnDisable() => GlobalEventBus.Unsubscribe<LoseEvent>(Show);

    public void TryAgain() => Leave(SceneManager.GetActiveScene().name);

    public void ReturnToMainMenu() => Leave(mainMenuScene);

    private void Show(LoseEvent loseEvent)
    {
        messageText.text = loseEvent.message;
        turnsText.text = loseEvent.turnsSurvived == 1 ? "Your cult endured a single turn." : $"Your cult endured {loseEvent.turnsSurvived} turns.";
        window.SetActive(true);
        group.alpha = 0f;
        group.DOFade(1f, 0.8f).SetUpdate(true).SetLink(gameObject);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayUIEvent(revealEvent);
    }

    private void Leave(string scene)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.StopBackgroundMusic(500);
        SceneManager.LoadScene(scene);
    }
}
