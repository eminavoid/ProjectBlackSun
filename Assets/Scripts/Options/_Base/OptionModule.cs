using System;
using System.Collections.Generic;

[Serializable]
public abstract class OptionModule
{
    public abstract bool CanExecute();

    public abstract void Execute(Option option, Seed seed);

    /// <summary>Resources this module always spends or gives, known before the option is picked.</summary>
    public virtual void GetFixedResourceChanges(List<ResourceDelta> changes) { }

    /// <summary>What it gives back is part of the option's cost, not a gain (marked "!!!" in the events Excel).</summary>
    public virtual bool IsRefund => false;
}