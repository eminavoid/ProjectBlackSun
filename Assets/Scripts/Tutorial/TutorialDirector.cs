using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Acompaña la partida: culto siempre, y la guía solo si es la primera vez.
/// No bloquea la UI. Skip corta la guía y la partida sigue.
/// </summary>
public class TutorialDirector : MonoBehaviour
{
    private CultCreationPanel cult;
    private TutorialHighlight highlight;
    private DistrictExplainHighlight districtExplain;
    private MapCameraController tourCamera;
    private MenuTransition doctrineMenu;
    private Seed waitingSeed;
    private Seed grantOnSettle;
    private bool settled;
    private bool planted;
    private bool assigned;
    private bool rewardClaimed;
    private bool doctrineKept;
    private bool continueBeat;
    private bool tourCancel;
    private bool journalOpened;
    private bool communed;
    private MapStatsPanel journalStats;

    private bool Stopped => !TutorialSession.Guide || TutorialSession.Skipped || TutorialSession.Finished;

    private void OnEnable()
    {
        SeedEventManager.onEventSettled += OnSettled;
        DistrictSeedPlantPanel.Planted += OnPlanted;
        DistrictClericAssignPanel.Assigned += OnAssigned;
        DoctrinesController.DoctrineAdded += OnDoctrineAdded;
        UnlockerMenuTest.RewardChosen += OnReward;
        TheologyMenu.Communed += OnCommuned;
    }

    private void OnDisable()
    {
        SeedEventManager.onEventSettled -= OnSettled;
        DistrictSeedPlantPanel.Planted -= OnPlanted;
        DistrictClericAssignPanel.Assigned -= OnAssigned;
        DoctrinesController.DoctrineAdded -= OnDoctrineAdded;
        UnlockerMenuTest.RewardChosen -= OnReward;
        TheologyMenu.Communed -= OnCommuned;
        StopTourListen();
        UnhookJournal();
    }

    private void OnDestroy()
    {
        if (!doctrineKept) ForgetPending();
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
            yield break;
        }

        highlight = TutorialHighlight.Create(canvas);
        highlight.SkipClicked += SkipGuide;
        districtExplain = gameObject.AddComponent<DistrictExplainHighlight>();

        yield return CultStep();
        if (Stopped) yield break;
        yield return WaitForCamera();
        if (Stopped) yield break;
        yield return WhereStep();
        if (Stopped) yield break;
        yield return RivalsStep();
        if (Stopped) yield break;
        yield return DistrictStep();
        if (Stopped) yield break;
        yield return TrayStep(TutorialContent.CreateWhisper(HomeZone()), "A matter is waiting in the tray. Open it and choose an answer.", null);
        if (Stopped) yield break;
        yield return SeedStep();
        if (Stopped) yield break;
        yield return ClericStep();
        if (Stopped) yield break;
        yield return JournalStep();
        if (Stopped) yield break;
        yield return EndTurnStep(2, "End the turn. The arrow you saw becomes a real move.");
        if (Stopped) yield break;
        yield return TrayStep(TutorialContent.CreateRivalReply(IntentZone()), "A rival play is in the tray. Open it and answer.", null);
        if (Stopped) yield break;
        yield return EndTurnStep(3, "End the turn again.");
        if (Stopped) yield break;
        yield return TrayStep(
            TutorialContent.CreateReward(HomeZone()),
            "A seed of your own came back through the tray. This is where rewards and rival plays arrive. Open it.",
            TutorialContent.CreatePlantable("Returned Seed", "Yours to plant on a free zone."));
        if (Stopped) yield break;
        yield return TheologyStep();
        if (Stopped) yield break;
        yield return DoctrineStep();
        if (Stopped) yield break;

