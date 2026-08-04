using System;
using UnityEngine;

[Serializable]
public abstract class OptionModule
{
    [Header("Visual")]
    [SerializeField] private bool displayDescription = true;

    public bool DisplayDescription => displayDescription;

    public abstract string GetDescription();

    public abstract bool CanExecute();

    public abstract void Execute(Option option, Seed seed);
}