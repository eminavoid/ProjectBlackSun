using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zeke.UI;

public class TheologyRevelation : MonoBehaviour
{
    private const float KickerSpacing = 14f;

    private static Sprite glowSprite;
    private static Sprite raysSprite;

    private TheologyRewardKind kind;
    private CanvasGroup group;
    private RectTransform body;
    private RectTransform title;
    private TMP_Text kicker;
    private Image glow;
    private Image rays;

    public static void Dress(Transform window, TheologyRewardKind kind, string kickerText)
    {
        if (window == null) return;
        window.gameObject.AddComponent<TheologyRevelation>().Build(kind, kickerText);
    }

    public static string InkHex(TheologyRewardKind kind)
    {
        switch (kind)
        {
            case TheologyRewardKind.Curse: return "8B1A1A";
            case TheologyRewardKind.Blessing: return "8A6400";
            case TheologyRewardKind.Seed: return "3D6B2A";
            default: return "34407F";
        }
    }

    private static Color Light(TheologyRewardKind kind)
    {
        switch (kind)
        {
            case TheologyRewardKind.Curse: return new Color(0.9f, 0.14f, 0.08f);
            case TheologyRewardKind.Blessing: return new Color(1f, 0.8f, 0.32f);
            case TheologyRewardKind.Seed: return new Color(0.62f, 0.92f, 0.42f);
            default: return new Color(0.58f, 0.68f, 1f);
        }
    }

    private void Build(TheologyRewardKind rewardKind, string kickerText)
    {
        kind = rewardKind;
        group = GetComponent<CanvasGroup>();

        UIBackgroundBlur blur = GetComponentInChildren<UIBackgroundBlur>(true);
        if (blur != null) blur.Tint = kind == TheologyRewardKind.Curse ? new Color(0.45f, 0.3f, 0.3f) : new Color(0.4f, 0.38f, 0.44f);

        body = WrapContent(blur != null ? blur.transform : null);
        glow = Halo("Revelation Glow", GlowSprite, new Vector2(2000f, 1250f));
        if (kind != TheologyRewardKind.Curse) rays = Halo("Revelation Rays", RaysSprite, new Vector2(2600f, 2600f));
        body.SetAsLastSibling();

        UIWindow window = GetComponent<UIWindow>();
        TMP_Text description = window.TryGetElement<TextMeshProUGUI>("Description");
        if (description != null)
        {
            description.rectTransform.anchoredPosition += new Vector2(0f, -40f);
            description.rectTransform.sizeDelta += new Vector2(0f, -30f);
            description.enableAutoSizing = true;
            description.fontSizeMax = description.fontSize;
            description.fontSizeMin = 28f;
        }

        TMP_Text titleText = window.TryGetElement<TextMeshProUGUI>("Title");
        if (titleText == null) return;

        titleText.color = new Color(0.17f, 0.1f, 0.07f);
        title = (RectTransform)titleText.transform.parent;
        title.anchoredPosition += new Vector2(0f, -40f);
        kicker = Kicker(kickerText, title);
    }

    private RectTransform WrapContent(Transform blur)
    {
        var holder = new GameObject("Revelation Body", typeof(RectTransform));
        var rect = (RectTransform)holder.transform;
        rect.SetParent(transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child == rect || child == blur) continue;
            child.SetParent(rect, true);
            child.SetSiblingIndex(0);
        }

