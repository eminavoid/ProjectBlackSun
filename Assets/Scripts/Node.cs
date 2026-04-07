using UnityEngine;

public class Node : MonoBehaviour
{
    [field: SerializeField] public Districts District { get; private set; }

    private Seed seed = null;

    public void AddSeed(Seed seed)
    {
        if (this.seed != null) return;
        seed.Initialize(this);
        this.seed = seed;
    }

    public void RemoveSeed(Seed seed)
    {
        if (this.seed != seed) return;
        this.seed = null;
    }

    private void Start()
    {
        GameTime.OnTurnEnded += Tick;
    }

    private void Tick()
    {
        if (seed == null) return;
        seed.Tick();
    }
}