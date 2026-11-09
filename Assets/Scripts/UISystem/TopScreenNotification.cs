using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Notificación breve en la parte superior de la pantalla.
/// </summary>
public class TopScreenNotification : MonoBehaviour
{
    private const float DefaultDurationSeconds = 10f;
    private const string RootName = "TopScreenNotificationRoot";

    private static TopScreenNotification instance;

    [SerializeField] private float durationSeconds = DefaultDurationSeconds;

    private static readonly Vector2 RestSize = new Vector2(860f, 72f);
    private static readonly Vector2 HeldSize = new Vector2(980f, 118f);

    private RectTransform root;
    private RectTransform textRect;
    private CanvasGroup group;
    private Image background;
    private TextMeshProUGUI label;
    private Button continueButton;
    private Coroutine hideRoutine;
    private bool holding;
    private string heldMessage = string.Empty;
    private bool heldShowsContinue;
    private Action heldContinue;

    public static void Show(string message, float? durationSeconds = null)
    {
        TopScreenNotification notifier = ResolveInstance();
        if (notifier == null)
        {
            Debug.LogWarning($"TopScreenNotification: no hay ScreenCanvas. Mensaje: {message}");
            return;
        }

        notifier.ShowInternal(message, durationSeconds ?? notifier.durationSeconds);
    }

    public static void Hold(string message, bool showContinue, Action onContinue)
    {
        TopScreenNotification notifier = ResolveInstance();
        if (notifier == null)
        {
            Debug.LogWarning($"TopScreenNotification: no hay ScreenCanvas. Mensaje: {message}");
            return;
        }

        notifier.HoldInternal(message, showContinue, onContinue);
    }

    public static void Release()
    {
        if (instance == null) return;
        instance.ReleaseInternal();
    }