        return rect;
    }

    private Image Halo(string haloName, Sprite sprite, Vector2 size)
    {
        var halo = new GameObject(haloName, typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)halo.transform;
        rect.SetParent(transform, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;

        Image image = halo.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        Color color = Light(kind);
        color.a = 0f;
        image.color = color;
        return image;
    }

    private TMP_Text Kicker(string text, RectTransform titleRect)
    {
        var holder = new GameObject("Kicker", typeof(RectTransform));
        var rect = (RectTransform)holder.transform;
        rect.SetParent(body, false);
        rect.anchorMin = titleRect.anchorMin;
        rect.anchorMax = titleRect.anchorMax;
        rect.pivot = titleRect.pivot;
        rect.sizeDelta = new Vector2(titleRect.sizeDelta.x, 46f);
        rect.anchoredPosition = titleRect.anchoredPosition + new Vector2(0f, 60f);

        TextMeshProUGUI label = holder.AddComponent<TextMeshProUGUI>();
        Button button = body.GetComponentInChildren<Button>(true);
        TMP_Text buttonLabel = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
        if (buttonLabel != null) label.font = buttonLabel.font;
        label.text = text.ToUpperInvariant();
        label.fontSize = 38f;
        label.characterSpacing = KickerSpacing;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        ColorUtility.TryParseHtmlString("#" + InkHex(kind), out Color ink);
        label.color = ink;
        label.alpha = 0f;
        return label;
    }

    private IEnumerator Start()
    {
        while (group != null && group.alpha <= 0.01f) yield return null;
        Play();
    }

    private void Play()
    {
        bool curse = kind == TheologyRewardKind.Curse;
        float peak = curse ? 0.55f : 0.6f;

        body.localScale = Vector3.one * 0.86f;
        body.DOScale(1f, 0.5f).SetEase(Ease.OutBack, 1.7f).SetUpdate(true).SetLink(gameObject);

        glow.rectTransform.localScale = Vector3.one * 0.4f;
        DOTween.Sequence()
            .Join(glow.rectTransform.DOScale(1.08f, 0.6f).SetEase(Ease.OutCubic))
            .Join(glow.DOFade(curse ? 1f : 0.85f, 0.35f))
            .Append(glow.DOFade(peak, 0.5f))
            .AppendCallback(() =>
            {
                glow.rectTransform.DOScale(1.15f, 1.6f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(gameObject);
                glow.DOFade(peak * 0.7f, 1.6f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(gameObject);
            })
            .SetUpdate(true).SetLink(gameObject);

        if (rays != null)
        {
            rays.rectTransform.localScale = Vector3.one * 0.6f;
            rays.rectTransform.DOScale(1f, 0.9f).SetEase(Ease.OutCubic).SetUpdate(true).SetLink(gameObject);
            rays.DOFade(0.32f, 0.9f).SetUpdate(true).SetLink(gameObject);
            rays.rectTransform.DORotate(new Vector3(0f, 0f, -360f), 50f, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart).SetUpdate(true).SetLink(gameObject);
        }

        if (curse)
        {
            body.DOShakeAnchorPos(0.5f, 26f, 26, 90f, false, true).SetDelay(0.12f).SetUpdate(true).SetLink(gameObject);
        }

        if (kicker != null)
        {
            kicker.characterSpacing = KickerSpacing + 30f;
            kicker.DOFade(1f, 0.35f).SetDelay(0.15f).SetUpdate(true).SetLink(gameObject);
            DOTween.To(() => kicker.characterSpacing, value => kicker.characterSpacing = value, KickerSpacing, 0.7f)
                .SetDelay(0.15f).SetEase(Ease.OutCubic).SetUpdate(true).SetLink(gameObject);
        }

        if (title != null)
        {
            title.DOPunchScale(Vector3.one * 0.08f, 0.45f, 6, 0.6f).SetDelay(0.3f).SetUpdate(true).SetLink(gameObject);
        }
    }

    private static Sprite GlowSprite => glowSprite != null ? glowSprite : glowSprite = Radial(128, false);
    private static Sprite RaysSprite => raysSprite != null ? raysSprite : raysSprite = Radial(512, true);

    private static Sprite Radial(int size, bool beams)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        var pixels = new Color32[size * size];
        float half = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float falloff = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                float alpha = falloff * falloff * (3f - 2f * falloff);
                if (beams) alpha *= Mathf.Pow(Mathf.Abs(Mathf.Cos(Mathf.Atan2(dy, dx) * 8f)), 5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
    }
}
