using System;
using UnityEngine;

public class DebtDefection : MonoBehaviour
{
    [Tooltip("Faithful who leave each turn for every point of debt, all resources below 0 added up.")]
    [SerializeField, Min(0f)] private float flockLostPerDebt = 0.1f;

    public static Action<int> onFaithfulLeft;

    private void OnEnable() => GameTime.OnTurnStarted += OnTurnStarted;

    private void OnDisable() => GameTime.OnTurnStarted -= OnTurnStarted;

    private void OnTurnStarted()
    {
        if (TutorialSession.SuppressAmbientEvents || ResourceManager.IsNull) return;

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
}
