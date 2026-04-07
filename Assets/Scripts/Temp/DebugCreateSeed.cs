using UnityEngine;

public class DebugCreateSeed : MonoBehaviour
{
    [SerializeField] private Node node;
    [SerializeField] private Seed seed;

    private void Start()
    {
        node.AddSeed(seed);
    }
}