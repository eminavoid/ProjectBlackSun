using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SettleDebt : OptionModule
{
    [SerializeField] private Resource resource;
    [Tooltip("Flock paid for each point of debt cleared.")]
    [SerializeField, Min(0f)] private float flockPerDebt = 0.5f;

    public override bool CanExecute()
    {
        int debt = Debt;
        return debt > 0 && FlockCost(debt) < ResourceManager.Resources.GetResourceAmount(Resource.Flock);
    }

    public override void Execute(Option option, Seed seed)
    {
        int debt = Debt;
        if (debt <= 0) return;

        ResourceManager.Resources.AddResource(resource, debt);
        ResourceManager.Resources.AddResource(Resource.Flock, -FlockCost(debt));
    }

    public override void GetFixedResourceChanges(List<ResourceDelta> changes)
    {
        int debt = Debt;
        if (debt <= 0) return;

        ResourceDelta.Add(changes, Resource.Flock, -FlockCost(debt));
        ResourceDelta.Add(changes, resource, debt);
    }

    private int Debt => Mathf.Max(0, -ResourceManager.Resources.GetResourceAmount(resource));

    private int FlockCost(int debt) => Mathf.CeilToInt(debt * flockPerDebt);
}
