using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CultLedger : MonoBehaviour
{
    public enum Page
    {
        Cult,
        Clergy,
        Rivals,
        Doctrines
    }

    [SerializeField] private MapStatsBook book;
    [SerializeField] private TMP_Text coverKicker;
    [SerializeField] private TMP_Text coverTitle;

    private PlayerClergy clergy;
    private GameLoopEvents gameLoop;
    private MapCameraController mapCamera;
    private DoctrinesController doctrines;
    private Page page;

    private void OnEnable() => GameTime.OnTurnResolved += RefreshIfOpen;

    private void OnDisable() => GameTime.OnTurnResolved -= RefreshIfOpen;

    private void Start()
    {
        clergy = FindAnyObjectByType<PlayerClergy>();
        gameLoop = FindAnyObjectByType<GameLoopEvents>();
        mapCamera = FindAnyObjectByType<MapCameraController>();
        doctrines = FindAnyObjectByType<DoctrinesController>(FindObjectsInactive.Include);
        if (!InfluenceManager.IsNull) InfluenceManager.Get.OnControlChanged += RefreshIfOpen;
        if (coverTitle != null)
        {
            coverTitle.fontSizeMax = coverTitle.fontSize;
            coverTitle.fontSizeMin = coverTitle.fontSize * 0.5f;
            coverTitle.enableAutoSizing = true;
        }
    }

    private void OnDestroy()
    {
        if (!InfluenceManager.IsNull) InfluenceManager.Get.OnControlChanged -= RefreshIfOpen;
    }

    public void OpenCult() => Show(Page.Cult);

    public void OpenClergy() => Show(Page.Clergy);

    public void OpenRivals() => Show(Page.Rivals);

    public void OpenDoctrines() => Show(Page.Doctrines);

    public bool IsShowing(Page target) => book.IsOpen && page == target;

    public Component Book => book;

    public void Close() => book.Close();

    private void Show(Page target)
    {
        bool opening = !book.IsOpen;
        if (!opening && target == page)
        {
            book.Close();
            return;
        }

        page = target;
        if (MapStatsPanel.Instance != null && MapStatsPanel.Instance.IsOpen) MapStatsPanel.Instance.Close();
        book.Open(null);
        Render(true, !opening);
    }

    private void RefreshIfOpen()
    {
        if (book.IsOpen) Render(false, false);
    }

    private void Render(bool animate, bool turn)
    {
        if (InfluenceManager.IsNull) return;

        book.SelectTab(null);
        book.BeginPage(turn);
        switch (page)
        {
            case Page.Cult: RenderCult(animate); break;
            case Page.Clergy: RenderClergy(animate); break;
            case Page.Doctrines: RenderDoctrines(animate); break;
            default: RenderRivals(animate); break;
        }

        book.EndPage(animate);
    }

    private void RenderCult(bool animate)
    {
        MapStatsGlobalSnapshot city = MapStatsQuery.CaptureGlobal();
        string name = CultName();
        int zones = Row(city, FactionId.Player)?.ControlledZones ?? 0;
        FactionId? leader = city.FaithEclipseLeader;

        SetCover("THE CREED OF", name);
        book.SetHeading(name == "Your Cult" ? string.Empty : "YOUR CULT", name.ToUpperInvariant(), $"{TurnLine()}  ·  {Count(zones, "zone")} ruled  ·  {Ordinal(Rank(city, zones))} of {city.Factions.Count} faiths");
        if (leader == FactionId.Player) book.SetSeal(FactionId.Player, "LEADS THE ECLIPSE", name, animate);
        else book.SetSeal(FactionId.Player, "THE ECLIPSE", leader.HasValue ? $"{FactionIdUtil.DisplayName(leader.Value)} leads" : "No faith leads", animate);

        book.AddSection("STANDING");
        book.AddText(StandingLine(city, zones));

        book.AddSection("VIRTUES");
        PlayerStats stats = InfluenceManager.Get.Stats;
        foreach (PlayerStats.PlayerStat virtue in Virtues.All) AddVirtue(stats, virtue);

        book.AddSection("DOCTRINES");
        if (doctrines == null || doctrines.Kept.Count == 0) book.AddText("Your cult keeps no doctrine yet.");
        else foreach (Doctrine doctrine in doctrines.Kept) book.AddEntry(null, Color.clear, doctrine.Name, StatChanges(doctrine));
        if (doctrines != null) book.AddAction("SHAPE YOUR DOCTRINES", OpenDoctrines);

        book.AddSection("TREASURY");
        if (clergy != null && clergy.UpkeepPerCleric > 0)
        {
            int unfed = clergy.UnfedClerics(InfluenceManager.Get);
            book.AddText(unfed > 0
                ? $"{Count(unfed, "cleric")} on the map {(unfed == 1 ? "costs" : "cost")} {unfed * clergy.UpkeepPerCleric} Wealth per turn. Your zones and faithful feed the rest."
                : "Your zones and faithful feed every cleric you have on the map.");
        }

        bool debts = false;
        foreach (Resource resource in System.Enum.GetValues(typeof(Resource)))
        {
            if (!SeedEventManager.IsInDebt(resource)) continue;
            debts = true;
            book.AddText($"In debt: {ResourceManager.Resources.GetResourceAmount(resource)} {resource}. Faithful leave each turn until it is paid.");
        }

        if (!debts) book.AddText("Your cult owes nothing.");
    }

    private void RenderClergy(bool animate)
    {
        InfluenceManager influence = InfluenceManager.Get;
        string name = CultName();
        int field = influence.CountDeployedClerics(FactionId.Player);
        int reserve = influence.GetClericPool(FactionId.Player);
        int upkeepEach = clergy != null ? clergy.UpkeepPerCleric : 0;

        SetCover("THE CLERGY OF", name);
        book.SetHeading("YOUR CLERGY", "CLERICS", $"{field} in the field  ·  {reserve} in reserve  ·  {(clergy != null ? clergy.UnfedClerics(influence) : field) * upkeepEach} Wealth per turn");

        book.AddSection("ORDINATION");
        if (clergy != null)
        {
            int perTurn = Mathf.Max(1, clergy.TrainingPerTurn);
            int turns = Mathf.Max(1, Mathf.CeilToInt((clergy.OrdinationCost - clergy.Training) / (float)perTurn));
            book.SetSeal(FactionId.Player, "NEXT CLERIC", turns == 1 ? "Next turn" : $"In {turns} turns", animate);
            book.AddText($"Each turn your cult trains {perTurn} points: twice its Learning plus its Stewardship, halved and rounded up. {clergy.Training}/{clergy.OrdinationCost} toward the next cleric.");
            book.AddAction($"ORDAIN NOW  ·  {clergy.OrdainPrice} WEALTH", Ordain);
            book.AddAction($"ORDAIN NOW  ·  {clergy.OrdainSecretsPrice} SECRETS", OrdainWithSecrets);
        }
        else
        {
            book.SetSeal(FactionId.Player, "YOUR CLERGY", name, animate);
        }

        book.AddSection("IN THE FIELD");
        var posted = new List<DistrictZone>();
        foreach (DistrictZone zone in influence.GetPlayableZones())
        {
            if (zone.Influence != null && zone.Influence.GetClerics(FactionId.Player) > 0) posted.Add(zone);
        }

        posted.Sort((a, b) => b.Influence.GetClerics(FactionId.Player).CompareTo(a.Influence.GetClerics(FactionId.Player)));
        if (posted.Count == 0) book.AddText("None of your clerics are on the map yet.");
        foreach (DistrictZone zone in posted)
        {
            ZoneInfluenceState state = zone.Influence;
            DistrictZone target = zone;
            book.AddEntry(book.MarkerFor(zone.District), Color.clear,
                $"{MapStatsQuery.ZoneLabel(zone)}  ·  {MapStatsQuery.DistrictLabel(zone.District)}",
                $"{Count(state.GetClerics(FactionId.Player), "cleric")}  ·  {state.GetShare(FactionId.Player)}/{state.Cap} influence  ·  {ControlOf(state)}",
                () => GoToZone(target));
        }

        book.AddSection("HOW CLERICS WORK");
        book.AddText("Select a zone on the map and press + to send a cleric from your reserve, or - to call one back.");
        book.AddText("Clerics raise your influence in their zone every turn. With more than half of it, the zone is yours.");
        if (upkeepEach > 0) book.AddText($"Each zone you rule feeds one cleric, and every {clergy.FlockPerFedCleric} faithful feed one more. Every other cleric on the map costs {upkeepEach} Wealth per turn. Clerics in reserve cost nothing.");
        book.AddText("No zone is safe. Where faiths meet, the one with more clerics there gains more, and the attacker has the edge.");
    }

    private void RenderRivals(bool animate)
    {
        MapStatsGlobalSnapshot city = MapStatsQuery.CaptureGlobal();
        FactionId? leader = city.FaithEclipseLeader;
        AIInfluenceController ai = FindAnyObjectByType<AIInfluenceController>();
        RivalAIDirector director = FindAnyObjectByType<RivalAIDirector>();

        SetCover("THE FAITHS OF", "BEHELD");
        book.SetHeading("THE CITY'S FAITHS", "RIVAL CULTS", $"{TurnLine()}  ·  {city.ControlledZones} of {city.PlayableZones} zones ruled");
        if (leader.HasValue) book.SetSeal(leader, "LEADS THE ECLIPSE", FactionIdUtil.DisplayName(leader.Value), animate);
        else book.SetSeal(null, "THE ECLIPSE", "No faith leads it yet", animate);

        book.AddSection("RIVALS");
        foreach (MapStatsFactionRow row in city.Factions)
        {
            if (FactionIdUtil.IsPlayer(row.Faction)) continue;
            string detail = FieldLine(row);
            string covets = Covets(ai, row.Faction);
            if (!string.IsNullOrEmpty(covets)) detail += "\nCovets " + covets;
            if (director != null && director.TryGetPersonality(row.Faction, out AIPersonality personality)) detail += "\n" + RivalAIDirector.PersonalityLabel(personality) + ": " + Manner(personality);
            AddFaction(row, leader == row.Faction ? "  ·  leads the eclipse" : string.Empty, detail);
        }

        MapStatsFactionRow player = Row(city, FactionId.Player);
        if (player != null)
        {
            book.AddSection("YOUR CULT");
            AddFaction(player, leader == FactionId.Player ? "  ·  leads the eclipse" : string.Empty, FieldLine(player));
        }

        book.AddSection("HOW RIVALS PLAY");
        book.AddText("Each rival trains new clerics as the turns pass and plants events of its own.");
        book.AddText("Each rival's temperament is drawn anew every match. Aggressive and opportunist faiths turn on whoever leads the eclipse; cautious ones keep to their own.");
        book.AddAction("OPEN THE CITY LEDGER", OpenCityLedger);
    }

    private void RenderDoctrines(bool animate)
    {
        string name = CultName();
        SetCover("THE DOCTRINES OF", name);
        if (doctrines == null)
        {
            book.SetHeading("YOUR CREED", "DOCTRINES", TurnLine());
            book.SetSeal(FactionId.Player, "YOUR CREED", name, animate);
            return;
        }

        int max = doctrines.MaxDoctrines;
        int kept = doctrines.Kept.Count;
        int ready = doctrines.ReadyCharges;
        int wait = doctrines.TurnsUntilCharge;
        string when = ready > 0 ? "Ready now" : wait <= 1 ? "Next turn" : $"In {wait} turns";
        book.SetHeading("YOUR CREED", "DOCTRINES", $"{kept}/{max} kept  ·  {Count(ready, "adoption")} ready");
        book.SetSeal(FactionId.Player, "NEXT ADOPTION", when, animate);

        book.AddSection("KEPT");
        if (kept == 0) book.AddText("Your cult keeps no doctrine yet. Adopt one below.");
        foreach (Doctrine doctrine in doctrines.Kept)
        {
            Doctrine target = doctrine;
            book.AddEntry(null, Color.clear, doctrine.Name, DoctrineDetail(doctrine));
            book.AddAction($"RENOUNCE {doctrine.Name.ToUpperInvariant()}", () => Renounce(target));
        }

        if (kept > 0 && kept < max) book.AddText($"{Count(max - kept, "place")} still open for a doctrine.");

        book.AddSection("KNOWN");
        var known = new List<Doctrine>();
        PlayerStats stats = doctrines.Stats;
        if (stats != null && stats.DoctrineInventory != null)
        {
            foreach (Doctrine doctrine in stats.DoctrineInventory)
            {
                if (doctrine != null && !doctrines.IsKept(doctrine)) known.Add(doctrine);
            }
        }

        if (known.Count == 0)
        {
            book.AddText("No other doctrine is known yet. New doctrines come from Theology: spend Zeal to commune with the Beholder and one may be revealed.");
            book.AddAction("SEEK DOCTRINES IN THEOLOGY", OpenTheology);
        }
        else if (kept >= max) book.AddText("Every place is taken. Renounce a doctrine to adopt another.");
        else if (ready == 0) book.AddText($"You can adopt again {when.ToLowerInvariant()}.");

        foreach (Doctrine doctrine in known)
        {
            Doctrine target = doctrine;
            book.AddEntry(null, Color.clear, doctrine.Name, DoctrineDetail(doctrine));
            if (doctrines.CanAdopt(doctrine)) book.AddAction($"ADOPT {doctrine.Name.ToUpperInvariant()}", () => Adopt(target));
        }

        book.AddSection("HOW DOCTRINES WORK");
        book.AddText($"Your cult keeps up to {max} doctrines. While kept, each one raises or lowers your virtues.");
        book.AddText($"Each adoption spends one of {max} charges, and a spent charge returns {Count(doctrines.ChangeCooldown, "turn")} later.");
        book.AddText("Renouncing costs nothing, and the doctrine's effects end at once.");
    }

    private void Adopt(Doctrine doctrine)
    {
        if (doctrines == null || !doctrines.Adopt(doctrine)) return;
        Render(true, false);
    }

    private void Renounce(Doctrine doctrine)
    {
        if (doctrines == null) return;
        doctrines.Renounce(doctrine);
        Render(true, false);
    }

    private static string DoctrineDetail(Doctrine doctrine)
    {
        string changes = StatChanges(doctrine);
        string rules = DoctrineText.Rules(doctrine);
        if (string.IsNullOrEmpty(changes)) return rules;
        return string.IsNullOrEmpty(rules) ? changes : $"{changes}\n<i>{rules}</i>";
    }

    private void AddVirtue(PlayerStats stats, PlayerStats.PlayerStat virtue)
    {
        int value = 10 + (stats != null ? stats.GetStat(virtue) : 0);
        List<Districts> districts = Virtues.DistrictsOf(virtue);
        float bonus = (value - 10) * Virtues.InfluencePercentPerPoint;
        string effect = Mathf.Approximately(bonus, 0f) ? "Normal influence" : $"{(bonus > 0 ? "+" : "")}{Mathf.RoundToInt(bonus)}% influence";
        string detail = districts.Count > 0 ? $"{effect} in {Virtues.DistrictList(districts)}" : effect;
        if (virtue == PlayerStats.PlayerStat.Learning) detail += "  ·  trains clerics twice over";
        else if (virtue == PlayerStats.PlayerStat.Stewardship) detail += "  ·  trains clerics";

        book.AddEntry(districts.Count > 0 ? book.MarkerFor(districts[0]) : null, Color.clear, $"{Virtues.Name(virtue)}  {value}", detail);
    }

    private void Ordain()
    {
        if (clergy != null) ConfirmOrdain(clergy.TryOrdain(), "Wealth");
    }

    private void OrdainWithSecrets()
    {
        if (clergy != null) ConfirmOrdain(clergy.TryOrdainWithSecrets(), "Secrets");
    }

    private void ConfirmOrdain(bool ordained, string resource)
    {
        if (!ordained)
        {
            TopScreenNotification.Show($"Not enough {resource} to ordain a cleric.", 3f);
            return;
        }

        TopScreenNotification.Show("A new cleric joins your reserve.", 3f);
        Render(false, false);
    }

    private void GoToZone(DistrictZone zone)
    {
        book.Close();
        if (mapCamera != null) mapCamera.FocusOnZone(zone);
        DistrictSelectionController.SetSelectedDistrict(zone.District, zone, zone.name, DistrictSelectionController.ResolvePartColorName(zone));
    }

    private void OpenTheology()
    {
        TheologyMenu theology = FindAnyObjectByType<TheologyMenu>(FindObjectsInactive.Include);
        if (theology == null) return;
        book.Close();
        MenuTransition.Show(theology.gameObject);
    }

    private void OpenCityLedger()
    {
        book.Close();
        if (MapStatsPanel.Instance != null) MapStatsPanel.Instance.OpenGlobal();
    }

    private void SetCover(string kicker, string title)
    {
        if (coverKicker != null) coverKicker.text = kicker;
        if (coverTitle != null) coverTitle.text = title.ToUpperInvariant();
    }

    private void AddFaction(MapStatsFactionRow row, string suffix, string detail)
    {
        Color ink = Color.Lerp(row.Color, Color.black, 0.45f);
        book.AddEntry(null, Color.Lerp(row.Color, Color.black, 0.2f),
            $"<color=#{ColorUtility.ToHtmlStringRGB(ink)}>{row.DisplayName}</color>{suffix}", detail);
    }

    private string TurnLine()
    {
        if (gameLoop == null) return $"Turn {GameTime.CurrentTurn}";
        return $"Turn {Mathf.Min(GameTime.CurrentTurn, gameLoop.EclipseTurn)}/{gameLoop.EclipseTurn}";
    }

    private string StandingLine(MapStatsGlobalSnapshot city, int zones)
    {
        string eclipse = gameLoop == null ? string.Empty
            : gameLoop.TurnsUntilEclipse <= 1 ? " The eclipse comes when this turn ends."
            : $" {gameLoop.TurnsUntilEclipse} turns until the eclipse.";

        FactionId? leader = city.FaithEclipseLeader;
        if (leader == FactionId.Player) return $"You lead the eclipse with {Count(zones, "zone")}.{eclipse}";
        if (leader.HasValue) return $"{FactionIdUtil.DisplayName(leader.Value)} leads with {Count(city.FaithEclipseLeading, "zone")}; you rule {Count(zones, "zone")}.{eclipse}";
        return $"No faith leads the eclipse yet; you rule {Count(zones, "zone")}.{eclipse}";
    }

    private static string FieldLine(MapStatsFactionRow row)
    {
        return $"{Count(row.ControlledZones, "zone")} ruled  ·  {row.Clerics} in the field  ·  {row.PoolClerics} in reserve";
    }

    private static string Covets(AIInfluenceController ai, FactionId faction)
    {
        if (ai == null) return string.Empty;
        IReadOnlyList<Districts> preferred = ai.GetPreferredDistricts(faction);
        if (preferred == null || preferred.Count == 0) return string.Empty;

        var districts = new List<Districts>();
        for (int i = 0; i < preferred.Count && i < 2; i++) districts.Add(preferred[i]);
        return Virtues.DistrictList(districts);
    }

    private static string CultName()
    {
        string name = FactionIdUtil.DisplayName(FactionId.Player);
        return string.IsNullOrWhiteSpace(name) || name == "You" ? "Your Cult" : name;
    }

    private static string Manner(AIPersonality personality)
    {
        switch (personality)
        {
            case AIPersonality.Aggressive: return "Spreads fast, one cleric per zone, and picks fights with every faith.";
            case AIPersonality.Cautious: return "Builds strongholds slowly and never starts a fight.";
            default: return "Takes empty ground first and only fights battles it can win.";
        }
    }

    private static string ControlOf(ZoneInfluenceState state)
    {
        if (state.Status != ZoneControlStatus.Controlled || !state.Controller.HasValue) return "contested";
        if (FactionIdUtil.IsPlayer(state.Controller.Value)) return "yours";
        return $"ruled by {FactionIdUtil.DisplayName(state.Controller.Value)}";
    }

    private static string StatChanges(Doctrine doctrine) => DoctrineText.Effects(doctrine, ",  ");

    private static int Rank(MapStatsGlobalSnapshot city, int zones)
    {
        int rank = 1;
        foreach (MapStatsFactionRow row in city.Factions)
        {
            if (!FactionIdUtil.IsPlayer(row.Faction) && row.ControlledZones > zones) rank++;
        }

        return rank;
    }

    private static MapStatsFactionRow Row(MapStatsGlobalSnapshot city, FactionId faction)
    {
        foreach (MapStatsFactionRow row in city.Factions)
        {
            if (row.Faction == faction) return row;
        }

        return null;
    }


    private static string Ordinal(int rank)
    {
        switch (rank)
        {
            case 1: return "1st";
            case 2: return "2nd";
            case 3: return "3rd";
            default: return $"{rank}th";
        }
    }

    private static string Count(int amount, string noun) => amount == 1 ? $"1 {noun}" : $"{amount} {noun}s";
}
