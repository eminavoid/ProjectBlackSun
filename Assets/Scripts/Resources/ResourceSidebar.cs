using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zeke.UI;

/// <summary>
/// The resources bar. When a resource changes its number ticks to the new value, the slot punches
/// and the change floats out next to it ("+25").
/// Changes can be held back (e.g. while a result window shows them) and released when their chips
/// fly in, so the bar changes the moment they land.
/// </summary>
[RequireComponent(typeof(UIWindow))]
public class ResourceSidebar : MonoBehaviour
{
    [Header("Colors")]
    [Tooltip("Brighter than the result window's, the bar is dark.")]
    [SerializeField] private Color gainColor = new Color(0.55f, 0.95f, 0.45f);
    [SerializeField] private Color lossColor = new Color(1f, 0.42f, 0.36f);
    [SerializeField] private Color refundColor = new Color(0.8f, 0.8f, 0.8f);

    [Header("Ticking Number")]
    [SerializeField, Min(0.01f)] private float countDuration = 0.55f;
    [SerializeField, Min(0.01f)] private float punchDuration = 0.42f;
    [Tooltip("The slot's icon grows this much and settles back.")]
    [SerializeField, Min(1f)] private float punchScale = 1.25f;

    [Header("Floating Change")]
    [Tooltip("The bar's own font has no '+'. Empty uses the bar's font.")]
    [SerializeField] private TMP_FontAsset popupFont;
    [SerializeField, Min(1f)] private float popupFontSize = 38f;
    [Tooltip("From the slot's number, in the bar's units.")]
    [SerializeField] private Vector2 popupOffset = new Vector2(-55f, 0f);
    [SerializeField] private float popupRise = 40f;
    [SerializeField, Min(0.01f)] private float popupDuration = 1f;

    [Header("Flying Chips")]
    [SerializeField, Min(0f)] private float flightStagger = 0.13f;
    [SerializeField, Min(0.01f)] private float liftDuration = 0.16f;
    [SerializeField, Min(1f)] private float liftScale = 1.45f;
    [SerializeField, Min(0.01f)] private float flightDuration = 0.62f;
    [Tooltip("How far the path bends upwards, in screen units.")]
    [SerializeField] private float arcHeight = 160f;
    [Tooltip("How much what a cost lands on grows when it arrives (cost icons are small).")]
    [SerializeField, Min(1f)] private float arrivalPunchScale = 1.8f;

    private class Slot
    {
        public RectTransform icon;
        public TMP_Text text;
        public Color textColor;
        public float shown;
        public int target;
        public Sequence ticking;
    }

    // An amount already applied to the player's resources but not shown yet. Its owner lets go of it
    // explicitly (a chip landing) or by being destroyed (a window closed some other way).
    private struct Held
    {
        public Object owner;
        public Resource resource;
        public int amount;
    }

    private readonly Dictionary<Resource, Slot> slots = new Dictionary<Resource, Slot>();
    private readonly List<Held> held = new List<Held>();
    private readonly HashSet<Resource> dirty = new HashSet<Resource>();
    private PlayerResources resources;
    private Transform canvas;

    /// <summary>True while chips are flying in or out, or the bar is still showing a change.</summary>
    // Every animation of the bar is tagged with the bar itself.
    public bool IsBusy => DOTween.IsTweening(this);

    public void Initialize(PlayerResources playerResources)
    {
        resources = playerResources;
        canvas = GlobalReferences.ScreenCanvas.transform;
        UIWindow window = GetComponent<UIWindow>();

        // Each slot's number is the UIElement named after its resource; the slot is its parent, with the icon next to it.
        foreach (Resource resource in System.Enum.GetValues(typeof(Resource)))
        {
            TMP_Text text = window.TryGetElement<TMP_Text>(resource.ToString());
            if (text == null) continue;

            int amount = resources.GetResourceAmount(resource);
            Slot slot = new Slot
            {
                icon = FindIcon(text),
                text = text,
                textColor = text.color,
                shown = amount,
                target = amount
            };

            text.text = Format(resource, amount);
            slots[resource] = slot;
        }

        resources.onResourceGained += OnResourceChanged;
    }

    private static RectTransform FindIcon(TMP_Text text)
    {
        Transform slot = text.transform.parent;
        foreach (Transform child in slot)
        {
            if (child != text.transform && child.GetComponent<Image>() != null) return (RectTransform)child;
        }
        return (RectTransform)slot;
    }

