using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Smoke checks for influence formulas (run from context menu on any active object).
/// </summary>
public class InfluenceSelfTest : MonoBehaviour
{
    [ContextMenu("Run Influence Self Test")]
    public void Run()
    {
        Vector2Int r1 = ClericInfluenceTable.GetRange(1);
        Vector2Int r5 = ClericInfluenceTable.GetRange(5);
        Vector2Int r15 = ClericInfluenceTable.GetRange(15);
        Vector2Int r20 = ClericInfluenceTable.GetRange(20);

        Debug.Assert(r1.x == 1 && r1.y == 5, "clerics 1 range");
        Debug.Assert(r5.x == 5 && r5.y == 15, "clerics 5 range");
        Debug.Assert(r15.x == 15 && r15.y == 15, "clerics 15 range");
        Debug.Assert(r20.x == 15 && r20.y == 15, "clerics 16+ range");

        ZoneInfluenceState state = new ZoneInfluenceState { Cap = 100 };
        state.TryAddShareClamped(FactionId.Player, 51);
        state.TryAddShareClamped(FactionId.Rival1, 49);
        state.RecalculateControl();
        Debug.Assert(state.Status == ZoneControlStatus.Controlled && state.Controller == FactionId.Player, "51% control");

        state = new ZoneInfluenceState { Cap = 100 };
        state.TryAddShareClamped(FactionId.Player, 50);
        state.TryAddShareClamped(FactionId.Rival1, 50);
        state.RecalculateControl();
        Debug.Assert(state.Status == ZoneControlStatus.Contested, "50/50 contested");

        Debug.Log("InfluenceSelfTest: OK", this);
    }
}
