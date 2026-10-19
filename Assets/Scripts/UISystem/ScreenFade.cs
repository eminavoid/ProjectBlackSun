using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ScreenFade : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField, Min(0f)] private float fadeInDelay = 0.35f;
    [SerializeField, Min(0.01f)] private float fadeInDuration = 1.6f;
    [SerializeField, Min(0.01f)] private float fadeOutDuration = 0.8f;

    private void Awake()
    {
        group.alpha = 1f;
        group.blocksRaycasts = true;
    }

    private void Start()
    {
        group.DOFade(0f, fadeInDuration)
            .SetDelay(fadeInDelay)
            .SetEase(Ease.InOutSine)
            .SetUpdate(true)
            .SetLink(gameObject)
            .OnComplete(() => group.blocksRaycasts = false);
    }

    public void LoadScene(string scene)
    {
        group.DOKill();
        group.blocksRaycasts = true;
        group.DOFade(1f, fadeOutDuration)
            .SetEase(Ease.InSine)
            .SetUpdate(true)
            .SetLink(gameObject)
            .OnComplete(() => SceneManager.LoadScene(scene));
    }
}