    private void OnDestroy()
    {
        if (resources != null) resources.onResourceGained -= OnResourceChanged;
        DOTween.Kill(this);
    }

    /// <summary>Keeps showing the values from before these changes until <paramref name="owner"/> lets go of them.</summary>
    public void Hold(Object owner, IReadOnlyList<ResourceDelta> deltas)
    {
        for (int i = 0; i < deltas.Count; i++)
        {
            if (deltas[i].Amount == 0) continue;
            held.Add(new Held { owner = owner, resource = deltas[i].Resource, amount = deltas[i].Amount });
        }
    }

    /// <summary>
    /// Flies the chips into their slots; each one's change shows when it lands.
    /// What <paramref name="owner"/> held moves to the chips, so the bar doesn't change before they arrive.
    /// </summary>
    public void Receive(Object owner, List<(RectTransform rect, ResourceDelta delta)> chips)
    {
        for (int i = 0; i < chips.Count; i++)
        {
            (RectTransform chip, ResourceDelta delta) = chips[i];
            if (!slots.TryGetValue(delta.Resource, out Slot slot))
            {
                Destroy(chip.gameObject);
                continue;
            }

            Hand(owner, chip.gameObject, delta);

            chip.SetParent(canvas, true);
            chip.SetAsLastSibling();
            CanvasGroup group = chip.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            Fly(chip, delta, slot, i * flightStagger);
        }
    }

    /// <summary>
    /// Shows a change before it's applied: the amount leaves its slot and flies into <paramref name="target"/>
    /// (e.g. the cost on an option), which punches when it arrives. The returned tween completes then.
    /// The bar keeps showing the change until <paramref name="owner"/> calls <see cref="Release"/>, once the change
    /// is really applied, or is destroyed, if it never is.
    /// </summary>
    public Tween Send(Object owner, Resource resource, int change, RectTransform target, float delay)
    {
        if (change == 0 || !slots.TryGetValue(resource, out Slot slot)) return null;

        Image token = CreateToken(slot);
        token.gameObject.SetActive(false);
        RectTransform tokenRect = token.rectTransform;
        Vector3 destination = target != null ? CanvasPoint(target) : CanvasPoint(slot.icon);
        float endScale = tokenRect.localScale.x * (target != null ? WorldHeight(target) : WorldHeight(slot.icon)) / WorldHeight(tokenRect);

        // The target may go away mid-flight; the token then finishes at its last known place.
        Sequence travel = Travel(tokenRect, CanvasPoint(tokenRect), () => destination = target != null ? CanvasPoint(target) : destination, endScale);
        travel.Insert(liftDuration + flightDuration * 0.9f, token.DOFade(0f, flightDuration * 0.1f));

        return DOTween.Sequence()
            .AppendInterval(delay)
            .AppendCallback(() =>
            {
                // The amount leaves the slot now: the bar shows the change from here on.
                token.gameObject.SetActive(true);
                held.Add(new Held { owner = owner, resource = resource, amount = -change });
                Retarget(resource, null);
            })
            .Append(travel)
            .OnComplete(() =>
            {
                if (target != null) PunchArrival(target);
            })
            .OnKill(() => { if (token != null) Destroy(token.gameObject); })
            .SetLink(token.gameObject).SetId(this).SetUpdate(true);
    }

    private void Hand(Object owner, Object newOwner, ResourceDelta delta)
    {
        for (int i = 0; i < held.Count; i++)
        {
            Held entry = held[i];
            if (entry.owner != owner || entry.resource != delta.Resource || entry.amount != delta.Amount) continue;

            entry.owner = newOwner;
            held[i] = entry;
            return;
        }
    }

    private void OnResourceChanged(Resource resource, int amount)
    {
        // Applied at the end of the frame: changes held in the same frame (an outcome) mustn't show first.
        dirty.Add(resource);
    }

    private void LateUpdate()
    {
        for (int i = held.Count - 1; i >= 0; i--)
        {
            if (held[i].owner != null) continue;
            dirty.Add(held[i].resource);
            held.RemoveAt(i);
        }

        foreach (Resource resource in dirty)
        {
            Retarget(resource, null);
        }
        dirty.Clear();
    }

