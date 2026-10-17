using System.Collections.Generic;
using UnityEngine;

public class TurnChronicle : MonoBehaviour
{
    private enum Phase
    {
        Idle,
        SettlingEvents,
        Resolving
    }

    [SerializeField] private ChronicleWindow windowPrefab;
    [Tooltip("Open the chronicle every turn, even when only resources changed.")]
    [SerializeField] private bool alwaysOpen;
    [Tooltip("Warn when the flock ends the turn at or below this amount. Losing the whole flock loses the game.")]
    [SerializeField, Min(0)] private int lowFlockWarning = 30;
    [Tooltip("Rival faiths mentioned at most, the ones that spread the most first.")]
    [SerializeField, Min(0)] private int maxRivalLines = 2;
    [SerializeField, Min(0.5f)] private float quietNoticeSeconds = 5f;

    private readonly Dictionary<DistrictZone, FactionId?> controllersBefore = new Dictionary<DistrictZone, FactionId?>();
    private Dictionary<Resource, int> resourcesBefore;
    private FactionId? leaderBefore;
    private int flockBefore;
    private readonly List<SettledEvent> settled = new List<SettledEvent>();
    private int ripenedEvents;
    private bool explainedEvents;
    private bool explainedSettling;
    private Phase phase;

    private static PlayerResources Wallet => ResourceManager.Resources;

    private void OnEnable()
    {
        GameTime.OnTurnResolving += BeginTurn;
        GameTime.OnTurnEnding += EndEventSettling;
        GameTime.OnTurnResolved += Publish;
        SeedEventManager.onEventSettled += CollectSettled;
        SeedEventManager.onSeedStored += CountRipened;
    }

    private void OnDisable()
    {
        GameTime.OnTurnResolving -= BeginTurn;
        GameTime.OnTurnEnding -= EndEventSettling;
        GameTime.OnTurnResolved -= Publish;
        SeedEventManager.onEventSettled -= CollectSettled;
        SeedEventManager.onSeedStored -= CountRipened;
    }

    private void BeginTurn()
    {
        phase = Phase.SettlingEvents;
        settled.Clear();
        ripenedEvents = 0;

        controllersBefore.Clear();
        foreach (DistrictZone zone in PlayableZones()) controllersBefore[zone] = ControllerOf(zone);
        leaderBefore = Leader();
        flockBefore = Wallet.GetResourceAmount(Resource.Flock);
        resourcesBefore = Wallet.Snapshot();
    }

    private void EndEventSettling()
    {
        if (phase != Phase.SettlingEvents) return;
        phase = Phase.Resolving;
        resourcesBefore = Wallet.Snapshot();
    }

    private void CollectSettled(SettledEvent settledEvent)
    {
        if (phase == Phase.SettlingEvents) settled.Add(settledEvent);
    }

    private void CountRipened(Seed seed)
    {
        if (phase == Phase.Resolving) ripenedEvents++;
    }

    private void Publish()
    {
        if (phase == Phase.Idle) return;
        phase = Phase.Idle;

        var news = new List<string>();
        var rivalNews = new List<string>();
        AddTerritoryNews(news, rivalNews);
        AddLeaderNews(news);
        AddEventNews(news);
        AddFlockWarning(news);

        var yield = new List<ResourceDelta>();
        Wallet.GetChangesSince(resourcesBefore, yield);

        int endedTurn = GameTime.CurrentTurn - 1;
        if (news.Count > 0 || settled.Count > 0 || (alwaysOpen && (yield.Count > 0 || rivalNews.Count > 0)))
        {
            news.AddRange(rivalNews);
            ChronicleWindow window = Instantiate(windowPrefab, GlobalReferences.ScreenCanvas.transform);
            window.Show(endedTurn, news, yield, new List<SettledEvent>(settled));
            return;
        }

        var notice = new List<string>();
        if (yield.Count > 0) notice.Add(FormatYield(yield) + ".");
        if (rivalNews.Count > 0) notice.Add(StripColor(rivalNews[0]));
        if (notice.Count > 0) TopScreenNotification.Show($"Turn {endedTurn} ended: {string.Join(" ", notice)}", quietNoticeSeconds);
    }

