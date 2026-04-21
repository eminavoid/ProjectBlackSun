using UnityEngine;
using System;

[Serializable]
public class ChangeResource : OptionModule
{
    [SerializeField] private Resource resource;
    [SerializeField] private int minAmount;
    [SerializeField] private int maxAmount;

    public override bool CanExecute() => true;

    public override void Execute(Seed seed)
    {
        int resourceAmount = UnityEngine.Random.Range(minAmount, maxAmount + 1);
        ResourceManager.Resources.AddResource(resource, resourceAmount);
    }
}