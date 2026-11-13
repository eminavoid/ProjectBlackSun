using UnityEngine;
using UnityEngine.Events;

public class GameLoopEvents : MonoBehaviour
{
    [SerializeField] private PlayerResources playerResources;
    [SerializeField, TextArea] private string defeatMessage = "With no faithful left to follow you, your cult falls apart.";

    [Header("Faith Eclipse")]
    [Tooltip("The eclipse comes when this turn ends. The faith controlling the most zones wins.")]
    [SerializeField, Min(1)] private int eclipseTurn = 30;
    [SerializeField] private string victoryTitle = "YOUR CULT ASCENDS";
    [SerializeField, TextArea] private string victoryMessage = "The Beholder opens its eye and finds your faith ruling Beheld. Your pontiff ascends to sainthood.";
    [SerializeField] private string eclipseDefeatTitle = "THE ECLIPSE HAS PASSED";
    [Tooltip("{0} is the faith that rules the city.")]
    [SerializeField, TextArea] private string eclipseDefeatMessage = "When the Beholder opened its eye, {0} ruled Beheld. Your cult will wait five more centuries.";
    [SerializeField, TextArea] private string eclipseTieMessage = "When the Beholder opened its eye, no faith ruled Beheld alone. Your cult will wait five more centuries.";

    [Space]

    [SerializeField] private UnityEvent onLose;

    private bool over;

    public int EclipseTurn => eclipseTurn;

    public int TurnsUntilEclipse => Mathf.Max(0, eclipseTurn - GameTime.CurrentTurn + 1);

    private void OnEnable()
    {
        GameTime.OnTurnResolved += OnTurnResolved;
        playerResources.onResourceGained += OnResourceChanged;
    }

    private void OnDisable()
    {
        GameTime.OnTurnResolved -= OnTurnResolved;
        playerResources.onResourceGained -= OnResourceChanged;
    }

    private void OnResourceChanged(Resource resource, int amount)
    {
        if (over || resource != Resource.Flock) return;
        if (playerResources.GetResourceAmount(Resource.Flock) > 0) return;

        over = true;
        GlobalEventBus.Invoke(new LoseEvent(defeatMessage, GameTime.CurrentTurn - 1));
        onLose?.Invoke();
    }

    private void OnTurnResolved()
    {
        if (over || GameTime.CurrentTurn <= eclipseTurn) return;

        over = true;
        ResolveEclipse();
    }

    private void ResolveEclipse()
    {
        FactionId? leader = FaithEclipseTracker.GetLeader(out int leaderZones);
        int playerZones = FaithEclipseTracker.GetControlledZoneCount(FactionId.Player);
        string standing = $"Your cult ruled {Zones(playerZones)} when the eclipse came.";

        if (leader.HasValue && FactionIdUtil.IsPlayer(leader.Value))
        {
            GlobalEventBus.Invoke(new VictoryEvent(victoryTitle, victoryMessage, standing));
            return;
        }

        string message = leader.HasValue
            ? string.Format(eclipseDefeatMessage, FactionIdUtil.DisplayName(leader.Value))
            : eclipseTieMessage;
        if (leader.HasValue) standing += $" {FactionIdUtil.DisplayName(leader.Value)} ruled {Zones(leaderZones)}.";
        else if (leaderZones > 0) standing += $" The leading faiths were tied at {Zones(leaderZones)}.";

        GlobalEventBus.Invoke(new LoseEvent(message, GameTime.CurrentTurn - 1, eclipseDefeatTitle, standing));
        onLose?.Invoke();
    }

    private static string Zones(int count) => count == 1 ? "1 zone" : $"{count} zones";
}
