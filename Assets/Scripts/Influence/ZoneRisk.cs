using System.Collections.Generic;
using UnityEngine;

public static class ZoneRisk
{
    private static readonly Dictionary<FactionId, int> clerics = new Dictionary<FactionId, int>();
    private static readonly Dictionary<FactionId, int> shares = new Dictionary<FactionId, int>();
    private static readonly Dictionary<FactionId, int> generation = new Dictionary<FactionId, int>();

    public static bool PlayerCouldLose(DistrictZone zone)
    {
        if (zone == null || InfluenceManager.IsNull) return false;
        ZoneInfluenceState state = zone.Influence;
        if (state == null || state.Status != ZoneControlStatus.Controlled || state.Controller != FactionId.Player) return false;

        clerics.Clear();
        shares.Clear();
        generation.Clear();
        foreach (FactionId faction in FactionIdUtil.All)
        {
            clerics[faction] = state.GetClerics(faction);
            shares[faction] = state.GetShare(faction);
        }

        AddPlannedArrivals(zone);
        if (!AnyRivalClerics()) return false;

        InfluenceManager manager = InfluenceManager.Get;
        foreach (FactionId faction in FactionIdUtil.All)
        {
            int count = clerics[faction];
            if (count <= 0)
            {
                generation[faction] = 0;
                continue;
            }

            Vector2Int range = ClericInfluenceTable.GetRange(count);
            int roll = (FactionIdUtil.IsPlayer(faction) ? range.x : range.y) + Outnumber(faction, manager.OutnumberBonusPerCleric);
            float mult = manager.GetInfluenceGenerationMultiplier(faction, zone) * manager.AttackMultiplier(state, faction);
            generation[faction] = Mathf.Max(0, Mathf.RoundToInt(roll * mult));
        }

        int room = state.Cap - Total();
        foreach (FactionId faction in FactionIdUtil.All)
        {
            if (FactionIdUtil.IsPlayer(faction) || room <= 0) continue;
            int added = Mathf.Min(generation[faction], room);
            shares[faction] += added;
            room -= added;
        }

        if (room > 0) shares[FactionId.Player] += Mathf.Min(generation[FactionId.Player], room);
        if (Total() >= state.Cap) Struggle(manager.StruggleSwing);

        return !Rules(FactionId.Player);
    }

    private static void AddPlannedArrivals(DistrictZone zone)
    {
        if (AIIntentBoard.IsNull) return;

        IReadOnlyList<AIIntent> intents = AIIntentBoard.Get.Intents;
        for (int i = 0; i < intents.Count; i++)
        {
            AIIntent intent = intents[i];
            if (intent == null || intent.Kind != AIIntentKind.AssignClerics || intent.Target != zone || !intent.Faction.HasValue) continue;
            clerics[intent.Faction.Value] += Mathf.Max(0, intent.Amount);
        }
    }

    private static bool AnyRivalClerics()
    {
        foreach (KeyValuePair<FactionId, int> pair in clerics)
        {
            if (!FactionIdUtil.IsPlayer(pair.Key) && pair.Value > 0) return true;
        }

        return false;
    }

    private static int Outnumber(FactionId faction, int perCleric)
    {
        int strongest = 0;
        foreach (KeyValuePair<FactionId, int> pair in clerics)
        {
            if (pair.Key != faction) strongest = Mathf.Max(strongest, pair.Value);
        }

        return strongest > 0 ? Mathf.Max(0, clerics[faction] - strongest) * perCleric : 0;
    }

    private static void Struggle(float swing)
    {
        int peak = 0;
        foreach (FactionId faction in FactionIdUtil.All)
        {
            if (shares[faction] > 0 || generation[faction] > 0) peak = Mathf.Max(peak, generation[faction]);
        }

        if (peak <= 0) return;

        var winners = new List<FactionId>();
        int pool = 0;
        foreach (FactionId faction in FactionIdUtil.All)
        {
            if (shares[faction] <= 0 && generation[faction] <= 0) continue;
            if (generation[faction] >= peak)
            {
                winners.Add(faction);
                continue;
            }

            int loss = Mathf.Min(Mathf.RoundToInt((peak - generation[faction]) * swing), shares[faction]);
            shares[faction] -= loss;
            pool += loss;
        }

        for (int i = 0; i < winners.Count && pool > 0; i++)
        {
            shares[winners[i]] += pool / winners.Count + (i < pool % winners.Count ? 1 : 0);
        }
    }

    private static bool Rules(FactionId faction)
    {
        int total = Total();
        int mine = shares[faction];
        if (total <= 0 || mine * 2 <= total) return false;

        foreach (KeyValuePair<FactionId, int> pair in shares)
        {
            if (pair.Key != faction && pair.Value >= mine) return false;
        }

        return true;
    }

    private static int Total()
    {
        int total = 0;
        foreach (int share in shares.Values) total += Mathf.Max(0, share);
        return total;
    }
}
