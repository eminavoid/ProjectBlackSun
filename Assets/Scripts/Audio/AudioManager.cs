using UnityEngine;
using UnityEngine.UI;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Tooltip("Persistent GameObject with an AkGameObj component. All UI sounds post through this emitter.")]
    public GameObject UIEmitter;

    [Header("Wwise Event Names")]
    [SerializeField] private string uiClickGenericEvent = "Play_UI_Click_Generic";
    [SerializeField] private string resourceIconClickEvent = "Play_UI_ResourceIcon";
    [SerializeField] private string eventPopupEvent = "Play_UI_EventPopup";
    [SerializeField] private string menuOpenEvent = "Play_UI_MenuOpen";
    [SerializeField] private string seedPlantEvent = "Play_UI_SeedPlant";

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


    public void PlayEventPopup()
    {
        if (UIEmitter == null)
        {
            Debug.LogWarning("AudioManager: UIEmitter is not assigned. Assign the UI_AudioEmitter GameObject in the Inspector.");
            return;
        }
        AkSoundEngine.PostEvent(eventPopupEvent, UIEmitter);
    }

    public void PlayMenuOpen()
    {
        if (UIEmitter == null)
        {
            Debug.LogWarning("AudioManager: UIEmitter is not assigned. Assign the UI_AudioEmitter GameObject in the Inspector.");
            return;
        }
        AkSoundEngine.PostEvent(menuOpenEvent, UIEmitter);
    }
    
    public void PlaySeedPlant()
    {
        if (UIEmitter == null)
        {
            Debug.LogWarning("AudioManager: UIEmitter is not assigned. Assign the UI_AudioEmitter GameObject in the Inspector.");
            return;
        }
        AkSoundEngine.PostEvent(seedPlantEvent, UIEmitter);
    }
}
