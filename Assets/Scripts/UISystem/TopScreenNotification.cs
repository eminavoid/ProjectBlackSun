using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Zeke.Abilities;
using Zeke.Tooltips;

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
    private static readonly Vector2 TopRight = new Vector2(-210f, 416f);

    private RectTransform root;
    private RectTransform textRect;
    private CanvasGroup group;
    private Image background;
    private TextMeshProUGUI label;
    private Button continueButton;
    private TextMeshProUGUI continueMark;
    private TextMeshProUGUI kickerLabel;
    private RectTransform kickerRect;
    private RectTransform buttonRect;
    private string heldKicker = string.Empty;
    private LinkTooltipTrigger links;
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

    public static bool IsHolding => instance != null && instance.holding;

    public static void Hold(string message, bool showContinue, Action onContinue, string continueLabel = "NEXT", string kicker = null)
    {
        TopScreenNotification notifier = ResolveInstance();
        if (notifier == null)
        {
            Debug.LogWarning($"TopScreenNotification: no hay ScreenCanvas. Mensaje: {message}");
            return;
        }

        if (notifier.continueMark != null) notifier.continueMark.text = continueLabel;
        notifier.heldKicker = kicker ?? string.Empty;
        notifier.HoldInternal(message, showContinue, onContinue);
    }

    public static void Lift(float extra)
    {
        TopScreenNotification notifier = ResolveInstance();
        if (notifier == null) return;
        notifier.bottomLift = Mathf.Max(0f, extra);
        if (notifier.root != null)
            notifier.root.anchoredPosition = TopRight;
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
        root.pivot = new Vector2(1f, 1f);
        root.anchoredPosition = TopRight;
        root.sizeDelta = RestSize;

        background = GetComponent<Image>();
        if (background == null) background = gameObject.AddComponent<Image>();
        background.sprite = UiArt.Find("Papel_Con_Borde");
        background.type = Image.Type.Simple;
        background.color = Color.white;
        background.raycastTarget = true;

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
        textObject.AddComponent<HoverLinkHandlerTMPro>();
        links = textObject.AddComponent<LinkTooltipTrigger>();

        GameObject kickerObject = new GameObject("Kicker", typeof(RectTransform));
        kickerObject.transform.SetParent(transform, false);
        kickerRect = kickerObject.GetComponent<RectTransform>();
        kickerRect.anchorMin = new Vector2(0f, 1f);
        kickerRect.anchorMax = new Vector2(1f, 1f);
        kickerRect.pivot = new Vector2(0.5f, 1f);
        kickerLabel = kickerObject.AddComponent<TextMeshProUGUI>();
        kickerLabel.font = label.font;
        kickerLabel.fontSharedMaterial = label.fontSharedMaterial;
        kickerLabel.fontSize = 18f;
        kickerLabel.characterSpacing = 6f;
        kickerLabel.alignment = TextAlignmentOptions.BottomLeft;
        kickerLabel.color = new Color(0.55f, 0.17f, 0.12f, 1f);
        kickerLabel.raycastTarget = false;
        kickerLabel.text = string.Empty;

        GameObject buttonObject = new GameObject("Continue", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(transform, false);
        buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 0f);
        buttonRect.anchorMax = new Vector2(1f, 0f);
        buttonRect.pivot = new Vector2(1f, 0f);
        buttonRect.sizeDelta = new Vector2(148f, 46f);
        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.sprite = UiArt.Find("Ladrillo_Gris_Largo_1");
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
        continueMark = mark;
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
        TooltipLinksDB glossary = Tooltip.Glossary;
        if (links != null) links.Database = glossary;
        label.text = glossary != null ? glossary.Highlight(heldMessage) : heldMessage;
        kickerLabel.text = heldKicker;
        Place(true);
        FitToText(true, heldShowsContinue);
        SetContinueVisible(heldShowsContinue);
        group.blocksRaycasts = true;
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
        kickerLabel.text = string.Empty;
        Place(false);
        FitToText(false, false);
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
        root.pivot = new Vector2(1f, 1f);
        root.anchoredPosition = TopRight;
        root.sizeDelta = held ? HeldSize : RestSize;
    }

    private void FitToText(bool held, bool showContinue)
    {
        if (root == null || label == null) return;

        const float padX = 52f;
        const float padTop = 40f;
        const float padBottom = 38f;
        const float kickerHeight = 30f;
        const float buttonHeight = 46f;
        const float gap = 10f;
        const float heldWidth = 560f;
        const float minWidth = 360f;

        label.alignment = held ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Center;
        float width = held
            ? heldWidth
            : Mathf.Clamp(label.GetPreferredValues(label.text, heldWidth - padX * 2f, 0f).x + padX * 2f, minWidth, heldWidth);
        float textHeight = label.GetPreferredValues(label.text, width - padX * 2f, 0f).y;
        bool hasKicker = kickerLabel != null && !string.IsNullOrEmpty(kickerLabel.text);
        float top = padTop + (hasKicker ? kickerHeight : 0f);
        float bottom = padBottom + (showContinue ? buttonHeight + gap : 0f);
        float height = Mathf.Max(top + textHeight + bottom, 104f);

        root.sizeDelta = new Vector2(width, height);
        if (kickerRect != null)
        {
            kickerRect.offsetMin = new Vector2(padX, -(padTop + kickerHeight));
            kickerRect.offsetMax = new Vector2(-padX, -padTop);
        }

        if (buttonRect != null) buttonRect.anchoredPosition = new Vector2(-(padX - 10f), padBottom - 8f);
        if (textRect == null) return;
        textRect.offsetMin = new Vector2(padX, bottom);
        textRect.offsetMax = new Vector2(-padX, -top);
    }

    private void SetVisible(bool visible)
    {
        if (background != null) background.enabled = visible;
        if (label != null) label.enabled = visible;
        if (kickerLabel != null) kickerLabel.enabled = visible;
        gameObject.SetActive(true);
        if (!visible && label != null) label.text = string.Empty;
    }
}
