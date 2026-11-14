using System;
using System.Collections.Generic;
using UnityEngine;

public class UnlockerManager : Singleton<UnlockerManager>
{
    [SerializeField] private List<Unlocked> unlocks;

    [Space]

    [SerializeField] private List<Unlockeable> startUnlocks;

    private Dictionary<UnlockCategory, Dictionary<Rarity, List<Unlockeable>>> locked = new Dictionary<UnlockCategory, Dictionary<Rarity, List<Unlockeable>>>();

    //replace with TryGetItem()
    public static List<Unlockeable> GetList(UnlockCategory category, Rarity rarity)
    {
        return Instance.locked[category][rarity];
    }

    protected override void OnInitialization()
    {
        foreach (UnlockCategory category in Enum.GetValues(typeof(UnlockCategory)))
        {
            locked.Add(category, new Dictionary<Rarity, List<Unlockeable>>());

            foreach (Rarity rarity in Enum.GetValues(typeof(Rarity)))
            {
                locked[category].Add(rarity, new List<Unlockeable>());
            }
        }

        for (int i = 0; i < unlocks.Count; i++)
        {
            unlocks[i].Clear();
        }

        for (int i = 0; i < startUnlocks.Count; i++)
        {
            locked[UnlockCategory.Seed][Rarity.Common].Add(startUnlocks[i]);
            startUnlocks[i].Unlock();
        }
    }
}

public enum UnlockCategory
{
    Seed,
    Doctrine
}