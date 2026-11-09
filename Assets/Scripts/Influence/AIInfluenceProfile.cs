using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class AIObjective
{
    public Districts district;
    public int weight = 1;

    public static AIObjective Of(Districts district, int weight)
    {
        return new AIObjective
        {
            district = district,
            weight = Mathf.Max(0, weight)
        };
    }
}

[CreateAssetMenu(fileName = "AI Influence Profile", menuName = "Influence/AI Influence Profile", order = 2)]
public class AIInfluenceProfile : ScriptableObject
{
    [SerializeField] private FactionId faction = FactionId.Rival1;
    [SerializeField] private string displayName = "Rival";
    [SerializeField] private int startingClerics = 15;
    [SerializeField] private int clericsPerTurn = 1;
    [SerializeField] private int maxAssignPerTurn = 2;
    [SerializeField] private List<AIObjective> objectives = new List<AIObjective>();

    [SerializeField, HideInInspector] private List<Districts> preferredDistricts = new List<Districts>();

    public FactionId Faction => faction;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? faction.ToString() : displayName;
    public int StartingClerics => startingClerics;
    public int ClericsPerTurn => clericsPerTurn;
    public int MaxAssignPerTurn => maxAssignPerTurn;
    public IReadOnlyList<AIObjective> Objectives
    {
        get
        {
            MigrateLegacyDistricts();
            return objectives;
        }
    }

    public IReadOnlyList<Districts> PreferredDistricts
    {
        get
        {
            MigrateLegacyDistricts();
            List<AIObjective> ranked = new List<AIObjective>();
            if (objectives != null)
            {
                for (int i = 0; i < objectives.Count; i++)
                {
                    if (objectives[i] != null) ranked.Add(objectives[i]);
                }
            }

            ranked.Sort((a, b) =>
            {
                int byWeight = b.weight.CompareTo(a.weight);
                return byWeight != 0 ? byWeight : a.district.CompareTo(b.district);
            });

            List<Districts> ordered = new List<Districts>();
            for (int i = 0; i < ranked.Count; i++)
            {
                if (!ordered.Contains(ranked[i].district)) ordered.Add(ranked[i].district);
            }

            return ordered;
        }
    }

    public int WeightOf(Districts district)
    {
        MigrateLegacyDistricts();
        if (objectives == null) return 0;

        int best = 0;
        bool found = false;
        for (int i = 0; i < objectives.Count; i++)
        {
            AIObjective objective = objectives[i];
            if (objective == null || objective.district != district) continue;
            found = true;
            if (objective.weight > best) best = objective.weight;
        }

        return found ? best : 0;
    }

    public bool Prefers(Districts district)
    {
        MigrateLegacyDistricts();
        if (objectives == null || objectives.Count == 0) return true;
        return WeightOf(district) > 0;
    }

    private void OnEnable()
    {
        MigrateLegacyDistricts();
    }

    private void MigrateLegacyDistricts()
    {
        if (objectives != null && objectives.Count > 0) return;
        if (preferredDistricts == null || preferredDistricts.Count == 0) return;

        objectives = new List<AIObjective>();
        for (int i = 0; i < preferredDistricts.Count; i++)
        {
            objectives.Add(AIObjective.Of(preferredDistricts[i], preferredDistricts.Count - i));
        }
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
        objectives = new List<AIObjective>();
        preferredDistricts = new List<Districts>();
        if (districts == null) return;

        for (int i = 0; i < districts.Length; i++)
        {
            objectives.Add(AIObjective.Of(districts[i], districts.Length - i));
            preferredDistricts.Add(districts[i]);
        }
    }

    public void RuntimeInit(
        FactionId factionId,
        string name,
        int startClerics,
        int perTurn,
        int maxAssign,
        params AIObjective[] ranked)
    {
        faction = factionId;
        displayName = name;
        startingClerics = startClerics;
        clericsPerTurn = perTurn;
        maxAssignPerTurn = maxAssign;
        objectives = new List<AIObjective>();
        preferredDistricts = new List<Districts>();
        if (ranked == null) return;

        for (int i = 0; i < ranked.Length; i++)
        {
            if (ranked[i] == null) continue;
            objectives.Add(ranked[i]);
            if (!preferredDistricts.Contains(ranked[i].district))
            {
                preferredDistricts.Add(ranked[i].district);
            }
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

    public static AIInfluenceProfile CreateRuntime(
        FactionId factionId,
        string name,
        int startClerics,
        params AIObjective[] ranked)
    {
        AIInfluenceProfile profile = CreateInstance<AIInfluenceProfile>();
        profile.hideFlags = HideFlags.HideAndDontSave;
        profile.name = name;
        profile.RuntimeInit(factionId, name, startClerics, 1, 2, ranked);
        return profile;
    }
}
