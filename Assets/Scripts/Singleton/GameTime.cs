using System.Collections;
using UnityEngine;
using System;
using Zeke.UI;
using TMPro;
using UnityEngine.UI;



#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class GameTime : Singleton<GameTime>
{
    [SerializeField] private float turnStartDelay;
    [SerializeField] private UIWindow seedConfirmationPopUp;

    /// <summary>Se dispara antes de OnTurnEnded: acá se commitean las jugadas planificadas
    /// para que la resolución del turno las tenga en cuenta.</summary>
    public static Action OnTurnEnding;
    public static Action OnTurnEnded;
    public static Action OnTurnStarted;

    public static Action OnTurnEndedLate;

    private static bool processingTurn = false;
    private static bool confirmationPopUpOpen = false;

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
        if (confirmationPopUpOpen) return;
        if (SeedEventManager.HasUnresolvedEvents)
        {
            confirmationPopUpOpen = true;

            UIWindow popUp = Instantiate(Instance.seedConfirmationPopUp, GlobalReferences.ScreenCanvas.transform);

            popUp.TryGetElement<TextMeshProUGUI>("Description").text = "There are still events left, continue and auto-resolve all of them? The free option will be automatically chosen";

            Button confirmButton = popUp.TryGetElement<Button>("Confirm Button");

            confirmButton.onClick.AddListener(Instance.ForceNextTurn);
            confirmButton.onClick.AddListener(() => Destroy(popUp.gameObject));
            confirmButton.onClick.AddListener(() => confirmationPopUpOpen = false);

            Button cancelButton = popUp.TryGetElement<Button>("Cancel Button");

            cancelButton.onClick.AddListener(() => Destroy(popUp.gameObject));
            cancelButton.onClick.AddListener(() => confirmationPopUpOpen = false);
        }
        else
        {
            Instance.StartCoroutine(Instance.NextTurnCoroutine());
        }
    }

    private void ForceNextTurn()
    {
        StartCoroutine(Instance.NextTurnCoroutine());
    }

    private IEnumerator NextTurnCoroutine()
    {
        processingTurn = true;
        OnTurnEndedLate?.Invoke();
        OnTurnEnding?.Invoke();
        OnTurnEnded?.Invoke();
        yield return new WaitForSeconds(turnStartDelay);
        OnTurnStarted?.Invoke();
        processingTurn = false;
    }
}
