using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Separa decisión de ejecución en las IAs para poder telegrafiar sus jugadas.
/// Orquesta el orden explícitamente en lugar de que cada IA se suscriba a GameTime por su cuenta.
///
/// Turno N: se planifica al inicio (flechas visibles), los clérigos se commitean antes de la
/// resolución y las seeds justo después. Es el mismo timing que tenían antes, así que el balance
/// no cambia: sólo se ve venir la jugada.
/// </summary>
[DefaultExecutionOrder(60)]
public class AIIntentBoard : Singleton<AIIntentBoard>
{
    [SerializeField] private bool logIntents;

    private readonly List<AIIntent> intents = new List<AIIntent>();

    private AIInfluenceController clericAi;
    private DebugAI seedAi;

    public static AIIntentBoard Get => Instance;

    public IReadOnlyList<AIIntent> Intents => intents;

    public event Action OnIntentsChanged;

    private void Start()
    {
        GameTime.OnTurnEnding += CommitClericIntents;
        GameTime.OnTurnStarted += OnTurnStarted;

        // El jugador debe ver intención desde el primer turno, no recién en el segundo.
        PlanTurn();
    }

    private void OnDestroy()
    {
        GameTime.OnTurnEnding -= CommitClericIntents;
        GameTime.OnTurnStarted -= OnTurnStarted;
    }

    private void OnTurnStarted()
    {
        CommitSeedIntents();
        PlanTurn();
    }

    /// <summary>Antes de la resolución: los clérigos ya generan influencia este mismo turno.</summary>
    private void CommitClericIntents()
    {
        ResolveAgents();
        if (clericAi != null) clericAi.PrepareCommit();
        Commit(AIIntentKind.AssignClerics);
    }

    /// <summary>Después de la resolución: la seed empieza a tickear el turno siguiente.</summary>
    private void CommitSeedIntents()
    {
        Commit(AIIntentKind.PlantSeed);
    }

    private void Commit(AIIntentKind kind)
    {
        ResolveAgents();

        bool changed = false;

        for (int i = intents.Count - 1; i >= 0; i--)
        {
            AIIntent intent = intents[i];
            if (intent.Kind != kind) continue;

            intents.RemoveAt(i);
            changed = true;

            bool executed = kind == AIIntentKind.AssignClerics
                ? clericAi != null && clericAi.ExecuteIntent(intent)
                : seedAi != null && seedAi.ExecuteIntent(intent);

            if (logIntents)
            {
                Debug.Log(
                    $"AIIntentBoard: {(executed ? "ejecutada" : "descartada")} {Describe(intent)}",
                    this);
            }
        }

        if (changed) OnIntentsChanged?.Invoke();
    }

    private void PlanTurn()
    {
        ResolveAgents();

        intents.Clear();

        if (clericAi != null) clericAi.PlanIntents(intents);
        if (seedAi != null) seedAi.PlanIntents(intents);

        for (int i = intents.Count - 1; i >= 0; i--)
        {
            if (!intents[i].IsValid) intents.RemoveAt(i);
        }

        if (logIntents)
        {
            for (int i = 0; i < intents.Count; i++)
            {
                Debug.Log($"AIIntentBoard: planificada {Describe(intents[i])}", this);
            }
        }

        OnIntentsChanged?.Invoke();
    }

    private void ResolveAgents()
    {
        if (clericAi == null) clericAi = FindAnyObjectByType<AIInfluenceController>();
        if (seedAi == null) seedAi = FindAnyObjectByType<DebugAI>();
    }

    private static string Describe(AIIntent intent)
    {
        string target = intent.Target != null ? $"{intent.Target.SectorName} ({intent.Target.District})" : "sin zona";

        return intent.Kind == AIIntentKind.AssignClerics
            ? $"{intent.Label}: +{intent.Amount} clérigo(s) → {target}"
            : $"{intent.Label}: seed → {target}";
    }
}
