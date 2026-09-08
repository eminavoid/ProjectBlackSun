using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UIClickSound : MonoBehaviour
{
    [Tooltip("Wwise event posted when this button is clicked.")]
    [SerializeField] private string clickEvent = "Play_UI_Click_Generic";

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.RemoveListener(PlayClickSound);
        button.onClick.AddListener(PlayClickSound);
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(PlayClickSound);
        }
    }

    private void PlayClickSound()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayUIEvent(clickEvent);
        }
        else
        {
            Debug.LogWarning("UIClickSound: AudioManager.Instance is null. Make sure AudioManager exists and initializes before this object spawns.");
        }
    }
}
