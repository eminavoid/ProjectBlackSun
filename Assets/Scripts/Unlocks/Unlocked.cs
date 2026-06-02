using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Unlocked", menuName = "Unlocks/new Unlocked List", order = 1)]
public class Unlocked : ScriptableObject
{
    [SerializeField] private List<ScriptableObject> unlocked;

    private readonly Dictionary<Rarity, List<ScriptableObject>> filtered = new Dictionary<Rarity, List<ScriptableObject>>();

    public List<ScriptableObject> GetCopy() => new List<ScriptableObject>(unlocked);
    public List<ScriptableObject> GetRarity(Rarity rarity) => filtered[rarity];

    public bool Contains(ScriptableObject scriptable)
    {
        return unlocked.Contains(scriptable);
    }

    public void Add(ScriptableObject scriptable, Rarity rarity)
    {
        unlocked.Add(scriptable);
        
        if (filtered.TryGetValue(rarity, out List<ScriptableObject> filteredList))
        {
            filteredList.Add(scriptable);
        }
        else
        {
            filtered.Add(rarity, new List<ScriptableObject>() { scriptable });
        }
    }

    public void Clear()
    {
        unlocked.Clear();
    }
}