using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class UIBackgroundBlur : MonoBehaviour
{
    [SerializeField] Shader blurShader;
    [SerializeField, Range(1, 8)] int downsample = 2;
    [SerializeField, Range(1, 6)] int iterations = 2;
    [SerializeField, Range(0.5f, 4f)] float radius = 1.5f;
    [Tooltip("Multiplied over the snapshot. Grey dims the background so the window stands out.")]
    [SerializeField] Color tint = new Color(0.72f, 0.72f, 0.72f, 1f);
    [SerializeField, Min(0f)] float fadeDuration = 0.22f;

    static Material blurMaterial;
    static readonly List<UIBackgroundBlur> shown = new List<UIBackgroundBlur>();

    RawImage image;
    RectTransform rect;
    RenderTexture result;

    public bool IsReady { get; private set; }
    public float FadeDuration => fadeDuration;

    void Awake()
    {
        image = GetComponent<RawImage>();
        rect = (RectTransform)transform;
        image.raycastTarget = true;
    }

    void OnEnable()
    {
        IsReady = false;
        image.enabled = false;

        UIBackgroundBlur behind = TopShownBlur();
        if (behind != null)
        {
            result = RenderTexture.GetTemporary(behind.result.descriptor);
            Graphics.Blit(behind.result, result);
            Show(1f);
            IsReady = true;
            return;
        }

        StartCoroutine(CaptureAtEndOfFrame());
    }

    void OnDisable()
    {
        StopAllCoroutines();
        shown.Remove(this);
        IsReady = false;
        if (result != null)
        {
            RenderTexture.ReleaseTemporary(result);
            result = null;
        }
        image.texture = null;
    }

    IEnumerator CaptureAtEndOfFrame()
    {
        yield return new WaitForEndOfFrame();
        bool captured = Capture();
        IsReady = true;
        if (!captured) yield break;

        Show(0f);
        DOVirtual.Float(0f, 1f, fadeDuration, SetAlpha).SetLink(gameObject, LinkBehaviour.KillOnDisable).SetUpdate(true);
    }

    bool Capture()
    {
        if (blurMaterial == null)
        {
            if (blurShader == null) { Debug.LogWarning("UIBackgroundBlur: no blur shader assigned.", this); return false; }
            blurMaterial = new Material(blurShader) { hideFlags = HideFlags.HideAndDontSave };
        }

        Texture2D screen = ScreenCapture.CaptureScreenshotAsTexture();

        int w = Mathf.Max(16, Screen.width / downsample);
        int h = Mathf.Max(16, Screen.height / downsample);
        RenderTexture a = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture b = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        a.wrapMode = b.wrapMode = TextureWrapMode.Clamp;
        Graphics.Blit(screen, a, blurMaterial, 1);
        Destroy(screen);

        for (int i = 0; i < iterations; i++)
        {
            float step = radius * (i + 1);
            blurMaterial.SetVector("_Direction", new Vector2(step / w, 0f));
            Graphics.Blit(a, b, blurMaterial, 0);
            blurMaterial.SetVector("_Direction", new Vector2(0f, step / h));
            Graphics.Blit(b, a, blurMaterial, 0);
        }
        RenderTexture.ReleaseTemporary(b);

        if (result != null) RenderTexture.ReleaseTemporary(result);
        result = a;
        return true;
    }

    void Show(float alpha)
    {
        var canvasRect = (RectTransform)GetComponentInParent<Canvas>().rootCanvas.transform;
        Vector3 parentScale = rect.parent != null ? rect.parent.lossyScale : Vector3.one;
        Vector3 canvasScale = canvasRect.lossyScale;
        rect.localScale = new Vector3(canvasScale.x / parentScale.x, canvasScale.y / parentScale.y, 1f);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = canvasRect.rect.size;
        rect.position = canvasRect.TransformPoint(canvasRect.rect.center);

        image.uvRect = new Rect(0f, 0f, 1f, 1f);
        image.texture = result;
        SetAlpha(alpha);
        image.enabled = true;
        shown.Add(this);
    }

    void SetAlpha(float alpha)
    {
        Color color = tint;
        color.a *= alpha;
        image.color = color;
    }

    static UIBackgroundBlur TopShownBlur()
    {
        for (int i = shown.Count - 1; i >= 0; i--)
        {
            UIBackgroundBlur blur = shown[i];
            if (blur != null && blur.isActiveAndEnabled && blur.result != null) return blur;
        }

        return null;
    }
}
