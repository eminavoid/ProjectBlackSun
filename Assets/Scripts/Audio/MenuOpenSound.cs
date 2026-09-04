using UnityEngine;
public class MenuOpenSound : MonoBehaviour
{
    private void OnEnable()
    {
        if (Time.frameCount <= 1) return;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayMenuOpen();
        }
    }
}
