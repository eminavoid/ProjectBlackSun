using System.Collections.Generic;
using UnityEngine;

public enum SeedEventType
{
    Economic,
    Social,
    Security
}

public enum SeedDifficulty
{
    Easy = 1,
    Medium = 2,
    Hard = 3
}

[CreateAssetMenu(fileName = "Seed", menuName = "Seeds/New Seed", order = 1)]
public class Seed : ScriptableObject
{
    [Header("Display")]
    [SerializeField] private string title;
    [SerializeField, TextArea(3, 6)] private string description;
    [SerializeField] private Sprite icon;

    [Header("Gameplay")]
    [SerializeField] private int ticks = 1;
    [Header("Classification")]
    [SerializeField] private SeedEventType eventType;
    [SerializeField] private SeedDifficulty difficulty;
    [field: SerializeField] public List<Option> Options { get; private set; }
    public int Ticks => ticks;
    public SeedEventType EventType => eventType;
    public SeedDifficulty Difficulty => difficulty;
    public int DifficultyValue => (int)difficulty;
    public string Title => string.IsNullOrWhiteSpace(title) ? name : title;
    public string Description => description;
    public Sprite Icon => icon;
    public DistrictZone CurrentZone => currentZone;
    public Districts? CurrentDistrict => currentZone != null ? currentZone.District : null;
    public int CurrentTicks => currentTicks;
    public int TurnsRemaining => Mathf.Max(0, ticks - currentTicks);
    public bool IsPlanted => currentZone != null;

    public bool CanPlantInDistrict(Districts district) => true;

    private DistrictZone currentZone;

    private int currentTicks = 0;

    public void Initialize(DistrictZone zone)
    {
        currentZone = zone;
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
            Debug.Log($"Seed '{Title}' completed in sector '{currentZone?.SectorName ?? "Unknown"}' after {ticks} turn(s).");
            SeedEventManager.EnqueueSeedEvent(this);
            currentZone.RemoveSeed(this);
        }
    }
}
