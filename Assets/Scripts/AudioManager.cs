using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Central audio hub for the game. Handles Wwise bank references (via AkBank
/// components on this same GameObject) and owns the single persistent
/// AkGameObj ("UIEmitter") that ALL UI sounds are posted through.
///
/// Posting every UI sound from one persistent emitter (instead of each
/// individual button) avoids "Unknown/Dead game object ID" and
/// "Voice Starvation" errors when buttons/panels get destroyed or disabled
/// right after being clicked.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Tooltip("Persistent GameObject with an AkGameObj component. All UI sounds post through this emitter.")]
    public GameObject UIEmitter;

    [Header("Wwise Event Names")]
    [SerializeField] private string uiClickGenericEvent = "Play_UI_Click_Generic";
    [SerializeField] private string resourceIconClickEvent = "Play_UI_ResourceIcon";

    [Header("Wwise Switch Group")]
    [SerializeField] private string resourceTypeSwitchGroup = "ResourceType";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        HookExistingButtons();
    }

    private void HookExistingButtons()
    {
        Button[] allButtons = FindObjectsOfType<Button>(true);
        foreach (Button btn in allButtons)
        {
            HookButton(btn);
        }
    }

    public void HookButton(Button btn)
    {
        btn.onClick.RemoveListener(PlayUIClick);
        btn.onClick.AddListener(PlayUIClick);
    }

    public void PlayUIClick()
    {
        if (UIEmitter == null)
        {
            Debug.LogWarning("AudioManager: UIEmitter is not assigned. Assign the UI_AudioEmitter GameObject in the Inspector.");
            return;
        }
        AkSoundEngine.PostEvent(uiClickGenericEvent, UIEmitter);
    }

    /// <summary>
    /// Plays the resource icon click sound, choosing the correct sample via
    /// the "ResourceType" Switch Group in Wwise. resourceType must match one
    /// of the Switches defined in that group exactly (e.g. "Gold", "Materials",
    /// "Flock", "Faith", "Secrets", "Authority", "Bliss").
    /// </summary>
    public void PlayResourceIconClick(string resourceType)
    {
        if (UIEmitter == null)
        {
            Debug.LogWarning("AudioManager: UIEmitter is not assigned. Assign the UI_AudioEmitter GameObject in the Inspector.");
            return;
        }
        AkSoundEngine.SetSwitch(resourceTypeSwitchGroup, resourceType, UIEmitter);
        AkSoundEngine.PostEvent(resourceIconClickEvent, UIEmitter);
    }
}
