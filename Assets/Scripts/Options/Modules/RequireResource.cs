using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class RequireResource : OptionModule
{
    [SerializeField] private Resource resource;
    [SerializeField] private int required;
    [Tooltip("When enabled, the required amount is spent after the option passes its requirement check.")]
    [SerializeField] private bool consumeResource = true;

    public override bool CanExecute()
    {
        int amount = ResourceManager.Resources.GetResourceAmount(resource);
        return amount >= required;
    }

    public override void Execute(Option option, Seed seed)
    {
        if (consumeResource && required > 0)
        {
            ResourceManager.Resources.AddResource(resource, -required);
        }
    }

    public override void GetFixedResourceChanges(List<ResourceDelta> changes)
    {
        if (consumeResource && required > 0) ResourceDelta.Add(changes, resource, -required);
    }
}
