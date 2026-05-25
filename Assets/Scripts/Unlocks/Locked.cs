using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Locked", menuName = "Unlocks/new Locked List", order = 1)]
public class Locked : ScriptableObject
{
    [SerializeField] private List<Unlockeable> locked;

    public List<Unlockeable> GetCopy() => new List<Unlockeable>(locked);

    public void Add(Unlockeable scriptable)
    {
        locked.Add(scriptable);
    }

    public void Clear()
    {
        locked.Clear();
    }
}