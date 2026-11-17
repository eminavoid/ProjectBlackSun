using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zeke.Tooltips;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class DistrictClericAssignPanel : MonoBehaviour
{
    [SerializeField] private RectTransform panel;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text counts;
    [Tooltip("Influence, next turn's gain and who else is here.")]
    [SerializeField] private TMP_Text forecast;
    [SerializeField] private Button assignButton;
    [SerializeField] private Button withdrawButton;
    [SerializeField] private float hiddenDrop = 170f;
    [SerializeField, Min(0.01f)] private float slideDuration = 0.3f;

    public static event System.Action Assigned;

    private DistrictZone shown;
    private PlayerClergy clergy;
    private bool visible = true;
    private float restY;

    public Button AssignButton => assignButton;
    public RectTransform Plate => panel;

    public void Reveal()
    {
        if (visible && shown == DistrictSelectionController.SelectedZone) return;
        visible = true;
        Show(DistrictSelectionController.SelectedZone);
    }

    private void Awake()
    {
        clergy = FindAnyObjectByType<PlayerClergy>();
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
        ZoneInfluenceState state = shown.Influence;
        int here = state.GetClerics(FactionId.Player);
        int reserve = manager.GetClericPool(FactionId.Player);
        int perCleric = clergy != null ? clergy.UpkeepPerCleric : 0;

        title.text = $"{MapStatsQuery.ZoneLabel(shown)}  ·  {MapStatsQuery.DistrictLabel(shown.District)} district  ·  {RulerText(state)}";
        string upkeep = perCleric > 0 ? $"  ·  Upkeep: {manager.CountDeployedClerics(FactionId.Player) * perCleric} Wealth a turn" : string.Empty;
        SetLinked(counts, $"Clerics here: {here}  ·  Reserve: {reserve}{upkeep}");
        if (forecast != null) SetLinked(forecast, Forecast(manager, state, here));

        SetButtonTip(assignButton, AssignTip(manager, state, here, reserve, perCleric));
        SetButtonTip(withdrawButton, here > 0
            ? $"Recall a cleric to your reserve.{(perCleric > 0 ? $"\n<size=80%>Saves {perCleric} Wealth a turn.</size>" : string.Empty)}"
            : "You have no clerics here to recall.");
    }

    private static string RulerText(ZoneInfluenceState state)
    {
        if (state.Status == ZoneControlStatus.Controlled && state.Controller.HasValue)
        {
            return FactionIdUtil.IsPlayer(state.Controller.Value) ? "Ruled by you" : $"Ruled by {FactionIdUtil.DisplayName(state.Controller.Value)}";
        }

        return state.HasAnyPresence ? "Contested" : "Free";
    }

    private string Forecast(InfluenceManager manager, ZoneInfluenceState state, int here)
    {
        int total = Mathf.Max(1, state.TotalInfluence);
        int share = state.GetShare(FactionId.Player);
        string mine = share > 0
            ? $"Your influence: {Mathf.RoundToInt(share * 100f / total)}% ({share} of {state.TotalInfluence})."
            : "You have no influence here yet.";
        string next = here > 0 ? $" Next turn: {GainText(manager, here)} influence."
            : share > 0 ? " With no clerics here, it won't grow."
            : manager.InClericReach(shown, FactionId.Player) ? " Send a cleric to start gaining influence."
            : " Too far: clerics can only go to zones next to one you rule.";
        bool yours = state.Status == ZoneControlStatus.Controlled && state.Controller == FactionId.Player;

        var rivals = new List<string>();
        foreach (FactionId faction in FactionIdUtil.All)
        {
            if (FactionIdUtil.IsPlayer(faction)) continue;
            int rivalShare = state.GetShare(faction);
            int rivalClerics = state.GetClerics(faction);
            if (rivalShare <= 0 && rivalClerics <= 0) continue;
            rivals.Add($"{FactionIdUtil.DisplayName(faction)} {Mathf.RoundToInt(rivalShare * 100f / total)}% ({rivalClerics} {(rivalClerics == 1 ? "cleric" : "clerics")})");
        }

        string others = rivals.Count > 0
            ? $"\n{string.Join(",  ", rivals)}. {(yours ? "You keep it while you hold more than half its influence." : "Rule it by holding more than half its influence.")}"
            : "\nNo rival faith is here.";
        return mine + next + others;
    }

    private string GainText(InfluenceManager manager, int clerics)
    {
        Vector2Int range = ClericInfluenceTable.GetRange(clerics);
        int bonus = InfluenceManager.OutnumberBonus(shown != null ? shown.Influence : null, FactionId.Player, clerics);
        float multiplier = manager.GetInfluenceGenerationMultiplier(FactionId.Player, shown);
        int low = Mathf.RoundToInt((range.x + bonus) * multiplier);
        int high = Mathf.RoundToInt((range.y + bonus) * multiplier);
        return low == high ? $"+{low}" : $"+{low} to +{high}";
    }

    private string AssignTip(InfluenceManager manager, ZoneInfluenceState state, int here, int reserve, int perCleric)
    {
        if (reserve <= 0) return "Your reserve is empty.\n<size=80%>Ordain clerics from your cult's Clergy page, or wait for new ones to train.</size>";
        if (!state.CanEnterByNormalMeans(FactionId.Player)) return "This zone is full and your faith has no foothold here.";
        if (!manager.InClericReach(shown, FactionId.Player)) return "Too far.\n<size=80%>Clerics can only go to zones next to one you rule.</size>";

        string gain = here > 0
            ? $"Next turn here: {GainText(manager, here + 1)} influence instead of {GainText(manager, here)}."
            : $"Next turn here: {GainText(manager, 1)} influence.";
        string cost = perCleric > 0 ? $"\n<size=80%>Costs {perCleric} Wealth a turn while it stays on the map.</size>" : string.Empty;
        return $"Send a cleric from your reserve.\n<size=80%>{gain}</size>{cost}";
    }

    private static void SetButtonTip(Button button, string text)
    {
        if (button == null) return;
        TooltipTrigger trigger = button.GetComponent<TooltipTrigger>();
        if (trigger == null) trigger = button.gameObject.AddComponent<TooltipTrigger>();
        trigger.SetText(text);
    }

    private static void SetLinked(TMP_Text text, string value)
    {
        TooltipLinksDB glossary = Tooltip.Glossary;
        text.text = glossary != null ? glossary.Highlight(value) : value;
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
        if (amount > 0) Assigned?.Invoke();
        counts.transform.DOComplete();
        counts.transform.DOPunchScale(Vector3.one * 0.08f, 0.25f, 6, 0.6f).SetLink(gameObject);
    }
}
