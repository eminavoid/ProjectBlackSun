using System.Collections;
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

    private RectTransform root;
    private Image background;
    private TextMeshProUGUI label;
    private Coroutine hideRoutine;

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
        root.anchoredPosition = new Vector2(0f, -24f);
        root.sizeDelta = new Vector2(860f, 72f);

        background = GetComponent<Image>();
        if (background == null) background = gameObject.AddComponent<Image>();
        background.color = new Color(0.06f, 0.07f, 0.1f, 0.92f);
        background.raycastTarget = false;

        GameObject textObject = new GameObject("Message", typeof(RectTransform));
        textObject.transform.SetParent(transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(24f, 10f);
        textRect.offsetMax = new Vector2(-24f, -10f);

        label = textObject.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 28f;
        label.color = Color.white;
        label.enableWordWrapping = true;
        label.raycastTarget = false;
        label.text = string.Empty;
    }

    private void ShowInternal(string message, float duration)
    {
        if (label == null) BuildUi();

        label.text = message ?? string.Empty;
        transform.SetAsLastSibling();
        SetVisible(true);

        if (hideRoutine != null) StopCoroutine(hideRoutine);
        hideRoutine = StartCoroutine(HideAfterSeconds(duration));
    }

    private IEnumerator HideAfterSeconds(float seconds)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, seconds));
        SetVisible(false);
        hideRoutine = null;
    }

    private void SetVisible(bool visible)
    {
        if (background != null) background.enabled = visible;
        if (label != null) label.enabled = visible;
        gameObject.SetActive(true);
        if (!visible && label != null) label.text = string.Empty;
    }
}
