using UnityEngine;

public class DebugAI : MonoBehaviour
{
    [SerializeField] private SeedsPool seedsPool;

    [Space]

    [SerializeField] private int minTickCooldown = 3;
    [SerializeField] private float spawnTickChanceRatio = 0.25f;

    private bool inCooldown = false;
    private int tickTimer = 0;

    private void Start()
    {
        GameTime.OnTurnStarted += OnTurnStarted;
    }

    private void OnTurnStarted()
    {
        if (inCooldown)
        {
            tickTimer += 1;

            if (tickTimer > minTickCooldown)
            {
                inCooldown = false;
            }

            return;
        }

        bool rollSucess = spawnTickChanceRatio > Random.Range(0f, 1f - Mathf.Epsilon);

        if (rollSucess)
        {
            DistrictData district = DistrictsManager.GetRandomDistrict();

            if (district == null) return;

            Node node = district.GetRandomNode();
            Seed seed = seedsPool.EvilSeeds[Random.Range(0, seedsPool.EvilSeeds.Count)];

            bool planted = node.AddSeed(seed);
            if (!planted) return;

            inCooldown = true;
            tickTimer = 0;

            Debug.Log($"AI planted {seed} at {node} in {node.District}");
        }
    }
}