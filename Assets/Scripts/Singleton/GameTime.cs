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

    public static Action OnTurnResolving;
    public static Action OnTurnResolved;

    public static int CurrentTurn { get; private set; } = 1;

    private static bool processingTurn = false;
    private static bool confirmationPopUpOpen = false;

    protected override void OnInitialization()
    {
        CurrentTurn = 1;
        processingTurn = false;
        confirmationPopUpOpen = false;
    }

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

            popUp.TryGetElement<TextMeshProUGUI>("Description").text = "Some events still await your judgement. End the turn anyway? Your advisors will take the free option on each one.";

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
        OnTurnResolving?.Invoke();
        OnTurnEndedLate?.Invoke();
        OnTurnEnding?.Invoke();
        OnTurnEnded?.Invoke();
        CurrentTurn++;
        yield return new WaitForSeconds(turnStartDelay);
        OnTurnStarted?.Invoke();
        processingTurn = false;
        OnTurnResolved?.Invoke();
    }
}
