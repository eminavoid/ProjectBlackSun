using UnityEngine;
using UnityEngine.Events;

public class GameLoopEvents : MonoBehaviour
{
    [SerializeField] private PlayerResources playerResources;

    [Space]

    [SerializeField] private UnityEvent onLose;

    private void Start()
    {
        GameTime.OnTurnEnded += OnTurnEnd;
    }

    private void OnTurnEnd()
    {
        if (playerResources.GetResourceAmount(Resource.Flock) <= 0)
        {
            onLose?.Invoke();
        }
    }
}