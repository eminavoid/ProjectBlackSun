using UnityEngine;

public abstract class Option : ScriptableObject
{
    protected Seed seed;

    public void Initialize(Seed seed)
    {
        this.seed = seed;
    }

    public abstract void ExecuteOption();
}