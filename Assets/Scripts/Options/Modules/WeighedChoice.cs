using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class WeighedChoice : OptionModule
{
    [SerializeField] private List<WeightedElement> modules;

    private readonly List<WeightedElementWrapper> tempModules = new List<WeightedElementWrapper>();

    public override bool CanExecute() => true;

    public override void Execute(Option option, Seed seed)
    {
        tempModules.Clear();

        for (int i = 0; i < modules.Count; i++)
        {
            tempModules.Add(new WeightedElementWrapper(modules[i], option));
        }

        if (tempModules.Count != 0)
        {
            WeightedElement element = WeightedSelect.SelectElement(tempModules).weightedElement;

            // Only this outcome's effects: the option's cost was already shown on its button.
            // Measured per module so refunds stay apart from gains of the same resource.
            List<ResourceDelta> changes = new List<ResourceDelta>();
            for (int i = 0; i < element.module.Count; i++)
            {
                Dictionary<Resource, int> before = ResourceManager.Resources.Snapshot();
                element.module[i].Execute(option, seed);
                ResourceManager.Resources.GetChangesSince(before, changes, element.module[i].IsRefund);
            }

            SeedEventManager.CreateEventOutputWindow(seed.Title, element.output, changes);
        }
    }

    [Serializable]
    private struct WeightedElement : IWeighted
    {
        public int weight;
        [SerializeReferenceDropdown, SerializeReference] public List<OptionModule> module;
        [SerializeField] public PlayerStats.PlayerStat statUsed;

        [Space]

        [SerializeField, TextArea(3, 5)] public string output;

        public readonly int Weight => weight;
    }

    //Wrapper so we don't have to redo all the options after this change
    private readonly struct WeightedElementWrapper : IWeighted
    {
        public readonly WeightedElement weightedElement;
        public readonly Option optionRef;

        public readonly int Weight => weightedElement.Weight + optionRef.PlayerStats.GetStatExtraWeight(weightedElement.statUsed);

        public WeightedElementWrapper(WeightedElement weightedElement, Option optionRef)
        {
            this.weightedElement = weightedElement;
            this.optionRef = optionRef;
        }
    }
}