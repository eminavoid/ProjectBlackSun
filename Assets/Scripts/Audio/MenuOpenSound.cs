using UnityEngine;
public class MenuOpenSound : MonoBehaviour
{
    private void Awake()
    {
        NodeTooltipMenuBlock.Ensure(gameObject);
    }

    private void OnEnable()
    {
        if (Time.frameCount <= 1) return;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayMenuOpen();
        }
    }
}
