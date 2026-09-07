using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the Config Menu's Music and SFX sliders. On enable, initializes
/// each slider to the currently saved volume (without re-triggering a save),
/// then listens for user changes and forwards them to AudioManager, which
/// applies the Wwise RTPC and persists the value to PlayerPrefs.
/// </summary>
public class ConfigMenuController : MonoBehaviour
{
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    private void OnEnable()
    {
        if (AudioManager.Instance == null) return;

        if (musicSlider != null)
        {
            musicSlider.SetValueWithoutNotify(AudioManager.Instance.GetSavedMusicVolume());
        }
        if (sfxSlider != null)
        {
            sfxSlider.SetValueWithoutNotify(AudioManager.Instance.GetSavedSFXVolume());
        }
    }

    private void Awake()
    {
        if (musicSlider != null)
        {
            musicSlider.onValueChanged.AddListener(OnMusicSliderChanged);
        }
        if (sfxSlider != null)
        {
            sfxSlider.onValueChanged.AddListener(OnSFXSliderChanged);
        }
    }

    private void OnMusicSliderChanged(float value)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMusicVolume(value);
        }
    }

    private void OnSFXSliderChanged(float value)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetSFXVolume(value);
        }
    }
}
