using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private string gameScene = "SceneClone";
    [SerializeField] private CanvasGroup curtain;
    [SerializeField] private CanvasGroup title;
    [SerializeField] private Graphic eclipseGlow;
    [SerializeField] private CanvasGroup[] buttons;

    private void Start()
    {
        curtain.alpha = 1f;
        curtain.blocksRaycasts = true;
        title.alpha = 0f;
        title.transform.localScale = Vector3.one * 1.08f;

        Sequence intro = DOTween.Sequence()
            .Append(curtain.DOFade(0f, 1.8f).SetEase(Ease.InOutSine))
            .Insert(0.7f, title.DOFade(1f, 1.4f))
            .Insert(0.7f, title.transform.DOScale(1f, 2f).SetEase(Ease.OutCubic))
            .OnComplete(() => curtain.blocksRaycasts = false);

        for (int i = 0; i < buttons.Length; i++)
        {
            var rect = (RectTransform)buttons[i].transform;
            float restX = rect.anchoredPosition.x;
            buttons[i].alpha = 0f;
            rect.anchoredPosition = new Vector2(restX - 50f, rect.anchoredPosition.y);
            intro.Insert(1.6f + i * 0.14f, buttons[i].DOFade(1f, 0.45f));
            intro.Insert(1.6f + i * 0.14f, rect.DOAnchorPosX(restX, 0.6f).SetEase(Ease.OutCubic));
        }

        eclipseGlow.DOFade(0.25f, 2.6f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetLink(gameObject);
    }

    public void NewGame()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.StopBackgroundMusic(1000);
        FadeOut(1f, () => SceneManager.LoadScene(gameScene));
    }

    public void Quit() => FadeOut(0.6f, Application.Quit);

    private void FadeOut(float duration, TweenCallback then)
    {
        curtain.blocksRaycasts = true;
        curtain.DOKill();
        curtain.DOFade(1f, duration).SetEase(Ease.InSine).OnComplete(then);
    }
}
