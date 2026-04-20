using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Option", menuName = "Options/New Option", order = 1)]
public class Option : ScriptableObject
{
    [SerializeReferenceDropdown, SerializeReference] private List<OptionModule> modules;

    private Seed seed;

    public void Initialize(Seed seed)
    {
        this.seed = seed;
    }

    public void ExecuteOption()
    {
        for (int i = 0; i < modules.Count; i++)
        {
            modules[i]?.Execute(seed);
        }
    }
}