    private static TopScreenNotification ResolveInstance()
    {
        if (instance != null) return instance;

        if (GlobalReferences.ScreenCanvas == null)
        {
            return null;
        }

        Transform existing = GlobalReferences.ScreenCanvas.transform.Find(RootName);
        if (existing != null)
        {
            instance = existing.GetComponent<TopScreenNotification>();
            if (instance == null) instance = existing.gameObject.AddComponent<TopScreenNotification>();
            return instance;
        }

        GameObject rootObject = new GameObject(RootName, typeof(RectTransform));
        rootObject.transform.SetParent(GlobalReferences.ScreenCanvas.transform, false);
        instance = rootObject.AddComponent<TopScreenNotification>();
        instance.BuildUi();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        if (root == null) BuildUi();
        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void BuildUi()
    {
        root = GetComponent<RectTransform>();
        if (root == null) root = gameObject.AddComponent<RectTransform>();

        root.anchorMin = new Vector2(0.5f, 1f);
        root.anchorMax = new Vector2(0.5f, 1f);
        root.pivot = new Vector2(0.5f, 1f);
        root.anchoredPosition = new Vector2(0f, -100f);
        root.sizeDelta = RestSize;

        background = GetComponent<Image>();
        if (background == null) background = gameObject.AddComponent<Image>();
        background.color = new Color(0.09f, 0.06f, 0.04f, 0.94f);
        background.raycastTarget = false;

        Outline outline = gameObject.GetComponent<Outline>();
        if (outline == null) outline = gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.88f, 0.71f, 0.38f, 0.55f);
        outline.effectDistance = new Vector2(2f, -2f);

        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.alpha = 0f;

        GameObject textObject = new GameObject("Message", typeof(RectTransform));
        textObject.transform.SetParent(transform, false);

        textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(24f, 10f);
        textRect.offsetMax = new Vector2(-24f, -10f);

        label = textObject.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 28f;
        label.color = new Color(0.94f, 0.86f, 0.66f);
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;
        label.text = string.Empty;

        GameObject buttonObject = new GameObject("Continue", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(transform, false);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 0.5f);
        buttonRect.anchorMax = new Vector2(1f, 0.5f);
        buttonRect.pivot = new Vector2(1f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(-14f, 0f);
        buttonRect.sizeDelta = new Vector2(52f, 52f);
        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(0.88f, 0.71f, 0.38f, 0.18f);
        continueButton = buttonObject.GetComponent<Button>();
        continueButton.targetGraphic = buttonImage;
        continueButton.onClick.AddListener(OnContinue);

        GameObject markObject = new GameObject("Mark", typeof(RectTransform));
        markObject.transform.SetParent(buttonObject.transform, false);
        RectTransform markRect = markObject.GetComponent<RectTransform>();
        markRect.anchorMin = Vector2.zero;
        markRect.anchorMax = Vector2.one;
        markRect.offsetMin = Vector2.zero;
        markRect.offsetMax = Vector2.zero;
        TextMeshProUGUI mark = markObject.AddComponent<TextMeshProUGUI>();
        mark.font = label.font;
        mark.fontSharedMaterial = label.fontSharedMaterial;
        mark.text = "›";
        mark.fontSize = 36f;
        mark.alignment = TextAlignmentOptions.Center;
        mark.color = label.color;
        mark.raycastTarget = false;
        buttonObject.SetActive(false);
    }

    private void HoldInternal(string message, bool showContinue, Action onContinue)
    {
        if (label == null) BuildUi();
        holding = true;
        heldMessage = message ?? string.Empty;
        heldShowsContinue = showContinue;
        heldContinue = onContinue;
        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        ApplyHold();
    }

    private void ReleaseInternal()
    {
        holding = false;
        heldContinue = null;
        heldShowsContinue = false;
        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        SetContinueVisible(false);
        if (group == null) return;
        group.blocksRaycasts = false;
        group.interactable = false;
        group.DOKill();
        group.DOFade(0f, 0.2f).SetUpdate(true).SetLink(gameObject).OnComplete(() => SetVisible(false));
    }

    private void ApplyHold()
    {
        if (label == null) BuildUi();
        label.text = heldMessage;
        root.sizeDelta = HeldSize;
        if (textRect != null) textRect.offsetMax = new Vector2(heldShowsContinue ? -72f : -24f, -10f);
        SetContinueVisible(heldShowsContinue);
        group.blocksRaycasts = heldShowsContinue;
        group.interactable = heldShowsContinue;
        transform.SetAsLastSibling();
        SetVisible(true);
        group.DOKill();
        group.alpha = 1f;
    }

    private void OnContinue()
    {
        if (!holding || !heldShowsContinue) return;
        heldContinue?.Invoke();
    }

    private void SetContinueVisible(bool visible)
    {
        if (continueButton != null) continueButton.gameObject.SetActive(visible);
    }

    private void ShowInternal(string message, float duration)
    {
        if (label == null) BuildUi();

        label.text = message ?? string.Empty;
        if (root != null) root.sizeDelta = RestSize;
        if (textRect != null) textRect.offsetMax = new Vector2(-24f, -10f);
        SetContinueVisible(false);
        group.blocksRaycasts = false;
        group.interactable = false;
        transform.SetAsLastSibling();
        SetVisible(true);
        group.DOKill();
        group.DOFade(1f, 0.25f).SetUpdate(true).SetLink(gameObject);
        root.DOKill();
        root.anchoredPosition = new Vector2(0f, -84f);
        root.DOAnchorPosY(-100f, 0.35f).SetEase(Ease.OutCubic).SetUpdate(true).SetLink(gameObject);

        if (hideRoutine != null) StopCoroutine(hideRoutine);
        hideRoutine = StartCoroutine(HideAfterSeconds(duration));
    }

    private IEnumerator HideAfterSeconds(float seconds)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, seconds));
        hideRoutine = null;
        if (holding)
        {
            ApplyHold();
            yield break;
        }

        group.DOKill();
        group.DOFade(0f, 0.35f).SetUpdate(true).SetLink(gameObject).OnComplete(() => SetVisible(false));
    }

    private void SetVisible(bool visible)
    {
        if (background != null) background.enabled = visible;
        if (label != null) label.enabled = visible;
        gameObject.SetActive(true);
        if (!visible && label != null) label.text = string.Empty;
    }
}
