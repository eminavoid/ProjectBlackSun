using System.Collections;
using UnityEngine;
using System;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class GameTime : Singleton<GameTime>
{
    [SerializeField] private float turnStartDelay;

    /// <summary>Se dispara antes de OnTurnEnded: acá se commitean las jugadas planificadas
    /// para que la resolución del turno las tenga en cuenta.</summary>
    public static Action OnTurnEnding;
    public static Action OnTurnEnded;
    public static Action OnTurnStarted;

    private static bool processingTurn = false;

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
#else
        if (Input.GetKeyDown(KeyCode.Escape))
#endif
        {
            QuitGame();
        }
    }

    private static void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public static void NextTurn()
    {
        if (processingTurn) return;

        Instance.StartCoroutine(Instance.NextTurnCoroutine());
    }

    private IEnumerator NextTurnCoroutine()
    {
        processingTurn = true;
        OnTurnEnding?.Invoke();
        OnTurnEnded?.Invoke();
        yield return new WaitForSeconds(turnStartDelay);
        OnTurnStarted?.Invoke();
        processingTurn = false;
    }
}
