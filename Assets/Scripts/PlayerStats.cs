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
        List<PlayerStat> stats = new List<PlayerStat>(playerStats.Keys);
        for (int i = 0; i < stats.Count; i++)
        {
            playerStats[stats[i]] = 0;
        }
    }

    public void RememberDoctrine(Doctrine doctrine)
    {
        if (doctrine == null) return;
        if (DoctrineInventory == null) DoctrineInventory = new List<Doctrine>();
        if (!DoctrineInventory.Contains(doctrine)) DoctrineInventory.Add(doctrine);
    }

    public void ForgetDoctrine(Doctrine doctrine)
    {
        if (doctrine == null || DoctrineInventory == null) return;
        DoctrineInventory.Remove(doctrine);
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