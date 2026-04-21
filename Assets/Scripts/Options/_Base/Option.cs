using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Option", menuName = "Options/New Option", order = 1)]
public class Option : ScriptableObject
{
    [SerializeReferenceDropdown, SerializeReference] private List<OptionModule> modules;

    [field: SerializeField] public string Title { get; private set; }
    [field: SerializeField, TextArea(10, 10)] public string Description { get; private set; }

    private Seed seed;

    public void Initialize(Seed seed)
    {
        this.seed = seed;
    }

    public bool CanExecute()
    {
        for (int i = 0; i < modules.Count; i++)
        {
            if (!modules[i].CanExecute())
            {
                return false;
            }
        }

        return true;
    }

    public void ExecuteOption()
    {
        for (int i = 0; i < modules.Count; i++)
        {
            modules[i]?.Execute(seed);
        }
    }
}