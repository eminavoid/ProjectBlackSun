/// <summary>
/// Sectas del Vertical Slice: jugador + 4 rivales fijos.
/// </summary>
public enum FactionId
{
    Player = 0,
    Rival1 = 1,
    Rival2 = 2,
    Rival3 = 3,
    Rival4 = 4
}

public static class FactionIdUtil
{
    public static readonly FactionId[] All =
    {
        FactionId.Player,
        FactionId.Rival1,
        FactionId.Rival2,
        FactionId.Rival3,
        FactionId.Rival4
    };

    public static bool IsPlayer(FactionId id) => id == FactionId.Player;

    public static string ShortLabel(FactionId id)
    {
        switch (id)
        {
            case FactionId.Player: return "P";
            case FactionId.Rival1: return "R1";
            case FactionId.Rival2: return "R2";
            case FactionId.Rival3: return "R3";
            case FactionId.Rival4: return "R4";
            default: return "?";
        }
    }

    public static string DisplayName(FactionId id)
    {
        switch (id)
        {
            case FactionId.Player: return "Jugador";
            case FactionId.Rival1: return "Crimson Choir";
            case FactionId.Rival2: return "Azure Ledger";
            case FactionId.Rival3: return "Verdant Flock";
            case FactionId.Rival4: return "Violet Veil";
            default: return id.ToString();
        }
    }
}
