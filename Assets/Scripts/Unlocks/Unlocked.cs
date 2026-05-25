using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Unlocked", menuName = "Unlocks/new Unlocked List", order = 1)]
public class Unlocked : ScriptableObject
{
    [SerializeField] private List<ScriptableObject> unlocked;

    public List<ScriptableObject> GetCopy() => new List<ScriptableObject>(unlocked);

    public bool Contains(ScriptableObject scriptable)
    {
        return unlocked.Contains(scriptable);
    }

    public void Add(ScriptableObject scriptable)
    {
        unlocked.Add(scriptable);
    }

    public void Clear()
    {
        unlocked.Clear();
    }
}