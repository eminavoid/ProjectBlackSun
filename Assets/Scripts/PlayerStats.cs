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
        { PlayerStat.Value1, 0 },
        { PlayerStat.Value2, 0 },
        { PlayerStat.Value3, 0 },
        { PlayerStat.Value4, 0 },
        { PlayerStat.Value5, 0 },
    };

    public int GetStatExtraWeight(PlayerStat stat) => GetStat(stat) * ExtraWeightPerStat;

    public int GetStat(PlayerStat stat) => Mathf.Max(0, playerStats[stat]);
    
    public void ChangeStat(PlayerStat stat, int value)
    {
        if (playerStats.ContainsKey(stat))
        {
            playerStats[stat] += value;
        }

        Debug.Log(GetStat(PlayerStat.Value1));
        Debug.Log(GetStat(PlayerStat.Value2));
        Debug.Log(GetStat(PlayerStat.Value3));
        Debug.Log(GetStat(PlayerStat.Value4));
        Debug.Log(GetStat(PlayerStat.Value5));
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
        Value1,
        Value2, 
        Value3, 
        Value4, 
        Value5
    }
}