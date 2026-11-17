using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(45)]
public class ZoneMarkers : MonoBehaviour
{
    [Tooltip("Full-screen rect on a canvas drawn under the game's menus.")]
    [SerializeField] private RectTransform root;
    [Tooltip("A white sprite tinted with each faith's color.")]
    [SerializeField] private Sprite badgeSprite;
    [SerializeField] private TMP_FontAsset font;

    [Header("Badges")]
    [SerializeField] private Vector2 badgeSize = new Vector2(24f, 28f);
    [SerializeField, Range(0.5f, 1f)] private float minorScale = 0.78f;
    [SerializeField, Min(1f)] private float playerScale = 1.18f;
    [SerializeField] private float badgeSpacing = 3f;
    [SerializeField, Min(1)] private int maxBadges = 3;
    [SerializeField] private float numberSize = 17f;
    [Tooltip("Camera height where markers have their normal size; closer, they grow up to the max.")]
    [SerializeField] private float referenceHeight = 5f;
    [SerializeField, Min(1f)] private float maxZoomScale = 1.8f;

    [Header("Owner Change")]
    [SerializeField] private float labelSize = 22f;
    [SerializeField] private float labelRise = 46f;
    [SerializeField, Min(0.1f)] private float labelSeconds = 2.4f;
    [SerializeField] private Color gainedColor = new Color(1f, 0.9f, 0.55f, 1f);
    [SerializeField] private Color lostColor = new Color(1f, 0.36f, 0.3f, 1f);

    public static event System.Action<DistrictZone> RiskShown;

    private readonly Dictionary<DistrictZone, Marker> markers = new Dictionary<DistrictZone, Marker>();
    private readonly List<FactionId> ranked = new List<FactionId>();
    private Camera mapCamera;

    private class Marker
    {
        public RectTransform Rect;
        public Vector3 Anchor;
        public readonly List<Badge> Badges = new List<Badge>();
        public readonly Dictionary<FactionId, int> Clerics = new Dictionary<FactionId, int>();
        public bool Present;
        public TextMeshProUGUI Risk;
    }

    private class Badge
    {
        public RectTransform Rect;
        public Image Image;
        public TMP_Text Number;
        public FactionId Faction;
    }

    private void Start()
    {
        mapCamera = Camera.main;
        if (InfluenceManager.IsNull || root == null)
        {
            enabled = false;
            return;
        }

        foreach (DistrictZone zone in InfluenceManager.Get.GetPlayableZones()) markers[zone] = CreateMarker(zone);

        InfluenceManager.Get.OnControlChanged += Refresh;
        InfluenceManager.Get.OnInfluenceTurnResolved += Refresh;
        TerritoryView.OwnerChanged += OnOwnerChanged;
        DistrictClericAssignPanel.Assigned += RefreshRisk;
        if (!AIIntentBoard.IsNull) AIIntentBoard.Get.OnIntentsChanged += RefreshRisk;
        Refresh();
    }

    private void OnDestroy()
    {
        TerritoryView.OwnerChanged -= OnOwnerChanged;
        DistrictClericAssignPanel.Assigned -= RefreshRisk;
        if (!AIIntentBoard.IsNull) AIIntentBoard.Get.OnIntentsChanged -= RefreshRisk;
        if (InfluenceManager.IsNull) return;
        InfluenceManager.Get.OnControlChanged -= Refresh;
        InfluenceManager.Get.OnInfluenceTurnResolved -= Refresh;
    }

