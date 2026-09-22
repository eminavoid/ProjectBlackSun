using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zeke.UI;
using TMPro;

public class SeedEventManager : Singleton<SeedEventManager>
{
    [System.Serializable]
    private class ResourceSeedPool
    {
        public Resource Resource;
        public SeedsPool SeedPool;
        [Range(0, 100)] public int SeedChance = 100;
        public int TriggerBelowAmount = 0;
    }

    [SerializeField] private PlayerResources resources;

    [SerializeField] private UIWindow spawnOptionsWindow;
    [SerializeField] private UIWindow eventOutputWindowPrefab;
    [SerializeField] private OptionDisplay optionDisplayPrefab;

    [Space]

    [SerializeField] private List<ResourceSeedPool> resourceSeedPools = new List<ResourceSeedPool>();
    [SerializeField, HideInInspector] private SeedsPool wealthSeedPool;
    [SerializeField, HideInInspector] private int wealthSeedChance = 100;

    private readonly Queue<Seed> seedEvents = new Queue<Seed>();

    public static void EnqueueSeedEvent(Seed seed)
    {
        Instance.seedEvents.Enqueue(seed);
    }

    private void Start()
    {
        AddLegacyWealthSeedPool();
        GameTime.OnTurnStarted += OnTurnStarted;
        SetOptionsWindowVisibility(false);
    }

    public void OnOptionSelected(Option option)
    {
        LayoutGroup layout = spawnOptionsWindow.TryGetElement<LayoutGroup>("Layout Group");

        foreach (Transform children in layout.transform)
        {
            Destroy(children.gameObject);
        }

        if (seedEvents.Count > 0)
        {
            StartChoosingOptionsPhase();
        }
        else
        {
            SetOptionsWindowVisibility(false);
        }
    }

    public static void CreateEventOutputWindow(string description)
    {
        UIWindow windowInstance = Instantiate(Instance.eventOutputWindowPrefab, GlobalReferences.ScreenCanvas.transform);
        windowInstance.TryGetElement<TextMeshProUGUI>("Description").text = description;
    }

    private void OnTurnStarted()
    {
        StartChoosingOptionsPhase();

        TryPlantResourceSeeds();
    }

    private void TryPlantResourceSeeds()
    {
        if (resourceSeedPools == null)
        {
            return;
        }

        for (int i = 0; i < resourceSeedPools.Count; i++)
        {
            ResourceSeedPool resourceSeedPool = resourceSeedPools[i];

            if (resourceSeedPool == null || resourceSeedPool.SeedPool == null || resourceSeedPool.SeedPool.EvilSeeds == null || resourceSeedPool.SeedPool.EvilSeeds.Count <= 0)
            {
                continue;
            }

            if (resources.GetResourceAmount(resourceSeedPool.Resource) >= resourceSeedPool.TriggerBelowAmount)
            {
                continue;
            }

            int seedChance = Mathf.Clamp(resourceSeedPool.SeedChance, 0, 100);

            if (Random.Range(0, 100) >= seedChance)
            {
                continue;
            }

            Seed seed = resourceSeedPool.SeedPool.EvilSeeds[Random.Range(0, resourceSeedPool.SeedPool.EvilSeeds.Count)];

            if (TryPlantSeed(seed))
            {
                Debug.Log($"Played seed due to low {resourceSeedPool.Resource}");
            }
        }
    }

    private bool TryPlantSeed(Seed seed)
    {
        if (seed == null) return false;

        if (!DistrictsManager.TryGetRandomFreeZoneAnyDistrict(out DistrictZone zone) || zone == null)
        {
            Debug.LogWarning("No hay sectores libres para plantar resource seed.", this);
            return false;
        }

        //TODO: que se fije las allowed seeds
        if (!seed.CanPlantInDistrict(zone.District)) return false;

        if (!zone.AddSeed(seed)) return false;

        return true;
    }

    private void AddLegacyWealthSeedPool()
    {
        if (resourceSeedPools == null)
        {
            resourceSeedPools = new List<ResourceSeedPool>();
        }

        if (wealthSeedPool == null || HasResourceSeedPool(Resource.Wealth))
        {
            return;
        }

        resourceSeedPools.Add(new ResourceSeedPool
        {
            Resource = Resource.Wealth,
            SeedPool = wealthSeedPool,
            SeedChance = wealthSeedChance,
            TriggerBelowAmount = 0
        });
    }

    private bool HasResourceSeedPool(Resource resource)
    {
        if (resourceSeedPools == null)
        {
            return false;
        }

        for (int i = 0; i < resourceSeedPools.Count; i++)
        {
            if (resourceSeedPools[i] != null && resourceSeedPools[i].Resource == resource)
            {
                return true;
            }
        }

        return false;
    }

    private void StartChoosingOptionsPhase()
    {
        if (seedEvents.Count <= 0)
        {
            return;
        }

        SetOptionsWindowVisibility(true);
        CreateSeedOptionsInCanvas(seedEvents.Dequeue());

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayEventPopup();
        }
    }

    private void CreateSeedOptionsInCanvas(Seed seed)
    {
        LayoutGroup layout = spawnOptionsWindow.TryGetElement<LayoutGroup>("Layout Group");
        spawnOptionsWindow.TryGetElement<TextMeshProUGUI>("Title").text = seed.Title;
        spawnOptionsWindow.TryGetElement<TextMeshProUGUI>("Description").text = seed.Description;

        for (int i = 0; i < seed.Options.Count; i++)
        {
            OptionDisplay display = Instantiate(optionDisplayPrefab, layout.transform);

            if (display.TryGetComponent(out UIWindow uiWIndow))
            {
                uiWIndow.TryGetElement<TextMeshProUGUI>("Title").text = seed.Options[i].Title;
                uiWIndow.TryGetElement<TextMeshProUGUI>("Description").text = seed.Options[i].Description;
            }

            display.InitializeData(seed.Options[i]);
            display.onOptionSelected += OnOptionSelected;
        }
    }

    private void SetOptionsWindowVisibility(bool condition)
    {
        spawnOptionsWindow.TryGetElement<RectTransform>("Title Rect").gameObject.SetActive(condition);
        spawnOptionsWindow.TryGetElement<RectTransform>("Description Rect").gameObject.SetActive(condition);
        spawnOptionsWindow.TryGetElement<Image>("Background").gameObject.SetActive(condition);
    }
}
