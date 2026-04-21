using UnityEngine;
using System;

[Serializable]
public class DoNothing : OptionModule
{
    public override bool CanExecute() => true;

    public override void Execute(Seed seed) { }
}