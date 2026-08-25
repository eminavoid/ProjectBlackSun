/// <summary>
/// Stub GDD: Boons / Banes como modificadores condicionales de influencia.
/// </summary>
public interface IZoneModifier
{
    string Id { get; }
    float GetInfluenceGenerationMultiplier(FactionId faction, DistrictZone zone);
}

/// <summary>Placeholder hasta que el lead cierre Boons/Banes.</summary>
public sealed class NullZoneModifier : IZoneModifier
{
    public static readonly NullZoneModifier Instance = new NullZoneModifier();

    public string Id => "None";

    public float GetInfluenceGenerationMultiplier(FactionId faction, DistrictZone zone) => 1f;
}

/// <summary>
/// Marcador de diseño: fanáticos para defender (intro GDD, sin reglas).
/// </summary>
public static class FanaticDefensePending
{
    public const bool Enabled = false;
    public const string Note = "GDD menciona fanáticos para defender; mecánica pendiente de diseño.";
}