    /// <summary>Stops holding what <paramref name="owner"/> held. Call it right when those changes are really applied.</summary>
    public void Release(Object owner)
    {
        for (int i = held.Count - 1; i >= 0; i--)
        {
            if (held[i].owner != owner) continue;
            dirty.Add(held[i].resource);
            held.RemoveAt(i);
        }
    }

    // A chip landed: what it held shows now, flashing its color.
    private void Land(Object owner, Color color)
    {
        for (int i = held.Count - 1; i >= 0; i--)
        {
            if (held[i].owner != owner) continue;
            Resource resource = held[i].resource;
            held.RemoveAt(i);
            Retarget(resource, color);
        }
    }

    private void Retarget(Resource resource, Color? color)
    {
        if (!slots.TryGetValue(resource, out Slot slot)) return;

        int target = resources.GetResourceAmount(resource);
        for (int i = 0; i < held.Count; i++)
        {
            if (held[i].resource == resource) target -= held[i].amount;
        }

        int change = target - slot.target;
        if (change == 0) return;

        slot.target = target;
        Color flash = color ?? (change > 0 ? gainColor : lossColor);
        Tick(resource, slot, flash);
        Float(resource, slot, change, flash);
    }

    // The number counts to its target and flashes the change's color while the icon punches.
    private void Tick(Resource resource, Slot slot, Color flash)
    {
        slot.ticking?.Kill();
        slot.icon.localScale = Vector3.one;
        slot.text.color = flash;

        Sequence tick = DOTween.Sequence();
        tick.Join(DOVirtual.Float(slot.shown, slot.target, countDuration, value =>
            {
                slot.shown = value;
                slot.text.text = Format(resource, Mathf.RoundToInt(value));
            }).SetEase(Ease.OutCubic))
            .Join(slot.text.DOColor(slot.textColor, Mathf.Max(countDuration, punchDuration)))
            .Join(slot.icon.DOScale(punchScale, punchDuration * 0.5f).SetLoops(2, LoopType.Yoyo).SetEase(Ease.OutQuad))
            .OnComplete(() => slot.text.text = Format(resource, slot.target))
            .OnKill(() => { if (slot.ticking == tick) slot.ticking = null; })
            .SetId(this).SetUpdate(true);
        slot.ticking = tick;
    }

    // "+25" pops out beside the number, drifts up and fades.
    private void Float(Resource resource, Slot slot, int change, Color color)
    {
        GameObject popup = new GameObject($"{resource} {change:+#;-#}", typeof(RectTransform));
        popup.transform.SetParent(transform, false);
        popup.AddComponent<LayoutElement>().ignoreLayout = true;

        TextMeshProUGUI text = popup.AddComponent<TextMeshProUGUI>();
        text.font = popupFont != null ? popupFont : slot.text.font;
        text.fontSize = popupFontSize;
        text.alignment = TextAlignmentOptions.Right;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.text = (change > 0 ? "+" : "") + Format(resource, change);
        // Invisible on its first frame, so a blur snapshot taken that frame doesn't keep a copy of it.
        text.color = new Color(color.r, color.g, color.b, 0f);

        RectTransform rect = (RectTransform)popup.transform;
        rect.pivot = new Vector2(1f, 0.5f);
        rect.sizeDelta = new Vector2(200f, popupFontSize * 1.5f);
        rect.localPosition = (Vector3)transform.InverseTransformPoint(slot.text.transform.position) + (Vector3)popupOffset;
        rect.localScale = Vector3.one * 0.6f;

        DOTween.Sequence()
            .Join(rect.DOLocalMoveY(rect.localPosition.y + popupRise, popupDuration).SetEase(Ease.OutCubic))
            .Join(rect.DOScale(1f, popupDuration * 0.25f).SetEase(Ease.OutBack))
            .Join(text.DOFade(1f, popupDuration * 0.12f))
            .Insert(popupDuration * 0.55f, text.DOFade(0f, popupDuration * 0.45f))
            .OnKill(() => { if (popup != null) Destroy(popup); })
            .SetLink(popup).SetId(this).SetUpdate(true);
    }

