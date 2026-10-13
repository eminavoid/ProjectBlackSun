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
        params Districts[] districts)
    {
        faction = factionId;
        displayName = name;
        startingClerics = startClerics;
        clericsPerTurn = perTurn;
        maxAssignPerTurn = maxAssign;
        preferredDistricts = new List<Districts>();
        if (districts == null) return;

        for (int i = 0; i < districts.Length; i++)
        {
            preferredDistricts.Add(districts[i]);
        }
    }

    public static AIInfluenceProfile CreateRuntime(
        FactionId factionId,
        string name,
        int startClerics,
        params Districts[] districts)
    {
        AIInfluenceProfile profile = CreateInstance<AIInfluenceProfile>();
        profile.hideFlags = HideFlags.HideAndDontSave;
        profile.name = name;
        profile.RuntimeInit(factionId, name, startClerics, 1, 2, districts);
        return profile;
    }
}
