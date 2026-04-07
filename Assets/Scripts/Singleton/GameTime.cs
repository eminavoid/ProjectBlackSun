using System.Collections;
using UnityEngine;
using System;

public class GameTime : Singleton<GameTime>
{
    [SerializeField] private float turnStartDelay;

    public static Action OnTurnEnded;
    public static Action OnTurnStarted;

    private static bool processingTurn = false;

    public static void NextTurn()
    {
        if (processingTurn) return;

        Instance.StartCoroutine(Instance.NextTurnCoroutine());
    }

    private IEnumerator NextTurnCoroutine()
    {
        processingTurn = true;
        OnTurnEnded?.Invoke();
        yield return new WaitForSeconds(turnStartDelay);
        OnTurnStarted?.Invoke();
        processingTurn = false;
    }
}