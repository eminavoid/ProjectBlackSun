using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TutorialDirector : MonoBehaviour
{
    private CultCreationPanel cult;
    private TutorialHighlight highlight;
    private TutorialGate gate;
    private DistrictZone homeZone;
    private DistrictZone clericZone;
    private Seed waitingSeed;
    private string heldLine;
    private bool heldContinue;
    private bool continueBeat;
    private bool settled;
    private bool planted;
    private bool turnEnding;
    private bool turnResolved;
    private bool communed;
    private bool rewardClaimed;
    private bool doctrineKept;

    private bool Stopped => !TutorialSession.Guide || TutorialSession.Skipped || TutorialSession.Finished;

    private void OnEnable()
    {
        SeedEventManager.onEventSettled += OnSettled;
        DistrictSeedPlantPanel.Planted += OnPlanted;
        DoctrinesController.DoctrineAdded += OnDoctrineAdded;
        TheologyMenu.Communed += OnCommuned;
        TheologyMenu.RewardGranted += OnTheologyReward;
        GameTime.OnTurnResolving += OnTurnResolving;
        GameTime.OnTurnResolved += OnTurnResolved;
    }

    private void OnDisable()
    {
        SeedEventManager.onEventSettled -= OnSettled;
        DistrictSeedPlantPanel.Planted -= OnPlanted;
        DoctrinesController.DoctrineAdded -= OnDoctrineAdded;
        TheologyMenu.Communed -= OnCommuned;
        TheologyMenu.RewardGranted -= OnTheologyReward;
        GameTime.OnTurnResolving -= OnTurnResolving;
        GameTime.OnTurnResolved -= OnTurnResolved;
    }

    private void OnDestroy()
    {
        if (!doctrineKept) ForgetPending();
        if (gate != null) InputLocks.Clear();
    }

    private void Start()
    {
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        yield return null;
        Transform canvas = ScreenCanvas();
        if (canvas == null) yield break;

        cult = CultCreationPanel.Create(canvas);
        if (!TutorialSession.Guide)
        {
            while (cult != null && !cult.Confirmed) yield return null;
            TutorialContent.GiveTutorialSeeds();
            yield break;
        }

        InputLocks.Lock(true, true, true);
        highlight = TutorialHighlight.Create(canvas);
        highlight.SkipClicked += SkipGuide;
        gate = TutorialGate.Create(canvas);

        foreach (IEnumerator step in Steps())
        {
            yield return step;
            if (Stopped) yield break;
        }

        doctrineKept = true;
        TutorialContent.GiveTutorialSeeds();
        TutorialSession.Complete();
        CleanupGuide();
    }

    private IEnumerable<IEnumerator> Steps()
    {
        yield return CultStep();
        yield return WaitForCamera();
        homeZone = TutorialContent.FindPlayerZone();
        clericZone = FreeNeighbor(homeZone);
        yield return IntroBeat();
        yield return RivalsBeat();
        yield return ClericStep();
        yield return SeedStep();
        yield return EventStep(TutorialContent.CreateWhisper(homeZone), "Something needs your answer: an event. Open the scroll on the left.");
        yield return EndTurnStep(2, "End the turn. Your clerics and the rival's move take effect.");
        yield return ChronicleStep();
        yield return RuleBeat();
        yield return BookStep(CultLedger.Page.Clergy, "OpenClergy",
            "Open Clerics to see where yours preach and how new ones join.",
            "A new cleric joins your reserve every few turns, or you can ordain one now for Wealth. Below, see where each of yours preaches.");
        yield return LedgerStep();
        yield return StruggleBeat();
        yield return BookStep(CultLedger.Page.Rivals, "OpenRivals",
            "Open Rival Cults to size up the other faiths.",
            "Each rival covets its own districts, and its temperament is drawn anew each match: cautious, aggressive or opportunist. See here how each one plays.");
        yield return EndTurnStep(3, "End the turn again.");
        yield return ChronicleStep();
        yield return TheologyStep();
        yield return DoctrineStep();
        yield return BookStep(CultLedger.Page.Cult, "OpenCult",
            "Open Your Cult to see where you stand.",
            "Your standing, virtues and treasury. Each virtue raises your influence in its districts, and your doctrines shape your virtues.");
        yield return ClosingBeat();
    }

    private IEnumerator CultStep()
    {
        highlight.Clear();
        gate.Allow(cult);
        while ((cult == null || !cult.Confirmed) && !TutorialSession.CultChosen && !Stopped) yield return null;
        gate.Allow();
    }

    private IEnumerator IntroBeat()
    {
        Focus(homeZone);
        FirstTimeHints.MarkSeen(FirstTimeHints.Goal);
        yield return Beat(homeZone, $"This zone is your cult's first. When the Faith Eclipse comes on turn {EclipseTurn()}, the faith that rules the most zones wins.");
    }

    private IEnumerator RivalsBeat()
    {
        DistrictZone zone = TutorialContent.ScriptedClericZone;
        string rival = FactionIdUtil.DisplayName(FactionId.Rival1);
        if (zone == null)
        {
            yield return Beat(null, "Other colors on the map are rival faiths. Their arrows show the moves they will make when the turn ends.");
            yield break;
        }

        Focus(zone);
        yield return Beat(zone, $"Other colors on the map are rival faiths. This arrow is a move the {rival} will make when the turn ends.");
    }

    private IEnumerator ClericStep()
    {
        DistrictClericAssignPanel panel = FindAnyObjectByType<DistrictClericAssignPanel>();
        DistrictZone target = clericZone != null ? clericZone : homeZone;
        if (panel == null || target == null || InfluenceManager.IsNull) yield break;

        target.EnsureInfluenceState();
        Focus(target);
        InputLocks.Lock(true, false, true);
        while (!Stopped && target.Influence.GetClerics(FactionId.Player) < 1)
        {
            if (InfluenceManager.Get.GetClericPool(FactionId.Player) <= 0) break;

            if (DistrictSelectionController.SelectedZone != target)
            {
                Guide("Clerics spread your faith, but only into zones next to one you rule. Click the marked zone.", target);
            }
            else
            {
                panel.Reveal();
                Guide("Press + to send a cleric. The panel shows the influence it will bring next turn.", panel.AssignButton, panel.Plate);
            }

            yield return null;
        }

        InputLocks.Lock(true, true, true);
    }

    private IEnumerator SeedStep()
    {
        DistrictSeedPlantPanel panel = FindAnyObjectByType<DistrictSeedPlantPanel>();
        if (panel == null) yield break;

        Seed seed = TutorialContent.FirstSeed;
        panel.Grant(seed, 1);
        TutorialContent.MarkFirstSeedGranted();
        DistrictZone target = clericZone != null ? clericZone : homeZone;
        planted = false;
        InputLocks.Lock(true, false, true);

        while (!planted && !Stopped)
        {
            if (panel.Owned(seed) <= 0) panel.Grant(seed, 1);
            DistrictZone zone = DistrictSelectionController.SelectedZone;
            bool zoneReady = zone != null && zone.IsPlayable && !zone.IsOccupied;

            if (!zoneReady)
            {
                Guide("Click the marked zone to plant there.", target, panel.MenuButton, panel.PanelRoot);
            }
            else if (!panel.MenuOpen)
            {
                Guide("Seeds are schemes that grow into events. Open the seed menu.", panel.MenuButton, panel.MenuButton);
            }
            else if (panel.SelectedSeed != seed)
            {
                Guide("Pick First Seed.", panel.ButtonFor(seed), panel.MenuButton, panel.PanelRoot);
            }
            else
            {
                Guide("Press Plant. It grows in the selected zone for a few turns, then becomes an event.", panel.PlantButton, panel.MenuButton, panel.PanelRoot);
            }

            yield return null;
        }

        InputLocks.Lock(true, true, true);
    }

    private IEnumerator EventStep(Seed seed, string openLine)
    {
        if (seed == null || seed.CurrentZone == null) yield break;

        FirstTimeHints.MarkSeen(FirstTimeHints.FirstEvent);
        EventsMenu menu = FindAnyObjectByType<EventsMenu>();
        Button opener = menu != null ? FindOpener(menu.Panel) : null;
        settled = false;
        waitingSeed = seed;
        SeedEventManager.StoreSeedEvent(seed);

        while (!Stopped && (!settled || SeedEventManager.OutputWindow != null))
        {
            if (SeedEventManager.OutputWindow != null)
            {
                Quiet(SeedEventManager.OutputWindow);
            }
            else if (SeedEventManager.OptionsOpen)
            {
                Quiet(SeedEventManager.OptionsWindow);
            }
            else if (menu != null && menu.Panel.activeInHierarchy && !settled)
            {
                Guide("Click the event to answer it.", menu.EntryFor(seed), menu.Panel.transform);
            }
            else if (!settled)
            {
                Guide(openLine, TrayMark(), SeedEventManager.UnresolvedBadge, opener);
            }

            yield return null;
        }

        waitingSeed = null;
    }

    private IEnumerator EndTurnStep(int turnAtLeast, string line)
    {
        if (GameTime.CurrentTurn >= turnAtLeast) yield break;

        Button endTurn = FindByMethod("NextTurn");
        turnEnding = false;
        turnResolved = false;
        InputLocks.Lock(false, true, true);
        while (!turnEnding && !Stopped)
        {
            Guide(line, endTurn, endTurn);
            yield return null;
        }

        InputLocks.Lock(true, true, true);
        Quiet();
        while ((!turnResolved || GameTime.CurrentTurn < turnAtLeast) && !Stopped) yield return null;
        yield return null;
    }

    private IEnumerator ChronicleStep()
    {
        ChronicleWindow window = FindAnyObjectByType<ChronicleWindow>();
        while (window != null && !Stopped)
        {
            Quiet(window);
            yield return null;
        }
    }

    private IEnumerator RuleBeat()
    {
        FirstTimeHints.MarkSeen(FirstTimeHints.ZoneTaken);
        DistrictZone zone = clericZone;
        if (zone == null || zone.Influence == null) yield break;
        if (zone.Influence.Status != ZoneControlStatus.Controlled || zone.Influence.Controller != FactionId.Player) yield break;

        Focus(zone);
        yield return Beat(zone, "You rule this zone now: more than half its influence is yours. Each zone you rule pays tithes and feeds one cleric. Clerics beyond that cost Wealth in upkeep.");
    }

    private IEnumerator StruggleBeat()
    {
        DistrictZone zone = TutorialContent.ScriptedClericZone;
        Focus(zone);
        yield return Beat(zone, "No zone is safe. Where faiths meet, the one with more clerics there gains more, and the attacker has the edge. Once a zone is full, the stronger faith takes influence from the rest.");
    }

    private IEnumerator LedgerStep()
    {
        MapStatsPanel stats = MapStatsPanel.Instance;
        if (stats == null) yield break;

        DistrictZone zone = TutorialContent.ScriptedClericZone;
        string openLine = zone != null
            ? $"The {FactionIdUtil.DisplayName(FactionId.Rival1)} moved here, as its arrow warned. Right-click this zone to open its ledger."
            : "Right-click a zone to open its ledger.";
        Focus(zone);

        continueBeat = false;
        while (!continueBeat && !Stopped)
        {
            if (stats.IsOpen)
            {
                InputLocks.Lock(true, true, true);
                Guide("The ledger shows who rules each zone and district, and how much influence every faith holds.", null, true, stats.Book);
            }
            else
            {
                InputLocks.Lock(true, true, false);
                Guide(openLine, zone);
            }

            yield return null;
        }

        InputLocks.Lock(true, true, true);
        if (stats.IsOpen) stats.Close();
    }

    private IEnumerator TheologyStep()
    {
        rewardClaimed = false;
        communed = false;
        Doctrine doctrine = TutorialContent.CreateVigil();
        TutorialSession.ArmFixedUnlock(TutorialContent.Wrap(doctrine), doctrine);

        TheologyMenu theology = FindAnyObjectByType<TheologyMenu>(FindObjectsInactive.Include);
        if (theology == null) yield break;

        Button opener = FindOpener(theology.gameObject);
        Button commune = FindByMethod("Commune");
        Button eye = FindByMethod("AdvanceEye");

        while (!rewardClaimed && !Stopped)
        {
            if (SeedEventManager.OutputWindow != null)
            {
                Quiet(SeedEventManager.OutputWindow, theology);
            }
            else if (!theology.gameObject.activeInHierarchy)
            {
                Guide("Theology: spend Zeal to commune with the Beholder for seeds, doctrines and blessings. A ! here means you can afford it. Open it.", opener, opener);
            }
            else if (!communed)
            {
                Guide("Press Commune. The first one is free.", commune, theology);
            }
            else
            {
                Guide("Click the eye until it wakes to receive your reward.", eye, theology);
            }

            yield return null;
        }
    }

    private IEnumerator DoctrineStep()
    {
        Doctrine doctrine = TutorialSession.PendingDoctrine;
        CultLedger ledger = FindAnyObjectByType<CultLedger>(FindObjectsInactive.Include);
        Button opener = FindByMethod("OpenDoctrines");
        if (ledger == null || opener == null || doctrine == null) yield break;

        string adopt = $"ADOPT {doctrine.Name.ToUpperInvariant()}";
        while (!doctrineKept && !Stopped)
        {
            if (ledger.IsShowing(CultLedger.Page.Doctrines))
            {
                Guide($"Adopt {doctrine.Name}. While your cult keeps it, it raises a virtue.", ButtonLabeled(ledger.Book, adopt), ledger.Book);
            }
            else
            {
                Guide("The eye gave you a doctrine. Open Doctrines to adopt it.", opener, opener);
            }

            yield return null;
        }

        ledger.Close();
    }

    private IEnumerator BookStep(CultLedger.Page page, string openMethod, string openLine, string readLine)
    {
        CultLedger ledger = FindAnyObjectByType<CultLedger>(FindObjectsInactive.Include);
        Button opener = FindByMethod(openMethod);
        if (ledger == null || opener == null) yield break;

        continueBeat = false;
        while (!continueBeat && !Stopped)
        {
            if (ledger.IsShowing(page)) Guide(readLine, null, true, ledger.Book);
            else Guide(openLine, opener, opener);
            yield return null;
        }

        ledger.Close();
    }

    private IEnumerator ClosingBeat()
    {
        string hints = FirstTimeHints.Enabled ? " Hints will appear the first time something new happens." : string.Empty;
        yield return Beat(null, $"That's the basics. Rule the most zones by turn {EclipseTurn()}, and guard them: rivals take what you leave unattended.{hints}", "DONE");
    }

    private IEnumerator WaitForCamera()
    {
        float elapsed = 0f;
        while (elapsed < 8f && !Stopped)
        {
            MapCameraController camera = MapCameraController.Instance;
            if (camera != null && camera.enabled) yield break;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private IEnumerator Beat(DistrictZone zone, string line, string label = "NEXT")
    {
        continueBeat = false;
        while (!continueBeat && !Stopped)
        {
            Guide(line, zone, true, label);
            yield return null;
        }
    }

    private void Guide(string line, Object target, params Component[] allow)
    {
        Guide(line, target, false, allow);
    }

    private void Guide(string line, Object target, bool showContinue, params Component[] allow)
    {
        Guide(line, target, showContinue, "NEXT", allow);
    }

    private void Guide(string line, Object target, bool showContinue, string label, params Component[] allow)
    {
        gate.Allow(allow);
        Point(target);
        if (line == heldLine && showContinue == heldContinue && TopScreenNotification.IsHolding) return;

        heldLine = line;
        heldContinue = showContinue;
        continueBeat = false;
        TopScreenNotification.Hold(line, showContinue, () => continueBeat = true, label, "TUTORIAL");
    }

    private void Quiet(params Component[] allow)
    {
        gate.Allow(allow);
        highlight.Clear();
        if (heldLine == null) return;
        heldLine = null;
        TopScreenNotification.Release();
    }

    private void Point(Object target)
    {
        if (target == null)
        {
            highlight.Clear();
            return;
        }

        switch (target)
        {
            case DistrictZone zone:
                highlight.TrackZone(zone);
                break;
            case Button button:
                highlight.Track(button);
                break;
            case Component component when component.transform is RectTransform rect && component.gameObject.activeInHierarchy:
                highlight.Track(rect);
                break;
            default:
                highlight.Clear();
                break;
        }
    }

    private static void Focus(DistrictZone zone)
    {
        if (zone != null && MapCameraController.Instance != null) MapCameraController.Instance.FocusOnZone(zone);
    }

    public void SkipGuide()
    {
        if (!TutorialSession.CultChosen)
        {
            CultCreationPanel.ResetOpeningStats();
            if (cult != null) cult.Dismiss();
        }

        ForgetPending();
        TutorialContent.GiveTutorialSeeds();
        TutorialSession.Skip();
        if (!AIIntentBoard.IsNull) AIIntentBoard.Get.PlayLivePlans();
        CleanupGuide();
        StopAllCoroutines();
    }

    private void CleanupGuide()
    {
        TopScreenNotification.Release();
        if (highlight != null) Destroy(highlight.gameObject);
        highlight = null;
        if (gate != null) Destroy(gate.gameObject);
        gate = null;
        InputLocks.Clear();
    }

    private void ForgetPending()
    {
        Doctrine doctrine = TutorialSession.PendingDoctrine;
        if (doctrine == null) return;

        if (!InfluenceManager.IsNull && InfluenceManager.Get.Stats != null)
            InfluenceManager.Get.Stats.ForgetDoctrine(doctrine);

        DoctrinesController doctrines = FindAnyObjectByType<DoctrinesController>();
        if (doctrines != null && doctrines.Stats != null)
            doctrines.Stats.ForgetDoctrine(doctrine);

        TutorialSession.ClearPendingDoctrine();
    }

    private void OnSettled(SettledEvent settledEvent)
    {
        if (waitingSeed == null || settledEvent == null || settledEvent.Seed != waitingSeed) return;
        settled = true;
    }

    private void OnPlanted() => planted = true;

    private void OnDoctrineAdded(Doctrine doctrine)
    {
        if (doctrine != null && doctrine == TutorialSession.PendingDoctrine) doctrineKept = true;
    }

    private void OnCommuned() => communed = true;

    private void OnTheologyReward() => rewardClaimed = true;

    private void OnTurnResolving() => turnEnding = true;

    private void OnTurnResolved() => turnResolved = true;

    private static DistrictZone FreeNeighbor(DistrictZone home)
    {
        if (home == null || InfluenceManager.IsNull) return null;

        InfluenceManager manager = InfluenceManager.Get;
        DistrictZone rivalTarget = TutorialContent.ScriptedClericZone;
        Vector3 origin = home.GetWorldBounds().center;
        IReadOnlyList<DistrictZone> zones = manager.GetPlayableZones();
        DistrictZone best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < zones.Count; i++)
        {
            DistrictZone zone = zones[i];
            if (zone == null || zone == home || zone == rivalTarget || zone.IsOccupied) continue;
            zone.EnsureInfluenceState();
            if (zone.Influence == null || zone.Influence.HasAnyPresence) continue;
            if (!zone.Influence.CanEnterByNormalMeans(FactionId.Player) || !manager.InClericReach(zone, FactionId.Player)) continue;

            float distance = (zone.GetWorldBounds().center - origin).sqrMagnitude;
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = zone;
        }

        return best;
    }

    private static int EclipseTurn()
    {
        GameLoopEvents loop = FindAnyObjectByType<GameLoopEvents>();
        return loop != null ? loop.EclipseTurn : 30;
    }

    private static Button ButtonLabeled(Component root, string label)
    {
        foreach (Button button in root.GetComponentsInChildren<Button>())
        {
            TMP_Text text = button.GetComponentInChildren<TMP_Text>();
            if (text != null && text.text == label) return button;
        }

        return null;
    }

    private RectTransform TrayMark()
    {
        RectTransform badge = SeedEventManager.UnresolvedBadge;
        if (badge != null && badge.gameObject.activeInHierarchy) return badge;

        EventsMenu menu = FindAnyObjectByType<EventsMenu>();
        Button opener = menu != null ? FindOpener(menu.Panel) : null;
        return opener != null ? (RectTransform)opener.transform : null;
    }


    private static Button FindByMethod(string methodName)
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            int count = button.onClick.GetPersistentEventCount();
            for (int call = 0; call < count; call++)
            {
                if (button.onClick.GetPersistentMethodName(call) == methodName) return button;
            }
        }

        return null;
    }

    private static Button FindOpener(GameObject panel)
    {
        if (panel == null) return null;

        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include);
        Button fallback = null;
        for (int i = 0; i < buttons.Length; i++)
        {
            if (!Opens(buttons[i], panel)) continue;
            string name = buttons[i].gameObject.name;
            if (name == "Dim" || name == "Close")
            {
                if (fallback == null) fallback = buttons[i];
                continue;
            }

            return buttons[i];
        }

        return fallback;
    }

    private static bool Opens(Button button, GameObject panel)
    {
        int count = button.onClick.GetPersistentEventCount();
        for (int i = 0; i < count; i++)
        {
            if (button.onClick.GetPersistentMethodName(i) != "Open") continue;
            if (button.onClick.GetPersistentTarget(i) is Component component && component.gameObject == panel)
                return true;
        }

        return false;
    }

    private static Transform ScreenCanvas()
    {
        if (!GlobalReferences.IsNull && GlobalReferences.ScreenCanvas != null)
            return GlobalReferences.ScreenCanvas.transform;

        Canvas canvas = FindAnyObjectByType<Canvas>();
        return canvas != null ? canvas.transform : null;
    }
}