    // The chip arcs into its slot shrinking onto the slot's icon, and its change shows on landing.
    private void Fly(RectTransform chip, ResourceDelta delta, Slot slot, float delay)
    {
        Image icon = chip.GetComponentInChildren<Image>();
        TMP_Text amount = chip.GetComponentInChildren<TMP_Text>();
        RectTransform iconRect = icon != null ? icon.rectTransform : chip;
        float landScale = chip.localScale.x * WorldHeight(slot.icon) / WorldHeight(iconRect);

        Sequence travel = Travel(chip, CanvasPoint(iconRect), () => CanvasPoint(slot.icon), landScale);
        if (amount != null) travel.Insert(liftDuration, amount.DOFade(0f, flightDuration * 0.4f));
        if (icon != null) travel.Insert(liftDuration + flightDuration * 0.85f, icon.DOFade(0f, flightDuration * 0.15f));

        Color landing = delta.IsRefund ? refundColor : delta.Amount > 0 ? gainColor : lossColor;
        travel.PrependInterval(delay)
            .OnComplete(() => Land(chip.gameObject, landing))
            .OnKill(() => { if (chip != null) Destroy(chip.gameObject); })
            .SetLink(chip.gameObject).SetId(this).SetUpdate(true);
    }

    private Image CreateToken(Slot slot)
    {
        GameObject token = new GameObject("Token", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = (RectTransform)token.transform;
        rect.SetParent(canvas, false);
        rect.SetAsLastSibling();

        Image image = token.GetComponent<Image>();
        Image source = slot.icon.GetComponent<Image>();
        image.sprite = source != null ? source.sprite : null;
        image.preserveAspect = true;
        image.raycastTarget = false;

        // Stands out over the light parchment.
        Shadow shadow = token.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
        shadow.effectDistance = new Vector2(4f, -4f);

        // Same size and place as the slot's icon.
        rect.sizeDelta = slot.icon.rect.size;
        rect.localScale = Vector3.one * (slot.icon.lossyScale.y / canvas.lossyScale.y);
        rect.localPosition = CanvasPoint(slot.icon);
        return image;
    }

    // Shared by chips flying in and costs flying out: a short lift-off, then an arc into the destination.
    // The point (an icon's centre) follows the path and the mover is placed around it, keeping its size most
    // of the way and shrinking (or growing) to endScale at the end, so it stays readable in flight.
    private Sequence Travel(RectTransform mover, Vector3 point, System.Func<Vector3> destination, float endScale)
    {
        Vector3 moverFromPoint = mover.localPosition - point;
        float startScale = mover.localScale.x;
        float liftedScale = startScale * liftScale;
        Vector3 lifted = point + Vector3.up * 12f;

        return DOTween.Sequence()
            .Append(DOVirtual.Float(0f, 1f, liftDuration, t =>
            {
                Place(mover, Vector3.Lerp(point, lifted, t), moverFromPoint, Mathf.Lerp(startScale, liftedScale, t), startScale);
            }).SetEase(Ease.OutCubic))
            .Append(DOVirtual.Float(0f, 1f, flightDuration, t =>
            {
                Vector3 end = destination();
                Vector3 control = (lifted + end) * 0.5f + Vector3.up * arcHeight;
                Place(mover, Bezier(lifted, control, end, t), moverFromPoint, Mathf.Lerp(liftedScale, endScale, t * t * t), startScale);
            }).SetEase(Ease.InOutCubic));
    }

    private static void Place(RectTransform mover, Vector3 point, Vector3 moverFromPoint, float scale, float startScale)
    {
        mover.localScale = Vector3.one * scale;
        mover.localPosition = point + moverFromPoint * (scale / startScale);
    }

    // Quadratic curve from a to b, bent towards control.
    private static Vector3 Bezier(Vector3 a, Vector3 control, Vector3 b, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * control + t * t * b;
    }

    private void PunchArrival(Transform target)
    {
        target.DOScale(target.localScale * arrivalPunchScale, punchDuration * 0.5f)
            .SetLoops(2, LoopType.Yoyo).SetEase(Ease.OutQuad)
            .SetLink(target.gameObject).SetId(this).SetUpdate(true);
    }

    private Vector3 CanvasPoint(RectTransform rect)
    {
        return canvas.InverseTransformPoint(rect.TransformPoint(rect.rect.center));
    }

    private static float WorldHeight(RectTransform rect)
    {
        return Mathf.Max(1f, rect.rect.height * rect.lossyScale.y);
    }

    private static string Format(Resource resource, int amount)
    {
        return resource == Resource.Happiness ? $"{amount}%" : amount.ToString();
    }
}
