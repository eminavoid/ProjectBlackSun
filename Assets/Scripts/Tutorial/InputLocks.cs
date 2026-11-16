using UnityEngine;

public static class InputLocks
{
    public static bool EndTurn { get; set; }
    public static bool MapSelection { get; set; }
    public static bool MapContext { get; set; }

    public static void Lock(bool endTurn, bool mapSelection, bool mapContext)
    {
        EndTurn = endTurn;
        MapSelection = mapSelection;
        MapContext = mapContext;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Clear()
    {
        EndTurn = false;
        MapSelection = false;
        MapContext = false;
    }
}
