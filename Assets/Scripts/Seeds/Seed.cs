using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Seed", menuName = "Seeds/New Seed", order = 1)]
public class Seed : ScriptableObject
{
    [SerializeField] private int ticks = 1;
    [field: SerializeField] public List<Option> Options { get; private set; }

    [field: Space]

    [field: SerializeField] public string Title { get; private set; }
    [field: SerializeField, TextArea(10, 10)] public string Description { get; private set; }

    private Node currentNode;

    private int currentTicks = 0;

    public void Initialize(Node node)
    {
        currentNode = node;
        currentTicks = 0;

        for (int i = 0; i < Options.Count; i++)
        {
            Options[i].Initialize(this);
        }
    }

    public void Tick()
    {
        currentTicks += 1;

        if (currentTicks >= ticks)
        {
            SeedEventManager.EnqueueSeedEvent(this);
            currentNode.RemoveSeed(this);
        }
    }
}