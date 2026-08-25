using UnityEngine;

/// <summary>
/// Contador Faith Eclipse: zonas Controlled por secta al cierre de partida.
/// </summary>
public static class FaithEclipseTracker
{
    public static int GetControlledZoneCount(FactionId faction)
    {
        if (InfluenceManager.IsNull) return 0;
        return InfluenceManager.Get.CountControlledZones(faction);
    }

    public static FactionId? GetLeader(out int leadingZones)
    {
        leadingZones = 0;
        if (InfluenceManager.IsNull) return null;
        return InfluenceManager.Get.GetFaithEclipseLeader(out leadingZones);
    }

    public static string FormatScoreboard()
    {
        if (InfluenceManager.IsNull) return "FE: (no manager)";

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (FactionId faction in FactionIdUtil.All)
        {
            sb.AppendLine($"{FactionIdUtil.DisplayName(faction)}: {GetControlledZoneCount(faction)}");
        }

        FactionId? leader = GetLeader(out int zones);
        sb.Append(leader.HasValue
            ? $"Leader: {FactionIdUtil.DisplayName(leader.Value)} ({zones})"
            : "Leader: (tie / none)");
        return sb.ToString();
    }
}
