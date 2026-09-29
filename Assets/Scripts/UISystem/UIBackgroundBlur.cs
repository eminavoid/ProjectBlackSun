using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Blurs everything on screen (world + overlay UI) behind a window and blocks clicks on it.
// Overlay canvases never show up in URP's render textures, so the final frame is grabbed at end of
// frame, blurred and shown on a full-screen RawImage. Whoever shows the window must wait for
// IsReady before activating the window's own content, otherwise the window ends up in the snapshot.
// UI that must stay sharp (e.g. the resources bar) just needs to be drawn after this object.
[RequireComponent(typeof(RawImage))]
public class UIBackgroundBlur : MonoBehaviour
{
    [SerializeField] Shader blurShader;
    [SerializeField, Range(1, 8)] int downsample = 2;
    [SerializeField, Range(1, 6)] int iterations = 2;
    [SerializeField, Range(0.5f, 4f)] float radius = 1.5f;
    [SerializeField] Color tint = Color.white;

    static Material blurMaterial;

    RawImage image;
    RectTransform rect;
    RenderTexture result;

    public bool IsReady { get; private set; }

    void Awake()
    {
        image = GetComponent<RawImage>();
        rect = (RectTransform)transform;
        image.raycastTarget = true; // swallow clicks meant for whatever is behind the window
    }

    void OnEnable()
    {
        IsReady = false;
        image.enabled = false;
        StartCoroutine(CaptureAtEndOfFrame());
    }

    void OnDisable()
    {
        StopAllCoroutines();
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
        Capture();
        IsReady = true;
    }

    void Capture()
    {
        if (blurMaterial == null)
        {
            if (blurShader == null) { Debug.LogWarning("UIBackgroundBlur: no blur shader assigned.", this); return; }
            blurMaterial = new Material(blurShader) { hideFlags = HideFlags.HideAndDontSave };
        }

        Texture2D screen = ScreenCapture.CaptureScreenshotAsTexture();

        int w = Mathf.Max(16, Screen.width / downsample);
        int h = Mathf.Max(16, Screen.height / downsample);
        RenderTexture a = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture b = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
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

        // Cover the whole canvas so the snapshot lines up 1:1 and every click behind is blocked.
        var canvasRect = (RectTransform)GetComponentInParent<Canvas>().rootCanvas.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = canvasRect.rect.size;
        rect.position = canvasRect.TransformPoint(canvasRect.rect.center);

        image.uvRect = new Rect(0f, 0f, 1f, 1f);
        image.texture = result;
        image.color = tint;
        image.enabled = true;
    }
}
