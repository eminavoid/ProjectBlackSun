using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class CardClickSound : MonoBehaviour
{
    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.RemoveListener(PlaySound);
        _button.onClick.AddListener(PlaySound);
    }

    private void PlaySound()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayCardClick();
        }
    }
}
