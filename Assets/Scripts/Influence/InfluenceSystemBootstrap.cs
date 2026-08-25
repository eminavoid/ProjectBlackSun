using UnityEngine;

/// <summary>
/// Asegura InfluenceManager + AI + panel debug en escena (VS bootstrap).
/// </summary>
[DefaultExecutionOrder(-30)]
public class InfluenceSystemBootstrap : MonoBehaviour
{
    [SerializeField] private DistrictProductionConfig productionConfig;
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private bool ensureAiController = true;
    [SerializeField] private bool ensureDebugPanel = true;
    [SerializeField] private bool ensureInfluenceOverlay = true;
    [SerializeField] private bool ensureIntentOverlay = true;

    public static void EnsureInScene()
    {
        if (FindAnyObjectByType<InfluenceSystemBootstrap>() != null)
        {
            return;
        }

        InfluenceManager manager = InfluenceManager.Get;
        GameObject host = manager.gameObject;
        host.AddComponent<InfluenceSystemBootstrap>();
    }

    private void Awake()
    {
        InfluenceManager manager = InfluenceManager.Get;
        manager.Configure(productionConfig, playerStats);

        if (playerStats == null)
        {
            DoctrinesController doctrines = FindAnyObjectByType<DoctrinesController>();
            if (doctrines != null && doctrines.Stats != null)
            {
                manager.Configure(productionConfig, doctrines.Stats);
            }
        }

        if (ensureAiController && FindAnyObjectByType<AIInfluenceController>() == null)
        {
            gameObject.AddComponent<AIInfluenceController>();
        }

        if (ensureDebugPanel && FindAnyObjectByType<InfluenceDebugPanel>() == null)
        {
            gameObject.AddComponent<InfluenceDebugPanel>();
        }

        if (FindAnyObjectByType<DistrictClericAssignPanel>() == null)
        {
            gameObject.AddComponent<DistrictClericAssignPanel>();
        }

        if (ensureInfluenceOverlay && FindAnyObjectByType<InfluenceOverlayRenderer>() == null)
        {
            gameObject.AddComponent<InfluenceOverlayRenderer>();
        }

        if (ensureIntentOverlay)
        {
            if (FindAnyObjectByType<AIIntentBoard>() == null) gameObject.AddComponent<AIIntentBoard>();
            if (FindAnyObjectByType<AIIntentOverlay>() == null) gameObject.AddComponent<AIIntentOverlay>();
        }

        manager.RefreshZoneCache();
        manager.EnsureZonesInitialized();
        manager.RebuildAdjacency();
    }
}
