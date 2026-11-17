using System;
using System.Collections.Generic;
using UnityEngine;

public class FirstTimeHints : MonoBehaviour
{
    public const string Goal = "goal";
    public const string FirstEvent = "firstEvent";
    public const string ZoneTaken = "zoneTaken";
    private const string ZoneLost = "zoneLost";
    private const string RivalNear = "rivalNear";
    private const string ReserveEmpty = "reserveEmpty";
    private const string Debt = "debt";
    private const string DistrictRuled = "district";
    private const string AtRisk = "atRisk";
    private const string SecretsOrdain = "secretsOrdain";
    private const string TheologyReady = "theologyReady";
    private const string HappinessChanged = "happiness";

    private const string EnabledKey = "hints.enabled";
    private const string SeenKey = "hints.seen";

    private static HashSet<string> seen;

    private readonly Queue<(string key, string text)> queue = new Queue<(string key, string text)>();
    private bool showing;
    private bool resolving;
    private InfluenceManager influence;
    private PlayerClergy clergy;
    private TheologyMenu theology;
    private int lastHappiness = -1;

    public static bool Enabled
    {
        get => PlayerPrefs.GetInt(EnabledKey, 1) == 1;
        set
        {
            PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    private static bool Muted => !Enabled || TutorialSession.SuppressAmbientEvents;

    private static HashSet<string> SeenKeys => seen ??= new HashSet<string>(
        PlayerPrefs.GetString(SeenKey, string.Empty).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));

    public static void MarkSeen(string key)
    {
        if (!SeenKeys.Add(key)) return;
        PlayerPrefs.SetString(SeenKey, string.Join(",", SeenKeys));
        PlayerPrefs.Save();
    }

    public static void ResetSeen()
    {
        seen = new HashSet<string>();
        PlayerPrefs.DeleteKey(SeenKey);
        PlayerPrefs.Save();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ForgetCache() => seen = null;

    private void OnEnable()
    {
        TerritoryView.OwnerChanged += OnOwnerChanged;
        GameTime.OnTurnResolving += OnTurnResolving;
        GameTime.OnTurnResolved += OnTurnResolved;
        SeedEventManager.onSeedStored += OnSeedStored;
        ZoneMarkers.RiskShown += OnRiskShown;
    }

    private void OnDisable()
    {
        TerritoryView.OwnerChanged -= OnOwnerChanged;
        GameTime.OnTurnResolving -= OnTurnResolving;
        GameTime.OnTurnResolved -= OnTurnResolved;
        SeedEventManager.onSeedStored -= OnSeedStored;
        ZoneMarkers.RiskShown -= OnRiskShown;
        if (influence != null) influence.OnInfluenceTurnResolved -= OnInfluenceResolved;
        influence = null;
    }

    private void Update()
    {
        if (influence == null && !InfluenceManager.IsNull)
        {
            influence = InfluenceManager.Get;
            influence.OnInfluenceTurnResolved += OnInfluenceResolved;
        }

        if (showing)
        {
            if (TopScreenNotification.IsHolding) return;
            showing = false;
        }

        if (Muted)
        {
            queue.Clear();
            return;
        }

        if (TutorialSession.CultChosen || TutorialSession.Skipped)
            Raise(Goal, $"Your goal: rule the most zones when the Faith Eclipse comes on turn {EclipseTurn()}. Select a zone and send clerics to spread your influence.");

        if (influence != null && influence.GetClericPool(FactionId.Player) <= 0 && influence.CountDeployedClerics(FactionId.Player) > 0)
            Raise(ReserveEmpty, "Your reserve is empty. Ordain clerics from your cult's Clergy page, wait for new ones to train, or recall some from the map.");

        RaiseResourceHints();

        if (queue.Count == 0 || !Calm()) return;

        (string key, string text) = queue.Dequeue();
        MarkSeen(key);
        showing = true;
        TopScreenNotification.Hold(text, true, Dismiss, "GOT IT", "HINT");
    }

    private void RaiseResourceHints()
    {
        if (ResourceManager.IsNull || ResourceManager.Resources == null) return;
        PlayerResources wallet = ResourceManager.Resources;

        if (clergy == null) clergy = FindAnyObjectByType<PlayerClergy>();
        if (clergy != null && wallet.GetResourceAmount(Resource.Secrets) >= clergy.OrdainSecretsPrice)
            Raise(SecretsOrdain, $"You hold {clergy.OrdainSecretsPrice} Secrets or more: enough to ordain a cleric at once from your cult's Clergy page.");

        if (theology == null) theology = FindAnyObjectByType<TheologyMenu>(FindObjectsInactive.Include);
        if (theology != null && theology.NextCost > 0 && wallet.GetResourceAmount(Resource.Zeal) >= theology.NextCost)
            Raise(TheologyReady, "You have enough Zeal to commune with the Beholder. A ! on Theology marks it; open it for seeds, doctrines and blessings.");

        int happiness = wallet.GetResourceAmount(Resource.Happiness);
        if (lastHappiness >= 0 && happiness != lastHappiness)
            Raise(HappinessChanged, "Your faithful's Happiness changed. Content faithful tithe more: at 100% Happiness your zones yield a quarter more Wealth.");
        lastHappiness = happiness;
    }

    private void OnRiskShown(DistrictZone zone)
    {
        Raise(AtRisk, $"A red ! over {ZoneName(zone)} means the rivals' planned moves could take it next turn. Send more clerics there to hold it.");
    }

    private void Raise(string key, string text)
    {
        if (Muted || SeenKeys.Contains(key)) return;
        foreach ((string queued, string _) in queue)
        {
            if (queued == key) return;
        }

        queue.Enqueue((key, text));
    }

    private void Dismiss()
    {
        showing = false;
        TopScreenNotification.Release();
    }

    private bool Calm()
    {
        if (resolving || TopScreenNotification.IsHolding) return false;
        if (SeedEventManager.OptionsOpen || SeedEventManager.OutputWindow != null) return false;
        if (FindAnyObjectByType<ChronicleWindow>() != null) return false;

        CultCreationPanel cult = FindAnyObjectByType<CultCreationPanel>();
        if (cult != null && !cult.Confirmed) return false;

        MapCameraController camera = MapCameraController.Instance;
        return camera == null || camera.enabled;
    }

    private void OnOwnerChanged(DistrictZone zone, FactionId? before, FactionId? after)
    {
        if (zone == null) return;

        if (after == FactionId.Player)
            Raise(ZoneTaken, $"You now rule {ZoneName(zone)}: more than half its influence is yours. Every zone you rule pays tithes and feeds one of your clerics.");
        else if (before == FactionId.Player)
            Raise(ZoneLost, $"You lost {ZoneName(zone)}: you no longer hold more than half its influence. Send more clerics than the rival has there to win it back.");
    }

    private void OnSeedStored(Seed seed)
    {
        if (seed == null || TutorialContent.IsTutorialSeed(seed)) return;
        Raise(FirstEvent, "A new event needs your answer. Open the scroll on the left. If you end the turn first, your advisors pick the default answer.");
    }

    private void OnTurnResolving() => resolving = true;

    private void OnTurnResolved()
    {
        resolving = false;
        foreach (Resource resource in Enum.GetValues(typeof(Resource)))
        {
            if (!SeedEventManager.IsInDebt(resource)) continue;
            Raise(Debt, $"Your {resource} has fallen into debt. While in debt, some of your faithful leave every turn and a debt crisis may strike.");
            return;
        }
    }

    private void OnInfluenceResolved()
    {
        if (influence == null) return;

        RaiseRivalNear();
        foreach (Districts district in Enum.GetValues(typeof(Districts)))
        {
            if (influence.GetDistrictController(district) != FactionId.Player) continue;

            float multiplier = influence.ProductionConfig != null ? influence.ProductionConfig.DistrictControlProductionMultiplier : 1f;
            string bonus = multiplier > 1f
                ? $" Its zones now yield you {Mathf.RoundToInt((multiplier - 1f) * 100f)}% more tithes."
                : string.Empty;
            Raise(DistrictRuled, $"You rule more than half of the {MapStatsQuery.DistrictLabel(district)} district.{bonus}");
            return;
        }
    }

    private void RaiseRivalNear()
    {
        IReadOnlyList<DistrictZone> zones = influence.GetPlayableZones();
        for (int i = 0; i < zones.Count; i++)
        {
            ZoneInfluenceState state = zones[i] != null ? zones[i].Influence : null;
            if (state == null || state.Status != ZoneControlStatus.Controlled || state.Controller != FactionId.Player) continue;

            foreach (FactionId faction in FactionIdUtil.All)
            {
                if (FactionIdUtil.IsPlayer(faction) || state.GetClerics(faction) <= 0) continue;
                Raise(RivalNear, $"The {FactionIdUtil.DisplayName(faction)} is spreading into {ZoneName(zones[i])}, a zone you rule. If they pass half its influence, it becomes theirs. Keep more clerics there than they do.");
                return;
            }
        }
    }

    private static string ZoneName(DistrictZone zone)
    {
        return $"{MapStatsQuery.ZoneLabel(zone)} ({MapStatsQuery.DistrictLabel(zone.District)})";
    }

    private static int EclipseTurn()
    {
        GameLoopEvents loop = FindAnyObjectByType<GameLoopEvents>();
        return loop != null ? loop.EclipseTurn : 30;
    }
}
