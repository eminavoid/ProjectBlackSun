using TMPro;
using UnityEngine;

public class LoseScreen : MonoBehaviour
{
    [SerializeField] private GameObject window;
    [SerializeField] private TextMeshProUGUI messageText;

    private void Awake()
    {
        GlobalEventBus.Subscribe<LoseEvent>(ShowLoseScreen);
        if (window == null) return;
        window.SetActive(false);
        NodeTooltipMenuBlock.Ensure(window);
    }

    private void ShowLoseScreen(LoseEvent loseEvent)
    {
        messageText.text = loseEvent.message;
        window.SetActive(true);
    }
}