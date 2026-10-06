public enum Region
{
    None,
    Agora,
    Ocularium,
    Crucible,
    PinkQuarter,
    Warrens,
    ImperialDistrict
}

public static class RegionExtensions
{
    public static bool TryGetResource(this Region region, out Resource resource)
    {
        switch (region)
        {
            case Region.Agora: resource = Resource.Wealth; return true;
            case Region.Ocularium: resource = Resource.Zeal; return true;
            case Region.Crucible: resource = Resource.Materials; return true;
            case Region.PinkQuarter: resource = Resource.Secrets; return true;
            case Region.Warrens: resource = Resource.Flock; return true;
            case Region.ImperialDistrict: resource = Resource.Authority; return true;
            default: resource = default; return false;
        }
    }
}
