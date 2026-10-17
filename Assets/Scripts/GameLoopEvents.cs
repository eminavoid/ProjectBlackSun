using UnityEngine;
using UnityEngine.Events;

public class GameLoopEvents : MonoBehaviour
{
    [SerializeField] private PlayerResources playerResources;
    [SerializeField, TextArea] private string defeatMessage = "With no faithful left to follow you, your cult falls apart.";

    [Space]

    [SerializeField] private UnityEvent onLose;

    private bool lost;

    private void OnEnable() => GameTime.OnTurnResolved += OnTurnResolved;

    private void OnDisable() => GameTime.OnTurnResolved -= OnTurnResolved;

    private void OnTurnResolved()
    {
        if (lost || playerResources.GetResourceAmount(Resource.Flock) > 0) return;

        lost = true;
        GlobalEventBus.Invoke(new LoseEvent(defeatMessage, GameTime.CurrentTurn - 1));
        onLose?.Invoke();
    }
}
