using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Seed", menuName = "Seeds/New Seed", order = 1)]
public class Seed : ScriptableObject
{
    [Header("Display")]
    [SerializeField] private string title;
    [SerializeField, TextArea(3, 6)] private string description;
    [SerializeField] private Sprite icon;

    [Header("Gameplay")]
    [SerializeField] private int ticks = 1;
    [field: SerializeField] public List<Option> Options { get; private set; }
    public int Ticks => ticks;
    public string Title => string.IsNullOrWhiteSpace(title) ? name : title;
    public string Description => description;
    public Sprite Icon => icon;
    public Node CurrentNode => currentNode;
    public Districts? CurrentDistrict => currentNode != null ? currentNode.District : null;

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
            Debug.Log($"Seed '{Title}' completed in node '{currentNode?.name ?? "Unknown"}' after {ticks} turn(s).");
            SeedEventManager.EnqueueSeedEvent(this);
            currentNode.RemoveSeed(this);
        }
    }
}