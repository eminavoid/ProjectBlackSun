using System;
using System.Collections.Generic;

[Serializable]
public abstract class OptionModule
{
    public abstract bool CanExecute();

    public abstract void Execute(Option option, Seed seed);

    public virtual void GetFixedResourceChanges(List<ResourceDelta> changes) { }

    public virtual bool IsRefund => false;
}