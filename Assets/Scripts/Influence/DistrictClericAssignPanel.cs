using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class DistrictClericAssignPanel : MonoBehaviour
{
    [SerializeField] private RectTransform panel;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text counts;
    [SerializeField] private Button assignButton;
    [SerializeField] private Button withdrawButton;
    [SerializeField] private float hiddenDrop = 170f;
    [SerializeField, Min(0.01f)] private float slideDuration = 0.3f;

    private DistrictZone shown;
    private bool visible = true;
    private float restY;

    private void Awake()
    {
        restY = panel.anchoredPosition.y;
        panel.anchoredPosition = new Vector2(panel.anchoredPosition.x, restY - hiddenDrop);
        group.alpha = 0f;
        group.blocksRaycasts = false;
        assignButton.onClick.AddListener(() => Change(1));
        withdrawButton.onClick.AddListener(() => Change(-1));
    }

    private void OnEnable()
    {
        DistrictSelectionController.OnSelectionChanged += OnSelectionChanged;
        GameTime.OnTurnResolved += Refresh;
    }

    private void OnDisable()
    {
        DistrictSelectionController.OnSelectionChanged -= OnSelectionChanged;
        GameTime.OnTurnResolved -= Refresh;
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.cKey.wasPressedThisFrame) return;
#else
        if (!Input.GetKeyDown(KeyCode.C)) return;
#endif
        visible = !visible;
        Show(visible ? DistrictSelectionController.SelectedZone : null);
    }

    private void OnSelectionChanged(Districts? district)
    {
        if (visible) Show(DistrictSelectionController.SelectedZone);
    }

    private void Show(DistrictZone zone)
    {
        if (zone != null && !zone.IsPlayable) zone = null;
        bool wasShown = shown != null;
        shown = zone;
        Refresh();
        if (wasShown == (zone != null)) return;

        panel.DOKill();
        group.DOKill();
        group.blocksRaycasts = zone != null;
        panel.DOAnchorPosY(zone != null ? restY : restY - hiddenDrop, slideDuration).SetEase(zone != null ? Ease.OutBack : Ease.InCubic).SetLink(gameObject);
        group.DOFade(zone != null ? 1f : 0f, slideDuration * 0.8f).SetLink(gameObject);
    }

    private void Refresh()
    {
        if (shown == null || InfluenceManager.IsNull) return;

        shown.EnsureInfluenceState();
        InfluenceManager manager = InfluenceManager.Get;
        title.text = $"{MapStatsQuery.ZoneLabel(shown)}  ·  {MapStatsQuery.DistrictLabel(shown.District)} district";
        counts.text = $"Your clerics here: {shown.Influence.GetClerics(FactionId.Player)}     In reserve: {manager.GetClericPool(FactionId.Player)}";
    }

    private void Change(int amount)
    {
        if (shown == null || InfluenceManager.IsNull) return;

        if (!InfluenceManager.Get.TryAssignClerics(shown, FactionId.Player, amount, out string error))
        {
            panel.DOComplete();
            panel.DOShakeAnchorPos(0.3f, new Vector2(10f, 0f), 18, 0f).SetLink(gameObject);
            if (!string.IsNullOrEmpty(error)) TopScreenNotification.Show(error, 3f);
            return;
        }

        Refresh();
        counts.transform.DOComplete();
        counts.transform.DOPunchScale(Vector3.one * 0.08f, 0.25f, 6, 0.6f).SetLink(gameObject);
    }
}
