using System;

[Serializable]
public abstract class OptionModule
{
    public abstract bool CanExecute();

    public abstract void Execute(Seed seed);
}