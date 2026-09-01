using UnityEngine;

public abstract class DoctrineBehaviour : ScriptableObject
{
    public virtual void OnTickStart() { }

    public virtual void OnTickEnd() { }
}