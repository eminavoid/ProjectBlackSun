using UnityEngine;

public enum AIIntentKind
{
    AssignClerics,
    PlantSeed
}

/// <summary>
/// Jugada que una IA ya decidió pero todavía no ejecutó. Se telegrafía al jugador durante su turno.
/// </summary>
public class AIIntent
{
    public AIIntentKind Kind;

    /// <summary>Secta que ejecuta. Null para el antagonista global que planta seeds.</summary>
    public FactionId? Faction;

    /// <summary>Base de poder desde donde se mueven los clérigos; puede ser null.</summary>
    public DistrictZone Origin;

    public DistrictZone Target;
    public int Amount;
    public Seed Seed;
    public string Label;

    public Color Color => Faction.HasValue ? FactionPalette.Glow(Faction.Value) : FactionPalette.Corruption;
    public Color LabelColor => Faction.HasValue ? FactionPalette.For(Faction.Value) : FactionPalette.CorruptionLabel;

    public bool IsValid => Target != null && Target.IsPlayable;
}
