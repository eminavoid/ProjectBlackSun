using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AI Influence Profile", menuName = "Influence/AI Influence Profile", order = 2)]
public class AIInfluenceProfile : ScriptableObject
{
    [SerializeField] private FactionId faction = FactionId.Rival1;
    [SerializeField] private string displayName = "Rival";
    [SerializeField] private int startingClerics = 15;
    [SerializeField] private int clericsPerTurn = 1;
    [SerializeField] private int maxAssignPerTurn = 2;
    [SerializeField] private List<Districts> preferredDistricts = new List<Districts>();

    public FactionId Faction => faction;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? faction.ToString() : displayName;
    public int StartingClerics => startingClerics;
    public int ClericsPerTurn => clericsPerTurn;
    public int MaxAssignPerTurn => maxAssignPerTurn;
    public IReadOnlyList<Districts> PreferredDistricts => preferredDistricts;

    public bool Prefers(Districts district)
    {
        if (preferredDistricts == null || preferredDistricts.Count == 0) return true;
        return preferredDistricts.Contains(district);
    }

    public void RuntimeInit(
        FactionId factionId,
        string name,
        int startClerics,
        int perTurn,
        int maxAssign,
        Districts primary,
        Districts secondary)
    {
        faction = factionId;
        displayName = name;
        startingClerics = startClerics;
        clericsPerTurn = perTurn;
        maxAssignPerTurn = maxAssign;
        preferredDistricts = new List<Districts> { primary, secondary };
    }

    public static AIInfluenceProfile CreateRuntime(
        FactionId factionId,
        string name,
        Districts primary,
        Districts secondary,
        int startClerics = 15)
    {
        AIInfluenceProfile profile = CreateInstance<AIInfluenceProfile>();
        profile.hideFlags = HideFlags.HideAndDontSave;
        profile.name = name;
        profile.RuntimeInit(factionId, name, startClerics, 1, 2, primary, secondary);
        return profile;
    }
}
