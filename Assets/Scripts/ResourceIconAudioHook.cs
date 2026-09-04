using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to each resource icon (Wealth, Zeal, Flock, Authority, Happiness, etc).
/// Set 'resourceType' in the Inspector to the exact Wwise Switch name that
/// corresponds to this icon (e.g. "Gold", "Faith", "Bliss"...).
/// On click, tells AudioManager to set the ResourceType switch and post the
/// resource icon click event.
/// </summary>
[RequireComponent(typeof(Button))]
public class ResourceIconAudioHook : MonoBehaviour
{
    [Tooltip("Must match a Switch name inside the 'ResourceType' Switch Group in Wwise exactly.")]
    public string resourceType;

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.RemoveListener(PlaySound);
        _button.onClick.AddListener(PlaySound);
    }

    private void PlaySound()
    {
        if (string.IsNullOrEmpty(resourceType))
        {
            Debug.LogWarning($"ResourceIconAudioHook on '{gameObject.name}' has no resourceType assigned.", this);
            return;
        }
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayResourceIconClick(resourceType);
        }
    }
}
