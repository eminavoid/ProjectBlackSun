using System.Collections.Generic;

public class SettledEvent
{
    public Seed Seed { get; }
    public Option Option { get; }
    public string Outcome { get; }
    public IReadOnlyList<ResourceDelta> Changes { get; }

    public SettledEvent(Seed seed, Option option, string outcome, IReadOnlyList<ResourceDelta> changes)
    {
        Seed = seed;
        Option = option;
        Outcome = outcome;
        Changes = changes;
    }
}
