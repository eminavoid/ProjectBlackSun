using System.Collections.Generic;

public readonly struct ResourceDelta
{
    public readonly Resource Resource;
    public readonly int Amount;
    public readonly bool IsRefund;

    public ResourceDelta(Resource resource, int amount, bool isRefund = false)
    {
        Resource = resource;
        Amount = amount;
        IsRefund = isRefund;
    }

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
