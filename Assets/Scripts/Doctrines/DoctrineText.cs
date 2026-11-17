using System.Collections.Generic;
using System.Text.RegularExpressions;

public static class DoctrineText
{
    public const string GainHex = "3D6B2A";
    public const string LossHex = "8B1A1A";

    private static readonly Regex StatToken = new Regex(@"[+\-]\s*\d+\s*(Diplomacy|Aggres+ion|Stewardship|Intrigue|Learning)\b", RegexOptions.IgnoreCase);
    private static readonly Regex Spaces = new Regex(@"\s+");

    public static string Effects(Doctrine doctrine, string separator)
    {
        if (doctrine == null || doctrine.StatUpdates == null) return string.Empty;

        var gains = new List<string>();
        var losses = new List<string>();
        foreach (Doctrine.StatUpdate update in doctrine.StatUpdates)
        {
            if (update.playerStat == PlayerStats.PlayerStat.None || update.changeAmount == 0) continue;
            bool gain = update.changeAmount > 0;
            (gain ? gains : losses).Add($"<color=#{(gain ? GainHex : LossHex)}><b>{update.changeAmount:+0;-0}</b> {Virtues.Name(update.playerStat)}</color>");
        }

        gains.AddRange(losses);
        return string.Join(separator, gains);
    }

    public static string Rules(Doctrine doctrine)
    {
        if (doctrine == null || string.IsNullOrWhiteSpace(doctrine.Description)) return string.Empty;
        return Spaces.Replace(StatToken.Replace(doctrine.Description, string.Empty), " ").Trim();
    }
}
