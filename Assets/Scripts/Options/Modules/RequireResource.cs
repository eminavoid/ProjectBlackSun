using UnityEngine;
using System;

[Serializable]
public class RequireResource : OptionModule
{
    [SerializeField] private Resource resource;
    [SerializeField] private int required;

    public override bool CanExecute()
    {
        int amount = ResourceManager.Resources.GetResourceAmount(resource);;
        return amount >= required;
    }

    public override void Execute(Seed seed) { }
}