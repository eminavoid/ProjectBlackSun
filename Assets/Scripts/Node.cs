using UnityEngine;

public class Node : MonoBehaviour
{
    [field: SerializeField] public Districts District { get; private set; }

    private Seed seed = null;

    public bool AddSeed(Seed seed)
    {
        if (this.seed != null) return false;
        if (seed == null) return false;

        Seed seedInstance = Instantiate(seed);
        seedInstance.Initialize(this);
        this.seed = seedInstance;
        return true;
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