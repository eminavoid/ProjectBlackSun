using System;
using UnityEngine;

public class PlayerClergy : MonoBehaviour
{
    [Header("Ordination")]
    [Tooltip("Training points for a new cleric. Each turn the cult earns (2 x Learning + Stewardship) / 2, rounded up, both stats counted from 10.")]
    [SerializeField, Min(1)] private int ordinationCost = 35;
    [Tooltip("Wealth to ordain a cleric at once from the clerics menu.")]
    [SerializeField, Min(0)] private int ordainPrice = 60;
    [Tooltip("Secrets to ordain a cleric at once from the clerics menu.")]
    [SerializeField, Min(0)] private int ordainSecretsPrice = 40;

    [Header("Upkeep")]
    [Tooltip("Wealth each cleric on the map costs per turn. Clerics in reserve cost nothing.")]
    [SerializeField, Min(0)] private int upkeepPerCleric = 2;
    [Tooltip("Clerics each zone you rule feeds for free. Only the clerics beyond that pay upkeep.")]
    [SerializeField, Min(0)] private int clericsFedPerZone = 1;
    [Tooltip("Faithful needed to feed one more cleric for free, on top of the ones your zones feed.")]
    [SerializeField, Min(1)] private int flockPerFedCleric = 75;

    public static Action<int> onOrdained;
    public static Action<int, int> onUpkeepPaid;

    private int training;

    public int UpkeepPerCleric => upkeepPerCleric;

    public int FlockPerFedCleric => flockPerFedCleric;

    public int UnfedClerics(InfluenceManager influence)
    {
        if (influence == null) return 0;
        int flock = ResourceManager.IsNull ? 0 : Mathf.Max(0, ResourceManager.Resources.GetResourceAmount(Resource.Flock));
        int fed = influence.CountControlledZones(FactionId.Player) * clericsFedPerZone + flock / flockPerFedCleric;
        return Mathf.Max(0, influence.CountDeployedClerics(FactionId.Player) - fed);
    }

    public int OrdinationCost => ordinationCost;
    public int OrdainPrice => ordainPrice;
    public int OrdainSecretsPrice => ordainSecretsPrice;
    public int Training => training;
    public int TrainingPerTurn => InfluenceManager.IsNull ? 0 : TrainingFrom(InfluenceManager.Get.Stats);

    private void OnEnable() => GameTime.OnTurnEnded += OnTurnEnded;

    private void OnDisable() => GameTime.OnTurnEnded -= OnTurnEnded;

    public bool TryOrdain() => TryOrdain(Resource.Wealth, ordainPrice);

    public bool TryOrdainWithSecrets() => TryOrdain(Resource.Secrets, ordainSecretsPrice);

    private bool TryOrdain(Resource resource, int price)
    {
        if (InfluenceManager.IsNull || ResourceManager.IsNull) return false;
        if (ResourceManager.Resources.GetResourceAmount(resource) < price) return false;

        ResourceManager.AddResource(resource, -price);
        InfluenceManager.Get.GetStock(FactionId.Player).ReturnClerics(1);
        return true;
    }

    private void OnTurnEnded()
    {
        if (InfluenceManager.IsNull || ResourceManager.IsNull) return;

        InfluenceManager influence = InfluenceManager.Get;
        if (!TutorialSession.SuppressAmbientEvents) PayUpkeep(influence);
        Train(influence);
    }

    private void PayUpkeep(InfluenceManager influence)
    {
        int clerics = UnfedClerics(influence);
        int cost = clerics * upkeepPerCleric;
        if (cost <= 0) return;

        ResourceManager.AddResource(Resource.Wealth, -cost);
        onUpkeepPaid?.Invoke(clerics, cost);
    }

    private void Train(InfluenceManager influence)
    {
        training += TrainingFrom(influence.Stats);
        int ordained = training / ordinationCost;
        if (ordained <= 0) return;

        training -= ordained * ordinationCost;
        influence.GetStock(FactionId.Player).ReturnClerics(ordained);
        onOrdained?.Invoke(ordained);
    }

    private static int TrainingFrom(PlayerStats stats)
    {
        int learning = stats != null ? stats.GetStat(PlayerStats.PlayerStat.Learning) : 0;
        int stewardship = stats != null ? stats.GetStat(PlayerStats.PlayerStat.Stewardship) : 0;
        return TrainingFor(learning, stewardship);
    }

    public static int TrainingFor(int learningPoints, int stewardshipPoints)
    {
        return (2 * (10 + learningPoints) + 10 + stewardshipPoints + 1) / 2;
    }
}
