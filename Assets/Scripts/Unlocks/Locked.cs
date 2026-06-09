using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Locked", menuName = "Unlocks/new Locked List", order = 1)]
public class Locked : ScriptableObject
{
    [SerializeField] private List<Unlockeable> locked;

    [SerializeField] private readonly Dictionary<Rarity, List<Unlockeable>> filtered = new Dictionary<Rarity, List<Unlockeable>>();

    public List<Unlockeable> GetCopy() => new List<Unlockeable>(locked);

    public List<Unlockeable> GetRarity(Rarity rarity) => filtered[rarity];

    public void Add(Unlockeable scriptable, Rarity rarity)
    {
        locked.Add(scriptable);

        if (filtered.TryGetValue(rarity, out List<Unlockeable> filteredList))
        {
            filteredList.Add(scriptable);
        }
        else
        {
            filtered.Add(rarity, new List<Unlockeable>() { scriptable });
        }
    }

    public void Clear()
    {
        locked.Clear();
    }

    private void OnValidate()
    {
        foreach (Unlockeable unlockeable in locked)
        {
            if (unlockeable == null) continue;

            if (filtered.TryGetValue(unlockeable.Rarity, out List<Unlockeable> filteredList))
            {
                filteredList.Add(unlockeable);
            }
            else
            {
                filtered.Add(unlockeable.Rarity, new List<Unlockeable>() { unlockeable });
            }
        }
    }
}