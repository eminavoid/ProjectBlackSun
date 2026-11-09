/// <summary>
/// Dificultad elegida en el menú. Sobrevive al cambio de escena.
/// Sin menú, las tres rivales quedan en Normal.
/// </summary>
public static class MatchSettings
{
    public static readonly FactionId[] Rivals =
    {
        FactionId.Rival1,
        FactionId.Rival2,
        FactionId.Rival3
    };

    private static readonly RivalDifficulty[] perRival =
    {
        RivalDifficulty.Normal,
        RivalDifficulty.Normal,
        RivalDifficulty.Normal
    };

    public static bool UseCustom { get; private set; }

    public static RivalDifficulty Shared { get; private set; } = RivalDifficulty.Normal;

    public static void UseShared(RivalDifficulty difficulty)
    {
        UseCustom = false;
        Shared = difficulty;
    }

    public static void UsePerRival(RivalDifficulty first, RivalDifficulty second, RivalDifficulty third)
    {
        UseCustom = true;
        perRival[0] = first;
        perRival[1] = second;
        perRival[2] = third;
    }

    public static RivalDifficulty DifficultyFor(FactionId faction)
    {
        if (!UseCustom) return Shared;

        for (int i = 0; i < Rivals.Length; i++)
        {
            if (Rivals[i] == faction) return perRival[i];
        }

        return Shared;
    }
}
