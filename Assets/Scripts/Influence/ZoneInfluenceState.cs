using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ZoneInfluenceState
{
    public const int DefaultCap = 100;

    [SerializeField] private int cap = DefaultCap;

    private readonly Dictionary<FactionId, int> shares = new Dictionary<FactionId, int>();
    private readonly Dictionary<FactionId, int> clerics = new Dictionary<FactionId, int>();
    private readonly HashSet<FactionId> expelledWhileAtCap = new HashSet<FactionId>();

    public int Cap
    {
        get => cap;
        set => cap = Mathf.Max(1, value);
    }

    public ZoneControlStatus Status { get; private set; } = ZoneControlStatus.Contested;
    public FactionId? Controller { get; private set; }

    public int TotalInfluence
    {
        get
        {
            int total = 0;
            foreach (KeyValuePair<FactionId, int> pair in shares)
            {
                total += Mathf.Max(0, pair.Value);
            }

            return total;
        }
    }

    public bool IsAtCap => TotalInfluence >= Cap;

    public int GetShare(FactionId faction)
    {
        return shares.TryGetValue(faction, out int value) ? Mathf.Max(0, value) : 0;
    }

    public int GetClerics(FactionId faction)
    {
        return clerics.TryGetValue(faction, out int value) ? Mathf.Max(0, value) : 0;
    }

    public float GetSharePercent(FactionId faction)
    {
        int total = TotalInfluence;
        if (total <= 0) return 0f;
        return GetShare(faction) * 100f / total;
    }

    public bool HasAbsoluteControl(FactionId faction)
    {
        int total = TotalInfluence;
        if (total <= 0) return false;
        return GetShare(faction) == total;
    }

    public bool IsExpelledWhileAtCap(FactionId faction) => expelledWhileAtCap.Contains(faction);

    public IEnumerable<FactionId> FactionsWithShare()
    {
        foreach (KeyValuePair<FactionId, int> pair in shares)
        {
            if (pair.Value > 0) yield return pair.Key;
        }
    }

    public IEnumerable<FactionId> FactionsWithClerics()
    {
        foreach (KeyValuePair<FactionId, int> pair in clerics)
        {
            if (pair.Value > 0) yield return pair.Key;
        }
    }

    public int CountFactionsWithShare()
    {
        int count = 0;
        foreach (KeyValuePair<FactionId, int> pair in shares)
        {
            if (pair.Value > 0) count++;
        }

        return count;
    }

    public void RecalculateControl()
    {
        Controller = null;
        Status = ZoneControlStatus.Contested;

        int total = TotalInfluence;
        if (total <= 0) return;

        FactionId? best = null;
        int bestShare = 0;
        bool tie = false;

        foreach (KeyValuePair<FactionId, int> pair in shares)
        {
            if (pair.Value <= 0) continue;
            if (pair.Value > bestShare)
            {
                bestShare = pair.Value;
                best = pair.Key;
                tie = false;
            }
            else if (pair.Value == bestShare)
            {
                tie = true;
            }
        }

        if (!best.HasValue || tie) return;

        // Controlled when one sect has more than 50% of zone influence.
        if (bestShare * 2 > total)
        {
            Status = ZoneControlStatus.Controlled;
            Controller = best;
        }
    }

    public bool CanEnterByNormalMeans(FactionId faction)
    {
        if (GetShare(faction) > 0 || GetClerics(faction) > 0) return true;
        if (!IsAtCap) return true;
        if (expelledWhileAtCap.Contains(faction)) return false;
        // Cap full and no presence: cannot enter by normal means.
        return false;
    }

    public void SetClerics(FactionId faction, int amount)
    {
        amount = Mathf.Max(0, amount);
        if (amount == 0) clerics.Remove(faction);
        else clerics[faction] = amount;
    }

    public void AddClerics(FactionId faction, int delta)
    {
        SetClerics(faction, GetClerics(faction) + delta);
    }

    public int SetShare(FactionId faction, int amount)
    {
        amount = Mathf.Max(0, amount);
        if (amount == 0) shares.Remove(faction);
        else shares[faction] = amount;
        ClearExpelFlagIfPresent(faction);
        return GetShare(faction);
    }

    public int AddShare(FactionId faction, int delta)
    {
        if (delta == 0) return GetShare(faction);
        return SetShare(faction, GetShare(faction) + delta);
    }

    /// <summary>Adds influence up to remaining room. Returns amount actually added.</summary>
    public int TryAddShareClamped(FactionId faction, int amount)
    {
        if (amount <= 0) return 0;
        int room = Cap - TotalInfluence;
        if (room <= 0) return 0;
        int added = Mathf.Min(amount, room);
        AddShare(faction, added);
        return added;
    }

    public void ApplyDirectInfluenceDelta(FactionId faction, int delta)
    {
        if (delta > 0)
        {
            TryAddShareClamped(faction, delta);
        }
        else if (delta < 0)
        {
            AddShare(faction, delta);
            if (GetShare(faction) <= 0 && GetClerics(faction) <= 0)
            {
                Expel(faction, returnClerics: false);
            }
        }

        RecalculateControl();
    }

    public int Expel(FactionId faction, bool returnClerics)
    {
        int removedClerics = GetClerics(faction);
        bool wasAtCap = IsAtCap;
        shares.Remove(faction);
        clerics.Remove(faction);
        if (wasAtCap)
        {
            expelledWhileAtCap.Add(faction);
        }

        RecalculateControl();
        return returnClerics ? removedClerics : 0;
    }

    public void ClearExpelFlagsIfNotAtCap()
    {
        if (!IsAtCap) expelledWhileAtCap.Clear();
    }

    private void ClearExpelFlagIfPresent(FactionId faction)
    {
        if (GetShare(faction) > 0 || GetClerics(faction) > 0)
        {
            expelledWhileAtCap.Remove(faction);
        }
    }

    public string FormatDebugLine()
    {
        string control = Status == ZoneControlStatus.Controlled && Controller.HasValue
            ? $"Controlled:{FactionIdUtil.ShortLabel(Controller.Value)}"
            : "Contested";

        return $"{control} total={TotalInfluence}/{Cap}";
    }
}
