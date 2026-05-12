using UnityEngine;

[CreateAssetMenu(fileName = "AI Profile", menuName = "AI/New AI Profile", order = 1)]
public class AIProfile : ScriptableObject
{
    [SerializeField] private SeedEventType primaryType = SeedEventType.Economic;
    [SerializeField] private SeedDifficulty maxDifficulty = SeedDifficulty.Medium;

    public SeedEventType PrimaryType => primaryType;
    public SeedDifficulty MaxDifficulty => maxDifficulty;

    public bool CanUse(Seed seed)
    {
        if (seed == null) return false;
        if (seed.EventType != primaryType) return false;
        if (seed.Difficulty > maxDifficulty) return false;
        return true;
    }
}