        doctrineKept = true;
        TutorialSession.Complete();
        CleanupGuide();
    }

    private IEnumerator CultStep()
    {
        if (cult != null) highlight.Track(cult.Card);
        Hold("Name your cult, then choose the attribute it starts with.", false);
        while ((cult == null || !cult.Confirmed) && !TutorialSession.CultChosen && !Stopped) yield return null;
    }

    private IEnumerator WhereStep()
    {
        DistrictZone zone = TutorialContent.FindPlayerZone();
        if (zone != null && MapCameraController.Instance != null)
        {
            MapCameraController.Instance.FocusOnZone(zone);
            highlight.TrackZone(zone);
        }
        else
        {
            highlight.Clear();
        }

        string line = zone != null
            ? "This street is yours. Your color on the Faith Eclipse bar is your cult. The cult that rules the most zones wins the Faith Eclipse. If your Flock runs out, the cult falls apart."
            : "Your color on the Faith Eclipse bar is your cult. The cult that rules the most zones wins the Faith Eclipse. If your Flock runs out, the cult falls apart.";
        yield return WaitContinue(line);
    }

    private IEnumerator RivalsStep()
    {
        DistrictZone zone = TutorialContent.ScriptedClericZone;
        if (zone != null && MapCameraController.Instance != null)
        {
            MapCameraController.Instance.FocusOnZone(zone);
            highlight.TrackZone(zone);
        }

        yield return WaitContinue("The arrow is a play the Crimson Choir has already chosen and has not made. Rose Ledger and Ochre Flock move the same way.");
    }

    private IEnumerator DistrictStep()
    {
        MapCameraController camera = MapCameraController.Instance;
        tourCancel = false;
        if (camera != null)
        {
            tourCamera = camera;
            camera.OnUserAdjusted += OnCameraNudged;
            camera.ZoomToMax();
        }

        foreach (Districts district in System.Enum.GetValues(typeof(Districts)))
        {
            if (Stopped || tourCancel) break;
            if (districtExplain == null || !districtExplain.Show(district, out _)) continue;
            highlight.Clear();
            continueBeat = false;
            Hold(TutorialContent.DistrictLine(district), true);
            float elapsed = 0f;
            while (elapsed < 3.2f && !continueBeat && !tourCancel && !Stopped)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        StopTourListen();
        if (districtExplain != null) districtExplain.Clear();
    }

    private IEnumerator TrayStep(Seed seed, string line, Seed grant)
    {
        settled = false;
        waitingSeed = null;
        grantOnSettle = null;

        if (seed == null || seed.CurrentZone == null)
        {
            yield return WaitContinue(line);
            yield break;
        }

        grantOnSettle = grant;
        waitingSeed = seed;
        SeedEventManager.StoreSeedEvent(seed);
        highlight.Track(TrayMark());
        Hold(line, false);
        while (!settled && !Stopped) yield return null;
        waitingSeed = null;
        grantOnSettle = null;
    }

    private IEnumerator SeedStep()
    {
        planted = false;
        DistrictSeedPlantPanel panel = FindAnyObjectByType<DistrictSeedPlantPanel>();
        if (panel == null)
        {
            yield return WaitContinue("Seeds are planted from the seed menu onto a free zone. A seed shows its name and how many turns it still needs.");
            yield break;
        }

        panel.Grant(TutorialContent.CreatePlantable(
            "First Seed",
            "Plant this on a free zone. Its name and the turns it still needs are how you read a seed."), 1);

        highlight.Track(panel.MenuButton);
        Hold("Seeds are how a cult acts on a street. Open the seed menu.", false);
        while (!panel.MenuOpen && !planted && !Stopped) yield return null;
        if (Stopped || planted) yield break;

        highlight.Track(panel.PlantButton);
        Hold("Read the name and the turns it needs. Pick the seed, pick a free zone, and plant it.", false);
        while (!planted && !Stopped) yield return null;
    }

    private IEnumerator ClericStep()
    {
        assigned = false;
        DistrictZone zone = HomeZone();
        if (zone != null)
        {
            DistrictSelectionController.SetSelectedDistrict(zone.District, zone, zone.SectorName, null);
            if (MapCameraController.Instance != null) MapCameraController.Instance.FocusOnZone(zone);
        }

        DistrictClericAssignPanel panel = FindAnyObjectByType<DistrictClericAssignPanel>();
        if (panel == null || panel.AssignButton == null)
        {
            yield return WaitContinue("Clerics are your people on a street. Select a zone and assign one. The panel shows how many stand here and how many wait in reserve.");
            yield break;
        }

        highlight.Track(panel.AssignButton);
        Hold("Clerics are your people on a street. Assign one. The panel shows how many stand here and how many wait in reserve.", false);
        while (!assigned && !Stopped) yield return null;
    }

    private IEnumerator JournalStep()
    {
        DistrictZone zone = HomeZone();
        if (zone != null) highlight.TrackZone(zone);
        Hold("Right-click a district to open the ledger. Choose a district or a zone. The page lists your influence beside the other cults.", false);

        journalOpened = false;
        journalStats = MapStatsPanel.Instance;
        if (journalStats == null)
        {
            yield return WaitContinue("Right-click a district to open the ledger.");
            yield break;
        }

        bool wasOpen = journalStats.IsOpen;
        journalStats.PageOpened += OnJournalPage;
        while (!journalOpened && !Stopped)
        {
            if (!wasOpen && journalStats.IsOpen) journalOpened = true;
            yield return null;
        }

        UnhookJournal();
    }

    private IEnumerator EndTurnStep(int turnAtLeast, string line)
    {
        if (GameTime.CurrentTurn >= turnAtLeast) yield break;

        highlight.Track(FindByMethod("NextTurn"));
        Hold(line, false);
        while (GameTime.CurrentTurn < turnAtLeast && !Stopped) yield return null;
    }

    private IEnumerator TheologyStep()
    {
        rewardClaimed = false;
        communed = false;
        Doctrine doctrine = TutorialContent.CreateVigil();
        TutorialSession.ArmFixedUnlock(TutorialContent.Wrap(doctrine), doctrine);

        TheologyMenu theology = FindAnyObjectByType<TheologyMenu>(FindObjectsInactive.Include);
        Button opener = theology != null ? FindOpener(theology.gameObject) : null;
        Button commune = FindByMethod("Commune");

        if (theology != null && !theology.gameObject.activeInHierarchy)
        {
            highlight.Track(opener != null ? opener : commune);
            Hold("Open Theology.", false);
            while (!theology.gameObject.activeInHierarchy && !communed && !rewardClaimed && !Stopped)
                yield return null;
        }

        if (Stopped || rewardClaimed) yield break;

        highlight.Track(commune);
        yield return WaitContinue("Theology costs Zeal. The first offering is free. After that, the price rises.", true);
        if (Stopped) yield break;
        highlight.Track(commune);
        yield return WaitContinue("The offering buys a reward. Yours is a doctrine: First Vigil.", true);
        if (Stopped) yield break;
        highlight.Track(commune);
        yield return WaitContinue("The Zeal you spend is the sacrifice. Press Commune.", true);
        if (Stopped || rewardClaimed) yield break;

        if (!communed)
        {
            highlight.Track(commune);
            Hold("Press Commune.", false);
            while (!communed && !rewardClaimed && !Stopped) yield return null;
        }

        if (Stopped || rewardClaimed) yield break;

        Button unlock = FindByMethod("TryPurchaseUnlock");
        RevealCodex(unlock);
        if (unlock == null)
        {
            yield return WaitContinue("Press Unlock and take First Vigil.");
            yield break;
        }

        highlight.Track(unlock);
        Hold("Press Unlock and take First Vigil.", false);
        while (!rewardClaimed && !Stopped) yield return null;
    }

    private IEnumerator DoctrineStep()
    {
        MenuTransition menu = DoctrineMenu();
        if (menu == null)
        {
            yield return WaitContinue("Open your cult. A doctrine is a rule the cult keeps. Choose First Vigil.");
            yield break;
        }

        Button opener = FindOpener(menu.gameObject);
        highlight.Track(opener);
        Hold("Open your cult. A doctrine is a rule the cult keeps.", false);
        while ((menu == null || !menu.gameObject.activeInHierarchy) && !doctrineKept && !Stopped) yield return null;
        if (Stopped || doctrineKept) yield break;

        Button add = FindByMethod("OpenSelectorMenu");
        highlight.Track(add);
        Hold("Choose First Vigil.", false);
        while (!doctrineKept && !Stopped) yield return null;
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

    private IEnumerator WaitContinue(string line, bool keepHighlight = false)
    {
        continueBeat = false;
        if (!keepHighlight) highlight.Clear();
        Hold(line, true);
        while (!continueBeat && !Stopped) yield return null;
    }

    private void Hold(string line, bool showContinue)
    {
        TopScreenNotification.Hold(line, showContinue, () => continueBeat = true);
    }

    public void SkipGuide()
    {
        if (!TutorialSession.CultChosen)
        {
            CultCreationPanel.ResetOpeningStats();
            if (cult != null) cult.Dismiss();
        }

        ForgetPending();
        TutorialSession.Skip();
        CleanupGuide();
        StopAllCoroutines();
    }

    private void CleanupGuide()
    {
        StopTourListen();
        UnhookJournal();
        if (districtExplain != null) districtExplain.Clear();
        TopScreenNotification.Release();
        if (highlight != null) Destroy(highlight.gameObject);
        highlight = null;
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

        if (grantOnSettle != null)
        {
            DistrictSeedPlantPanel panel = FindAnyObjectByType<DistrictSeedPlantPanel>();
            if (panel != null) panel.Grant(grantOnSettle, 1);
            grantOnSettle = null;
        }

        waitingSeed = null;
        settled = true;
    }

    private void OnPlanted() => planted = true;

    private void OnAssigned() => assigned = true;

    private void OnReward(Unlockeable unlock)
    {
        if (TutorialSession.PendingDoctrine != null &&
            unlock != null &&
            unlock.Scriptable == TutorialSession.PendingDoctrine)
        {
            rewardClaimed = true;
        }
    }

    private void OnDoctrineAdded(Doctrine doctrine)
    {
        if (doctrine != null && doctrine == TutorialSession.PendingDoctrine) doctrineKept = true;
    }

    private void OnCommuned() => communed = true;

    private void OnCameraNudged() => tourCancel = true;

    private void OnJournalPage() => journalOpened = true;

    private void StopTourListen()
    {
        if (tourCamera == null) return;
        tourCamera.OnUserAdjusted -= OnCameraNudged;
        tourCamera = null;
    }

    private void UnhookJournal()
    {
        if (journalStats == null) return;
        journalStats.PageOpened -= OnJournalPage;
        journalStats = null;
    }

    private static DistrictZone HomeZone()
    {
        return TutorialContent.FindPlayerZone();
    }

    private static DistrictZone IntentZone()
    {
        return TutorialContent.ScriptedSeedZone != null
            ? TutorialContent.ScriptedSeedZone
            : TutorialContent.ScriptedClericZone;
    }

    private RectTransform TrayMark()
    {
        RectTransform badge = SeedEventManager.UnresolvedBadge;
        if (badge != null && badge.gameObject.activeInHierarchy) return badge;

        EventsMenu menu = FindAnyObjectByType<EventsMenu>();
        Button opener = menu != null ? FindOpener(menu.Panel) : null;
        return opener != null ? (RectTransform)opener.transform : null;
    }

    private MenuTransition DoctrineMenu()
    {
        if (doctrineMenu != null) return doctrineMenu;

        MenuTransition[] menus = FindObjectsByType<MenuTransition>(FindObjectsInactive.Include);
        for (int i = 0; i < menus.Length; i++)
        {
            if (menus[i] == null || menus[i].gameObject.name != "DoctrineMenu") continue;
            doctrineMenu = menus[i];
            break;
        }

        return doctrineMenu;
    }

    private static void RevealCodex(Button unlock)
    {
        if (unlock != null && unlock.gameObject.activeInHierarchy) return;

        UnlockCodex codex = FindAnyObjectByType<UnlockCodex>(FindObjectsInactive.Include);
        if (codex != null && !codex.gameObject.activeSelf) codex.gameObject.SetActive(true);
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
