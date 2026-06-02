using UnityEngine;

public class DebugCreateSeed : MonoBehaviour
{
    [SerializeField] private DistrictZone sector;
    [SerializeField] private Seed seed;

    private void Start()
    {
        if (sector == null)
        {
            sector = GetComponent<DistrictZone>();
        }

        if (sector != null)
        {
            sector.AddSeed(seed);
        }
    }
}
