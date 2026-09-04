using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach this ONLY to prefabs containing a Button that get Instantiate()'d
/// at runtime (e.g. a dynamically spawned list item like seedItemButtonPrefab).
///
/// Buttons that already exist in the scene at startup do NOT need this ---
/// AudioManager hooks them automatically on its own Awake(). This component
/// exists purely to cover buttons born AFTER that point.
/// </summary>
[RequireComponent(typeof(Button))]
public class UIClickSound : MonoBehaviour
{
    private void Awake()
    {
        Button btn = GetComponent<Button>();
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.HookButton(btn);
        }
        else
        {
            Debug.LogWarning("UIClickSound: AudioManager.Instance is null. Make sure AudioManager exists and initializes before this object spawns.");
        }
    }
}
