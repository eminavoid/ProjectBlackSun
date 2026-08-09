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
            case FactionId.Player: return "Player";
            case FactionId.Rival1: return "Rival 1";
            case FactionId.Rival2: return "Rival 2";
            case FactionId.Rival3: return "Rival 3";
            case FactionId.Rival4: return "Rival 4";
            default: return id.ToString();
        }
    }
}
