using UnityEngine;
using System;

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
}
