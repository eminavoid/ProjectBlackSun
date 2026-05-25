using System.Collections.Generic;
using UnityEngine;

public class UnlockerManager : Singleton<UnlockerManager>
{
    [SerializeField] private List<Unlocked> unlocks;
    [SerializeField] private Locked locked;

    [Space]

    [SerializeField] private List<Unlockeable> startUnlocks;

    protected override void OnInitialization()
    {
        for (int i = 0; i < unlocks.Count; i++)
        {
            unlocks[i].Clear();
        }

        for (int i = 0; i < startUnlocks.Count; i++)
        {
            startUnlocks[i].Unlock();
        }
    }
}