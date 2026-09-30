using System.Collections.Generic;

/// <summary>A signed amount of one resource: what an option costs or what an outcome gave.</summary>
public readonly struct ResourceDelta
{
    public readonly Resource Resource;
    public readonly int Amount;
    /// <summary>Gives back (part of) what the option cost, rather than being a new gain.</summary>
    public readonly bool IsRefund;

    public ResourceDelta(Resource resource, int amount, bool isRefund = false)
    {
        Resource = resource;
        Amount = amount;
        IsRefund = isRefund;
    }

    /// <summary>Adds the amount to that resource's entry, so each resource shows up once (refunds apart).</summary>
    public static void Add(List<ResourceDelta> deltas, Resource resource, int amount, bool isRefund = false)
    {
        for (int i = 0; i < deltas.Count; i++)
        {
            if (deltas[i].Resource != resource || deltas[i].IsRefund != isRefund) continue;

            deltas[i] = new ResourceDelta(resource, deltas[i].Amount + amount, isRefund);
            return;
        }

        deltas.Add(new ResourceDelta(resource, amount, isRefund));
    }
}
