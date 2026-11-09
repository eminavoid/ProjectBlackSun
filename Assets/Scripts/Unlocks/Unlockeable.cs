using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Unlockeable", menuName = "Unlocks/new Unlockeable", order = 1)]
public class Unlockeable : ScriptableObject
{
    [SerializeField] private ScriptableObject scriptable;
    [SerializeField] private Unlocked target;
    [SerializeField] private List<ScriptableObject> requeriments;
    [SerializeField] private Rarity rarity;

    public ScriptableObject Scriptable => scriptable;
    public Rarity Rarity => rarity;

    public static Unlockeable CreateRuntime(string unlockName, ScriptableObject payload)
    {
        Unlockeable unlock = CreateInstance<Unlockeable>();
        unlock.name = unlockName;
        unlock.scriptable = payload;
        return unlock;
    }

    public void Unlock()
    {
        if (target == null || scriptable == null) return;

        if (!target.Contains(scriptable))
        {
            target.Add(scriptable, rarity);
        }
    }

    public bool HasRequeriments(Unlocked unlocked)
    {
        for (int i = 0; i < requeriments.Count; i++)
        {
            if (!unlocked.Contains(requeriments[i]))
            {
                return false;
            }
        }

        return true;
    }
}