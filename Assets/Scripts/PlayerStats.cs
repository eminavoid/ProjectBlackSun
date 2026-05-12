using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Player Stats", menuName = "new Player Stats", order = 1)]
public class PlayerStats : ScriptableObject
{
    [field: SerializeField] public List<Doctrine> DoctrineInventory { get; private set; }
    [field: SerializeField] public int ExtraWeightPerStat { get; private set; } = 10;

    private readonly Dictionary<PlayerStat, int> playerStats = new Dictionary<PlayerStat, int>()
    {
        { PlayerStat.None, 0 },
        { PlayerStat.Diplomacy, 0 },
        { PlayerStat.Aggresion, 0 },
        { PlayerStat.Stewardship, 0 },
        { PlayerStat.Intrigue, 0 },
        { PlayerStat.Learning, 0 },
    };

    public int GetStatExtraWeight(PlayerStat stat) => GetStat(stat) * ExtraWeightPerStat;

    public int GetStat(PlayerStat stat) => Mathf.Max(0, playerStats[stat]);
    
    public void ChangeStat(PlayerStat stat, int value)
    {
        if (playerStats.ContainsKey(stat))
        {
            playerStats[stat] += value;
        }
    }

    public void ResetValues()
    {
        foreach (PlayerStat playerStat in playerStats.Keys)
        {
            playerStats[playerStat] = 0;
        }
    }

    public enum PlayerStat
    {
        None,
        Diplomacy,
        Aggresion, 
        Stewardship, 
        Intrigue, 
        Learning
    }
}