    private void LateUpdate()
    {
        if (mapCamera == null) mapCamera = Camera.main;
        if (mapCamera == null) return;

        float zoomScale = Mathf.Clamp(referenceHeight / Mathf.Max(mapCamera.transform.position.y, 0.01f), 1f, maxZoomScale);
        foreach (Marker marker in markers.Values)
        {
            Vector3 screen = mapCamera.WorldToScreenPoint(marker.Anchor);
            bool onScreen = screen.z > 0f;
            if (marker.Rect.gameObject.activeSelf != onScreen) marker.Rect.gameObject.SetActive(onScreen);
            if (!onScreen) continue;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out Vector2 local);
            marker.Rect.anchoredPosition = local;
            marker.Rect.localScale = Vector3.one * zoomScale;
        }
    }

    private Marker CreateMarker(DistrictZone zone)
    {
        GameObject go = new GameObject($"Marker {zone.SectorName}", typeof(RectTransform));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(root, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = Vector2.zero;

        Bounds bounds = zone.GetWorldBounds();
        return new Marker { Rect = rect, Anchor = bounds.center };
    }

    private void Refresh()
    {
        foreach (KeyValuePair<DistrictZone, Marker> pair in markers) Refresh(pair.Key, pair.Value);
        RefreshRisk();
    }

    private void RefreshRisk()
    {
        foreach (KeyValuePair<DistrictZone, Marker> pair in markers) SetRisk(pair.Key, pair.Value, ZoneRisk.PlayerCouldLose(pair.Key));
    }

    private void SetRisk(DistrictZone zone, Marker marker, bool atRisk)
    {
        bool showing = marker.Risk != null && marker.Risk.gameObject.activeSelf;
        if (atRisk == showing) return;
        if (marker.Risk == null) marker.Risk = CreateRiskMark(marker.Rect);

        RectTransform rect = marker.Risk.rectTransform;
        rect.DOKill();
        rect.localScale = Vector3.one;
        marker.Risk.gameObject.SetActive(atRisk);
        if (!atRisk) return;

        rect.DOScale(1.3f, 0.55f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetLink(rect.gameObject);
        RiskShown?.Invoke(zone);
    }

    private TextMeshProUGUI CreateRiskMark(RectTransform parent)
    {
        TextMeshProUGUI mark = new GameObject("At Risk", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        RectTransform rect = mark.rectTransform;
        rect.SetParent(parent, false);
        rect.sizeDelta = new Vector2(40f, 40f);
        rect.anchoredPosition = new Vector2(0f, badgeSize.y * 1.15f);
        mark.font = font;
        mark.fontSize = labelSize * 1.2f;
        mark.fontStyle = FontStyles.Bold;
        mark.alignment = TextAlignmentOptions.Center;
        mark.color = lostColor;
        mark.outlineWidth = 0.3f;
        mark.outlineColor = new Color(0.08f, 0.05f, 0.03f, 1f);
        mark.raycastTarget = false;
        mark.text = "!";
        return mark;
    }

    private void Refresh(DistrictZone zone, Marker marker)
    {
        ZoneInfluenceState state = zone.Influence;
        ranked.Clear();
        if (state != null)
        {
            foreach (FactionId faction in FactionIdUtil.All)
            {
                if (state.GetShare(faction) > 0 || state.GetClerics(faction) > 0) ranked.Add(faction);
            }
        }

        FactionId? owner = state != null && state.Status == ZoneControlStatus.Controlled ? state.Controller : null;
        ranked.Sort((a, b) =>
        {
            if (owner.HasValue && a == owner.Value) return -1;
            if (owner.HasValue && b == owner.Value) return 1;
            return state.GetShare(b).CompareTo(state.GetShare(a));
        });

        int count = Mathf.Min(ranked.Count, maxBadges);
        while (marker.Badges.Count < count) marker.Badges.Add(CreateBadge(marker.Rect));

        float width = 0f;
        for (int i = 0; i < marker.Badges.Count; i++)
        {
            Badge badge = marker.Badges[i];
            bool used = i < count;
            badge.Rect.gameObject.SetActive(used);
            if (!used) continue;

            FactionId faction = ranked[i];
            int clerics = state.GetClerics(faction);
            float scale = (owner.HasValue && faction == owner.Value ? 1f : minorScale) * (FactionIdUtil.IsPlayer(faction) ? playerScale : 1f);

            badge.Faction = faction;
            badge.Image.color = TerritoryView.MapColor(faction);
            badge.Number.text = clerics > 0 ? clerics.ToString() : string.Empty;
            badge.Rect.sizeDelta = badgeSize * scale;
            width += badge.Rect.sizeDelta.x + (i > 0 ? badgeSpacing : 0f);

            marker.Clerics.TryGetValue(faction, out int before);
            if (marker.Present && clerics > before)
            {
                badge.Rect.DOComplete();
                badge.Rect.DOPunchScale(Vector3.one * 0.45f, 0.45f, 6, 0.6f).SetLink(badge.Rect.gameObject);
            }

            marker.Clerics[faction] = clerics;
        }

        float x = -width * 0.5f;
        for (int i = 0; i < count; i++)
        {
            RectTransform rect = marker.Badges[i].Rect;
            rect.anchoredPosition = new Vector2(x + rect.sizeDelta.x * 0.5f, 0f);
            x += rect.sizeDelta.x + badgeSpacing;
        }

        marker.Present = true;
    }

    private Badge CreateBadge(RectTransform parent)
    {
        GameObject go = new GameObject("Badge", typeof(RectTransform), typeof(Image));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.sprite = badgeSprite;
        image.preserveAspect = true;
        image.raycastTarget = false;

        TextMeshProUGUI number = new GameObject("Clerics", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        RectTransform numberRect = number.rectTransform;
        numberRect.SetParent(rect, false);
        numberRect.anchorMin = Vector2.zero;
        numberRect.anchorMax = Vector2.one;
        numberRect.offsetMin = new Vector2(-6f, -2f);
        numberRect.offsetMax = new Vector2(6f, 4f);
        number.font = font;
        number.fontSize = numberSize;
        number.fontStyle = FontStyles.Bold;
        number.alignment = TextAlignmentOptions.Center;
        number.color = Color.white;
        number.outlineWidth = 0.28f;
        number.outlineColor = new Color(0.08f, 0.05f, 0.03f, 1f);
        number.raycastTarget = false;

        return new Badge { Rect = rect, Image = image, Number = number };
    }

    private void OnOwnerChanged(DistrictZone zone, FactionId? from, FactionId? to)
    {
        bool gained = to.HasValue && FactionIdUtil.IsPlayer(to.Value);
        bool lost = from.HasValue && FactionIdUtil.IsPlayer(from.Value);
        if (!markers.TryGetValue(zone, out Marker marker)) return;

        foreach (Badge badge in marker.Badges)
        {
            if (!badge.Rect.gameObject.activeSelf) continue;
            badge.Rect.DOComplete();
            badge.Rect.DOPunchScale(Vector3.one * 0.6f, 0.6f, 5, 0.5f).SetLink(badge.Rect.gameObject);
        }

        if (!gained && !lost) return;

        ShowLabel(marker, gained ? "ZONE TAKEN" : "ZONE LOST", gained ? gainedColor : lostColor);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayOutcomeResources(gained, lost);
    }

    private void ShowLabel(Marker marker, string text, Color color)
    {
        TextMeshProUGUI label = new GameObject("Owner Change", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        RectTransform rect = label.rectTransform;
        rect.SetParent(marker.Rect, false);
        rect.sizeDelta = new Vector2(260f, 40f);
        rect.anchoredPosition = new Vector2(0f, badgeSize.y);
        label.font = font;
        label.fontSize = labelSize;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = color;
        label.outlineWidth = 0.3f;
        label.outlineColor = new Color(0.08f, 0.05f, 0.03f, 1f);
        label.raycastTarget = false;
        label.text = text;

        rect.localScale = Vector3.one * 0.4f;
        DOTween.Sequence()
            .Append(rect.DOScale(1f, 0.35f).SetEase(Ease.OutBack))
            .Join(rect.DOAnchorPosY(badgeSize.y + labelRise, labelSeconds).SetEase(Ease.OutCubic))
            .Insert(labelSeconds * 0.6f, label.DOFade(0f, labelSeconds * 0.4f))
            .OnComplete(() => Destroy(label.gameObject))
            .SetLink(label.gameObject);
    }
}