    private void AddTerritoryNews(List<string> news, List<string> rivalNews)
    {
        var gained = new List<DistrictZone>();
        var lost = new List<DistrictZone>();
        var takers = new HashSet<FactionId?>();
        var rivalGrowth = new Dictionary<FactionId, int>();

        foreach (KeyValuePair<DistrictZone, FactionId?> pair in controllersBefore)
        {
            if (pair.Key == null) continue;
            FactionId? before = pair.Value;
            FactionId? after = ControllerOf(pair.Key);
            if (before == after) continue;

            if (after == FactionId.Player) gained.Add(pair.Key);
            if (before == FactionId.Player)
            {
                lost.Add(pair.Key);
                takers.Add(after);
            }

            if (before.HasValue && before != FactionId.Player) AddGrowth(rivalGrowth, before.Value, -1);
            if (after.HasValue && after != FactionId.Player) AddGrowth(rivalGrowth, after.Value, 1);
        }

        if (gained.Count > 0)
        {
            news.Add($"Your faith took control of {Zones(gained.Count)} in {DistrictsOf(gained)}.");
        }

        if (lost.Count > 0)
        {
            if (takers.Count == 1 && !takers.Contains(null))
            {
                FactionId taker = default;
                foreach (FactionId? t in takers) taker = t.Value;
                news.Add($"{Name(taker)} took {Zones(lost.Count)} from you in {DistrictsOf(lost)}.");
            }
            else if (takers.Count == 1)
            {
                news.Add($"You lost your hold on {Zones(lost.Count)} in {DistrictsOf(lost)}.");
            }
            else
            {
                news.Add($"Rival faiths took {Zones(lost.Count)} from you in {DistrictsOf(lost)}.");
            }
        }

        var spreading = new List<KeyValuePair<FactionId, int>>();
        foreach (KeyValuePair<FactionId, int> pair in rivalGrowth)
        {
            if (pair.Value > 0) spreading.Add(pair);
        }

        spreading.Sort((a, b) => b.Value.CompareTo(a.Value));
        for (int i = 0; i < spreading.Count && i < maxRivalLines; i++)
        {
            int spread = spreading[i].Value;
            rivalNews.Add($"{Name(spreading[i].Key)} spread to {spread} more {(spread == 1 ? "zone" : "zones")}.");
        }
    }

    private void AddLeaderNews(List<string> news)
    {
        FactionId? leader = Leader();
        if (leader == leaderBefore) return;

        if (leader == FactionId.Player) news.Add("Your faith now controls the most zones in the city.");
        else if (leader.HasValue) news.Add($"{Name(leader.Value)} now controls the most zones in the city.");
        else if (leaderBefore.HasValue) news.Add("No faith controls the most zones anymore: the lead is tied.");
    }

    private void AddEventNews(List<string> news)
    {
        if (ripenedEvents > 0)
        {
            string events = ripenedEvents == 1 ? "1 new event awaits" : $"{ripenedEvents} new events await";
            news.Add(explainedEvents
                ? $"{events} your decision."
                : $"Seeds ripen into events. {events} your decision in the scroll on the left.");
            explainedEvents = true;
        }

        if (settled.Count > 0 && !explainedSettling)
        {
            string events = settled.Count == 1 ? "1 event" : $"{settled.Count} events";
            news.Add($"You ended the turn with {events} pending, so your advisors settled {(settled.Count == 1 ? "it" : "them")} with the default choice. Read what happened below.");
            explainedSettling = true;
        }
    }

    private void AddFlockWarning(List<string> news)
    {
        int flock = Wallet.GetResourceAmount(Resource.Flock);
        if (flock <= 0 || flock > lowFlockWarning) return;
        if (flock >= flockBefore && flockBefore <= lowFlockWarning) return;

        news.Add($"Your flock is dwindling ({flock} left). If it reaches 0, your faith is lost.");
    }

    private static string FormatYield(List<ResourceDelta> yield)
    {
        var parts = new List<string>();
        for (int i = 0; i < yield.Count; i++)
        {
            parts.Add($"{(yield[i].Amount > 0 ? "+" : "")}{yield[i].Amount} {yield[i].Resource}");
        }

        return string.Join(", ", parts);
    }

    private static void AddGrowth(Dictionary<FactionId, int> growth, FactionId faction, int amount)
    {
        growth.TryGetValue(faction, out int current);
        growth[faction] = current + amount;
    }

    private static string Zones(int count) => count == 1 ? "1 zone" : $"{count} zones";

    private static string DistrictsOf(List<DistrictZone> zones)
    {
        var names = new List<string>();
        foreach (DistrictZone zone in zones)
        {
            string name = MapStatsQuery.DistrictLabel(zone.District);
            if (!names.Contains(name)) names.Add(name);
        }

        if (names.Count == 1) return $"the {names[0]} district";
        string last = names[names.Count - 1];
        names.RemoveAt(names.Count - 1);
        return $"the {string.Join(", ", names)} and {last} districts";
    }

    private static string Name(FactionId faction)
    {
        Color ink = Color.Lerp(FactionPalette.For(faction), Color.black, 0.45f);
        return $"<color=#{ColorUtility.ToHtmlStringRGB(ink)}>{FactionIdUtil.DisplayName(faction)}</color>";
    }

    private static string StripColor(string text)
    {
        return System.Text.RegularExpressions.Regex.Replace(text, "<.*?>", string.Empty);
    }

    private static IReadOnlyList<DistrictZone> PlayableZones()
    {
        return InfluenceManager.IsNull ? new List<DistrictZone>() : InfluenceManager.Get.GetPlayableZones();
    }

    private static FactionId? ControllerOf(DistrictZone zone)
    {
        ZoneInfluenceState state = zone != null ? zone.Influence : null;
        return state != null && state.Status == ZoneControlStatus.Controlled ? state.Controller : null;
    }

    private static FactionId? Leader()
    {
        return InfluenceManager.IsNull ? null : InfluenceManager.Get.GetFaithEclipseLeader(out _);
    }
}
