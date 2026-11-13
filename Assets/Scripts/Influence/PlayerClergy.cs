using System;
using UnityEngine;

public class PlayerClergy : MonoBehaviour
{
    [Header("Ordination")]
    [Tooltip("Training points for a new cleric. Each turn the cult earns (2 x Learning + Stewardship) / 2, both stats counted from 10.")]
    [SerializeField, Min(1)] private int ordinationCost = 50;

    [Header("Upkeep")]
    [Tooltip("Wealth each cleric on the map costs per turn. Clerics in reserve cost nothing.")]
    [SerializeField, Min(0)] private int upkeepPerCleric = 2;

    public static Action<int> onOrdained;
    public static Action<int, int> onUpkeepPaid;

    private int training;

    public int UpkeepPerCleric => upkeepPerCleric;

    private void OnEnable() => GameTime.OnTurnEnded += OnTurnEnded;

    private void OnDisable() => GameTime.OnTurnEnded -= OnTurnEnded;

    private void OnTurnEnded()
    {
        if (InfluenceManager.IsNull || ResourceManager.IsNull) return;

        InfluenceManager influence = InfluenceManager.Get;
        if (!TutorialSession.SuppressAmbientEvents) PayUpkeep(influence);
        Train(influence);
    }

    private void PayUpkeep(InfluenceManager influence)
    {
        int clerics = influence.CountDeployedClerics(FactionId.Player);
        int cost = clerics * upkeepPerCleric;
        if (cost <= 0) return;

        ResourceManager.AddResource(Resource.Wealth, -cost);
        onUpkeepPaid?.Invoke(clerics, cost);
    }

    private void Train(InfluenceManager influence)
    {
        training += TrainingPerTurn(influence.Stats);
        int ordained = training / ordinationCost;
        if (ordained <= 0) return;

        training -= ordained * ordinationCost;
        influence.GetStock(FactionId.Player).ReturnClerics(ordained);
        onOrdained?.Invoke(ordained);
    }

    private static int TrainingPerTurn(PlayerStats stats)
    {
        int learning = 10 + (stats != null ? stats.GetStat(PlayerStats.PlayerStat.Learning) : 0);
        int stewardship = 10 + (stats != null ? stats.GetStat(PlayerStats.PlayerStat.Stewardship) : 0);
        return (2 * learning + stewardship) / 2;
    }
}
