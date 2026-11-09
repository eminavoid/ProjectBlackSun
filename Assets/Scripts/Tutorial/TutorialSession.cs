/// <summary>
/// Estado del onboarding. Sobrevive al cambio de escena, igual que la dificultad.
/// </summary>
public static class TutorialSession
{
    public static bool Guide { get; private set; }
    public static bool Skipped { get; private set; }
    public static bool Finished { get; private set; }
    public static bool CultChosen { get; private set; }
    public static string CultName { get; private set; }
    public static PlayerStats.PlayerStat StartingStat { get; private set; } = PlayerStats.PlayerStat.None;

    public static Unlockeable FixedUnlock { get; private set; }
    public static Doctrine PendingDoctrine { get; private set; }

    public static bool SuppressAmbientEvents => Guide && !Finished && !Skipped;

    public static bool UseScriptedPlans =>
        Guide && !Finished && !Skipped && GameTime.CurrentTurn <= 3;

    public static void BeginGuide()
    {
        Guide = true;
        Skipped = false;
        Finished = false;
        ClearCult();
    }

    public static void BeginFree()
    {
        Guide = false;
        Skipped = false;
        Finished = true;
        ClearCult();
    }

    public static void Skip()
    {
        Skipped = true;
        Finished = true;
        Guide = false;
        FixedUnlock = null;
    }

    public static void Complete()
    {
        Finished = true;
        Guide = false;
        FixedUnlock = null;
    }

    public static void SetCult(string name, PlayerStats.PlayerStat stat)
    {
        CultChosen = true;
        CultName = name;
        StartingStat = stat;
        FactionIdUtil.SetPlayerName(name);
    }

    public static void ArmFixedUnlock(Unlockeable unlock, Doctrine doctrine)
    {
        FixedUnlock = unlock;
        PendingDoctrine = doctrine;
    }

    public static bool ClaimFixedUnlock(Unlockeable unlock)
    {
        if (unlock == null || unlock != FixedUnlock) return false;
        FixedUnlock = null;
        return true;
    }

    public static void ClearPendingDoctrine()
    {
        PendingDoctrine = null;
    }

    private static void ClearCult()
    {
        CultChosen = false;
        CultName = null;
        StartingStat = PlayerStats.PlayerStat.None;
        FixedUnlock = null;
        PendingDoctrine = null;
        FactionIdUtil.SetPlayerName(null);
    }
}
