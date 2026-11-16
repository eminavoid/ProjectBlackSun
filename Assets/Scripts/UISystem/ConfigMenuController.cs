using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ConfigMenuController : MonoBehaviour
{
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Button hintsButton;
    [SerializeField] private TMP_Text hintsState;

    private void OnEnable()
    {
        ShowHints();
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
        if (hintsButton != null)
        {
            hintsButton.onClick.AddListener(ToggleHints);
        }
    }

    private void ToggleHints()
    {
        FirstTimeHints.Enabled = !FirstTimeHints.Enabled;
        ShowHints();
    }

    private void ShowHints()
    {
        if (hintsState != null) hintsState.text = FirstTimeHints.Enabled ? "ON" : "OFF";
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
