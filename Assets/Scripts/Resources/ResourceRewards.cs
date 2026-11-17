using System.Collections.Generic;

public readonly struct ResourceRewards
{
    public static ResourceRewards None => new ResourceRewards(new List<Resource>());

    public readonly List<Resource> resources;

    public ResourceRewards(List<Resource> resources)
    {
        this.resources = resources;
    }
}