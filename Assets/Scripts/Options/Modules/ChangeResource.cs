using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class ChangeResource : OptionModule
{
    [SerializeField] private Resource resource;
    [SerializeField] private int minAmount;
    [SerializeField] private int maxAmount;
    [Tooltip("Gives back (part of) the option's cost. The result window shows it apart from the gains, in grey.")]
    [SerializeField] private bool refund;

    public override bool IsRefund => refund;

    public override bool CanExecute() => true;

    public override void Execute(Option option, Seed seed)
    {
        int resourceAmount = UnityEngine.Random.Range(minAmount, maxAmount + 1);
        ResourceManager.Resources.AddResource(resource, resourceAmount);
    }

    // A random range isn't known until it runs, so only fixed amounts are shown up front.
    public override void GetFixedResourceChanges(List<ResourceDelta> changes)
    {
        if (minAmount == maxAmount && minAmount != 0) ResourceDelta.Add(changes, resource, minAmount, refund);
    }
}