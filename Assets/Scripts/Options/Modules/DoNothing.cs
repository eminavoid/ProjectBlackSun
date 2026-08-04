using System;

[Serializable]
public class DoNothing : OptionModule
{
    public override string GetDescription()
    {
        return $"Nothing";
    }

    public override bool CanExecute() => true;

    public override void Execute(Option option, Seed seed) { }
}