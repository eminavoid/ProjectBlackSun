using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Unlockeable", menuName = "Unlocks/new Unlockeable", order = 1)]
public class Unlockeable : ScriptableObject
{
    [SerializeField] private ScriptableObject scriptable;
    [SerializeField] private Unlocked target;
    [SerializeField] private List<ScriptableObject> requeriments;

    public ScriptableObject Scriptable => scriptable;

    public void Unlock()
    {
        if (!target.Contains(this))
        {
            target.Add(this);
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