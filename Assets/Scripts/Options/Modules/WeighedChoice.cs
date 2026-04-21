using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class WeighedChoice : OptionModule
{
    [SerializeField] private List<WeightedElement> modules;

    public override bool CanExecute() => true;

    public override void Execute(Seed seed)
    {
        if (modules.Count != 0)
        {
            WeightedElement element = WeightedSelect.SelectElement(modules);

            element.module.Execute(seed);
            SeedEventManager.CreateEventOutputWindow(element.output);
        }
    }

    [Serializable]
    private struct WeightedElement : IWeighted
    {
        public int weight;
        [SerializeReferenceDropdown, SerializeReference] public OptionModule module;

        [Space]

        [SerializeField, TextArea(3, 5)] public string output;

        public readonly int Weight => weight;
    }
}