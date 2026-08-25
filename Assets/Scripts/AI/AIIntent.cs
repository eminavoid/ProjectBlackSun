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

    /// <summary>Secta que ejecuta (clérigos o seeds). El color sale de acá.</summary>
    public FactionId? Faction;

    /// <summary>Base de poder desde donde se mueven los clérigos; puede ser null.</summary>
    public DistrictZone Origin;

    public DistrictZone Target;
    public int Amount;
    public Seed Seed;
    public string Label;

    public Color Color => FactionPalette.Glow(Faction);
    public Color LabelColor => FactionPalette.For(Faction);

    public bool IsValid => Target != null && Target.IsPlayable;
}
