using System;
using UnityEngine;

public class DebtDefection : MonoBehaviour
{
    [Tooltip("Faithful who leave each turn for every point of debt, all resources below 0 added up.")]
    [SerializeField, Min(0f)] private float flockLostPerDebt = 0.1f;

    [Header("Crowding")]
    [Tooltip("Faithful your cult can keep even without zones.")]
    [SerializeField, Min(0)] private int baseFlockCapacity = 100;
    [Tooltip("Extra faithful each zone you rule can shelter.")]
    [SerializeField, Min(0)] private int flockCapacityPerZone = 25;
    [Tooltip("Share of the faithful above capacity who drift away each turn.")]
    [SerializeField, Range(0f, 1f)] private float driftShare = 0.2f;

    public static Action<int> onFaithfulLeft;
    public static Action<int, int> onFaithfulDrifted;

    public int FlockCapacity => baseFlockCapacity + flockCapacityPerZone * (InfluenceManager.IsNull ? 0 : InfluenceManager.Get.CountControlledZones(FactionId.Player));

    private void OnEnable() => GameTime.OnTurnStarted += OnTurnStarted;

    private void OnDisable() => GameTime.OnTurnStarted -= OnTurnStarted;

    private void OnTurnStarted()
    {
        if (TutorialSession.SuppressAmbientEvents || ResourceManager.IsNull) return;

        Drift();
        int debt = 0;
        foreach (Resource resource in Enum.GetValues(typeof(Resource)))
        {
            if (SeedEventManager.IsInDebt(resource)) debt -= ResourceManager.Resources.GetResourceAmount(resource);
        }
        if (debt <= 0) return;

        int lost = Mathf.Max(1, Mathf.CeilToInt(debt * flockLostPerDebt));
        ResourceManager.AddResource(Resource.Flock, -lost);
        onFaithfulLeft?.Invoke(lost);
    }

    private void Drift()
    {
        int capacity = FlockCapacity;
        int excess = ResourceManager.Resources.GetResourceAmount(Resource.Flock) - capacity;
        if (excess <= 0) return;

        int drifted = Mathf.Max(1, Mathf.CeilToInt(excess * driftShare));
        ResourceManager.AddResource(Resource.Flock, -drifted);
        onFaithfulDrifted?.Invoke(drifted, capacity);
    }
}
