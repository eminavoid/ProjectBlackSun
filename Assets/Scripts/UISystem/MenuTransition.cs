using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

public class MenuTransition : MonoBehaviour
{
    private enum Style { Slide, Pop }

    [SerializeField] private RectTransform panel;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private Style style = Style.Pop;
    [SerializeField] private Vector2 slideOffset = new Vector2(-120f, 0f);
    [SerializeField, Range(0.5f, 1f)] private float popScale = 0.92f;
    [SerializeField, Min(0.01f)] private float duration = 0.3f;
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private bool destroyOnClose;
    [SerializeField] private UnityEvent onClosed;

    private Vector2 restPosition;
    private Vector3 restScale;
    private Sequence motion;
    private bool closing;

    public static void Show(GameObject target)
    {
        if (target.TryGetComponent(out MenuTransition transition)) transition.Open();
        else target.SetActive(true);
    }

    public static void Hide(GameObject target)
    {
        if (target.TryGetComponent(out MenuTransition transition)) transition.Close();
        else target.SetActive(false);
    }

    private void Awake()
    {
        restPosition = panel.anchoredPosition;
        restScale = panel.localScale;
    }

    private void OnEnable()
    {
        if (playOnEnable) Play(true);
    }

    private void OnDisable()
    {
        motion?.Kill();
        closing = false;
        if (group != null)
        {
            group.alpha = 1f;
            group.blocksRaycasts = true;
        }

        if (style == Style.Slide) panel.anchoredPosition = restPosition;
        else panel.localScale = restScale;
    }

    public void Open()
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        else if (closing) Play(true);
    }

    public void Close()
    {
        if (closing) return;
        if (!gameObject.activeInHierarchy)
        {
            Finish();
            return;
        }

        Play(false);
    }

    private void Play(bool show)
    {
        bool reopening = show && closing;
        closing = !show;
        motion?.Kill();

        if (show && !reopening)
        {
            if (group != null) group.alpha = 0f;
            if (style == Style.Slide) panel.anchoredPosition = restPosition + slideOffset;
            else panel.localScale = restScale * popScale;
        }

        float time = show ? duration : duration * 0.75f;
        motion = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        if (group != null)
        {
            group.blocksRaycasts = show;
            motion.Join(group.DOFade(show ? 1f : 0f, time));
        }

        if (style == Style.Slide)
        {
            motion.Join(panel.DOAnchorPos(show ? restPosition : restPosition + slideOffset, time).SetEase(show ? Ease.OutCubic : Ease.InCubic));
        }
        else
        {
            motion.Join(panel.DOScale(show ? restScale : restScale * popScale, time).SetEase(show ? Ease.OutBack : Ease.InCubic));
        }

        if (!show) motion.OnComplete(Finish);
    }

    private void Finish()
    {
        closing = false;
        if (destroyOnClose) Destroy(gameObject);
        else gameObject.SetActive(false);
        onClosed?.Invoke();
    }
}
