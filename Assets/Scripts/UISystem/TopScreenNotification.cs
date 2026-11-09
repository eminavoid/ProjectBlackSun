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

    private static readonly Vector2 RestSize = new Vector2(760f, 96f);
    private static readonly Vector2 HeldSize = new Vector2(840f, 136f);

    private RectTransform root;
    private RectTransform textRect;
    private CanvasGroup group;
    private Image background;
    private TextMeshProUGUI label;
    private Button continueButton;
    private Coroutine hideRoutine;
    private bool holding;
    private float bottomLift;
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

    public static void Lift(float extra)
    {
        TopScreenNotification notifier = ResolveInstance();
        if (notifier == null) return;
        notifier.bottomLift = Mathf.Max(0f, extra);
        if (notifier.root != null)
            notifier.root.anchoredPosition = new Vector2(-210f, 360f);
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

        root.anchorMin = new Vector2(1f, 0.5f);
        root.anchorMax = new Vector2(1f, 0.5f);
        root.pivot = new Vector2(1f, 0.5f);
        root.anchoredPosition = new Vector2(-210f, 360f);
        root.sizeDelta = RestSize;

        background = GetComponent<Image>();
        if (background == null) background = gameObject.AddComponent<Image>();
        background.sprite = Art("Papel_Con_Borde");
        background.type = Image.Type.Simple;
        background.color = new Color(1f, 1f, 1f, 0.72f);
        background.raycastTarget = false;

        Outline outline = gameObject.GetComponent<Outline>();
        if (outline != null) outline.enabled = false;

        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.alpha = 0f;

        GameObject textObject = new GameObject("Message", typeof(RectTransform));
        textObject.transform.SetParent(transform, false);

        textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(36f, 16f);
        textRect.offsetMax = new Vector2(-36f, -16f);

        label = textObject.AddComponent<TextMeshProUGUI>();
        TextMeshProUGUI sample = GetComponentInParent<Canvas>() != null
            ? GetComponentInParent<Canvas>().GetComponentInChildren<TextMeshProUGUI>(true)
            : null;
        if (sample != null)
        {
            label.font = sample.font;
            label.fontSharedMaterial = sample.fontSharedMaterial;
        }

        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 24f;
        label.color = new Color(0.27f, 0.16f, 0.11f, 1f);
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;
        label.text = string.Empty;

        GameObject buttonObject = new GameObject("Continue", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(transform, false);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 0.5f);
        buttonRect.anchorMax = new Vector2(1f, 0.5f);
        buttonRect.pivot = new Vector2(1f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(-22f, 0f);
        buttonRect.sizeDelta = new Vector2(148f, 46f);
        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.sprite = Art("Ladrillo_Gris_Largo_1");
        buttonImage.type = Image.Type.Simple;
        buttonImage.color = new Color(1f, 1f, 1f, 0.92f);
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
        mark.text = "NEXT";
        mark.fontSize = 18f;
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
        bottomLift = 0f;
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
        Place(true);
        FitToText(heldShowsContinue);
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
        Place(false);
        FitToText(false);
        SetContinueVisible(false);
        group.blocksRaycasts = false;
        group.interactable = false;
        transform.SetAsLastSibling();
        SetVisible(true);
        group.DOKill();
        group.DOFade(1f, 0.25f).SetUpdate(true).SetLink(gameObject);

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

    private void Place(bool held)
    {
        if (root == null) return;
        root.anchorMin = new Vector2(1f, 0.5f);
        root.anchorMax = new Vector2(1f, 0.5f);
        root.pivot = new Vector2(1f, 0.5f);
        root.anchoredPosition = new Vector2(-210f, 360f);
        root.sizeDelta = held ? HeldSize : RestSize;
    }

    private void FitToText(bool showContinue)
    {
        if (root == null || label == null) return;

        const float padX = 64f;
        const float padY = 46f;
        const float buttonSpace = 176f;
        const float maxWidth = 560f;
        const float minWidth = 420f;

        float side = showContinue ? buttonSpace : 0f;
        float textWidth = maxWidth - padX * 2f - side;
        Vector2 preferred = label.GetPreferredValues(label.text, textWidth, 0f);
        float width = Mathf.Clamp(preferred.x + padX * 2f + side, minWidth, maxWidth);
        textWidth = width - padX * 2f - side;
        preferred = label.GetPreferredValues(label.text, textWidth, 0f);
        float height = Mathf.Clamp(preferred.y + padY * 2f, 112f, 320f);

        root.sizeDelta = new Vector2(width, height);
        if (textRect == null) return;
        textRect.offsetMin = new Vector2(padX, padY);
        textRect.offsetMax = new Vector2(-(padX + side), -padY);
    }

    private static Sprite Art(string spriteName)
    {
        Sprite[] sprites = Resources.FindObjectsOfTypeAll<Sprite>();
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] != null && sprites[i].name == spriteName) return sprites[i];
        }

        return null;
    }

    private void SetVisible(bool visible)
    {
        if (background != null) background.enabled = visible;
        if (label != null) label.enabled = visible;
        gameObject.SetActive(true);
        if (!visible && label != null) label.text = string.Empty;
    }
}
