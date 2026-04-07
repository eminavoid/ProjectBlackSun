using System.Collections.Generic;
using UnityEngine;

public class SeedEventManager : Singleton<SeedEventManager>
{
    [SerializeField] private RectTransform spawnOptionsRoot;
    [SerializeField] private OptionDisplay optionDisplayPrefab;

    private readonly Queue<Seed> seedEvents = new Queue<Seed>();

    public static void EnqueueSeedEvent(Seed seed)
    {
        Instance.seedEvents.Enqueue(seed);
    }

    private void Start()
    {
        GameTime.OnTurnStarted += OnTurnStarted;
    }

    public void OnOptionSelected()
    {
        foreach (Transform children in spawnOptionsRoot.transform)
        {
            Destroy(children.gameObject);
        }

        if (seedEvents.Count > 0)
        {
            StartChoosingOptionsPhase();
        }
    }

    private void OnTurnStarted()
    {
        StartChoosingOptionsPhase();
    }

    private void StartChoosingOptionsPhase()
    {
        if (seedEvents.Count <= 0)
        {
            Debug.Log("no qeued seeds this turn");
            return;
        }

        Debug.Log("new seed event");

        CreateSeedOptionsInCanvas(seedEvents.Dequeue());
    }

    private void CreateSeedOptionsInCanvas(Seed seed)
    {
        for (int i = 0; i < seed.Options.Count; i++)
        {
            OptionDisplay display = Instantiate(optionDisplayPrefab, spawnOptionsRoot);
            display.InitializeData(seed.Options[i]);
            display.onOptionSelected += OnOptionSelected;
        }
    }
}