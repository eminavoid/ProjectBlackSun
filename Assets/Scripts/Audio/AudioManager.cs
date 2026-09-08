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
    [SerializeField] private string districtClickEvent = "Play_World_DistrictClick";
    [SerializeField] private string cardClickEvent = "Play_UI_Click_Card";
    [SerializeField] private string backgroundMusicEvent = "Play_MUS_Background";

    [Header("Wwise RTPC Names")]
    [SerializeField] private string musicVolumeRtpc = "Music_Volume";
    [SerializeField] private string sfxVolumeRtpc = "SFX_Volume";

    [Header("PlayerPrefs Keys")]
    private const string MusicVolumePrefKey = "Settings_MusicVolume";
    private const string SfxVolumePrefKey = "Settings_SFXVolume";

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
        LoadSavedVolumes();
    }

    private void Start()
    {
        PlayBackgroundMusic();
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
    
    public void PlayDistrictClick()
    {
        if (UIEmitter == null)
        {
            Debug.LogWarning("AudioManager: UIEmitter is not assigned. Assign the UI_AudioEmitter GameObject in the Inspector.");
            return;
        }
        AkSoundEngine.PostEvent(districtClickEvent, UIEmitter);
    }

    public void PlayCardClick()
    {
        if (UIEmitter == null)
        {
            Debug.LogWarning("AudioManager: UIEmitter is not assigned. Assign the UI_AudioEmitter GameObject in the Inspector.");
            return;
        }
        AkSoundEngine.PostEvent(cardClickEvent, UIEmitter);
    }

    public void PlayBackgroundMusic()
    {
        if (UIEmitter == null)
        {
            Debug.LogWarning("AudioManager: UIEmitter is not assigned. Assign the UI_AudioEmitter GameObject in the Inspector.");
            return;
        }
        AkSoundEngine.PostEvent(backgroundMusicEvent, UIEmitter);
    }

    public void SetMusicVolume(float value)
    {
        AkSoundEngine.SetRTPCValue(musicVolumeRtpc, value);
        PlayerPrefs.SetFloat(MusicVolumePrefKey, value);
        PlayerPrefs.Save();
    }

    public void SetSFXVolume(float value)
    {
        AkSoundEngine.SetRTPCValue(sfxVolumeRtpc, value);
        PlayerPrefs.SetFloat(SfxVolumePrefKey, value);
        PlayerPrefs.Save();
    }

    public float GetSavedMusicVolume()
    {
        return PlayerPrefs.GetFloat(MusicVolumePrefKey, 100f);
    }

    public float GetSavedSFXVolume()
    {
        return PlayerPrefs.GetFloat(SfxVolumePrefKey, 100f);
    }

    private void LoadSavedVolumes()
    {
        AkSoundEngine.SetRTPCValue(musicVolumeRtpc, GetSavedMusicVolume());
        AkSoundEngine.SetRTPCValue(sfxVolumeRtpc, GetSavedSFXVolume());
    }
}
