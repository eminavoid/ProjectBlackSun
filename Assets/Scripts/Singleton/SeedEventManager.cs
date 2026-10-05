using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zeke.UI;
using TMPro;
using System;

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

    private readonly List<Seed> seedEvents = new List<Seed>();

    public static Action<Seed> onSeedStored;
    public static Action<Seed> onSeedRemoved;

    public static void CreateSeedEventMenu(Seed seed)
    {
        Instance.spawnOptionsWindow.gameObject.SetActive(true);
        Instance.SetOptionsWindowVisibility(true);
        Instance.CreateSeedOptionsInCanvas(seed);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayEventPopup();
        }
    }

    public static void StoreSeedEvent(Seed seed)
    {
        Instance.seedEvents.Add(seed);
        onSeedStored?.Invoke(seed);
    }

    private static Seed RemoveSeed(Seed seed)
    {
        Instance.seedEvents.Remove(seed);
        onSeedRemoved?.Invoke(seed);
        return seed;
    }

    private void Start()
    {
        AddLegacyWealthSeedPool();
        GameTime.OnTurnStarted += OnTurnStarted;
        GameTime.OnTurnEndedLate += OnTurnEndedLate;
        SetOptionsWindowVisibility(false);
    }

    public void OnOptionSelected(Option option)
    {
        UnloadOptionsWindow();
        RemoveSeed(option.Seed);

        //SetOptionsWindowVisibility(false);
    }

    public static void CreateEventOutputWindow(string title, string description, IReadOnlyList<ResourceDelta> resourceChanges = null)
    {
        UIWindow windowInstance = Instantiate(Instance.eventOutputWindowPrefab, GlobalReferences.ScreenCanvas.transform);
        windowInstance.TryGetElement<TextMeshProUGUI>("Title").text = title;
        windowInstance.TryGetElement<TextMeshProUGUI>("Description").text = description;

        ResourceAmountRow resourceRow = windowInstance.GetComponentInChildren<ResourceAmountRow>(true);
        if (resourceRow != null) resourceRow.Show(resourceChanges);

        Instance.StartCoroutine(Instance.ShowOutputWindowAfterBlur(windowInstance));
    }

    public void UnloadOptionsWindow()
    {
        LayoutGroup layout = spawnOptionsWindow.TryGetElement<LayoutGroup>("Layout Group");

        foreach (Transform children in layout.transform)
        {
            Destroy(children.gameObject);
        }

        if (openOutputWindows > 0)
        {
            ApplyOptionsWindowVisibility(false);
            return;
        }
    }

    private void OnTurnStarted()
    {
        Debug.Log($"Turn started: {seedEvents.Count}");

        TryPlantResourceSeeds();
    }

    private void OnTurnEndedLate()
    {
        Debug.Log($"Turn ended: {seedEvents.Count}");

        for (int i = 0; i < seedEvents.Count; i++)
        {
            Seed seed = seedEvents[i];
            seed.Options[seed.DefaultOption - 1].ExecuteOption();
        }
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

            if (UnityEngine.Random.Range(0, 100) >= seedChance)
            {
                continue;
            }

            Seed seed = resourceSeedPool.SeedPool.EvilSeeds[UnityEngine.Random.Range(0, resourceSeedPool.SeedPool.EvilSeeds.Count)];

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

    private void CreateSeedOptionsInCanvas(Seed seed)
    {
        LayoutGroup layout = spawnOptionsWindow.TryGetElement<LayoutGroup>("Layout Group");
        spawnOptionsWindow.TryGetElement<TextMeshProUGUI>("Title").text = seed.Title;
        spawnOptionsWindow.TryGetElement<TextMeshProUGUI>("Description").text = seed.Description;

        for (int i = 0; i < seed.Options.Count; i++)
        {
            OptionDisplay display = Instantiate(optionDisplayPrefab, layout.transform);

            if (display.TryGetComponent(out UIWindow uiWindow))
            {
                uiWindow.TryGetElement<TextMeshProUGUI>("Title").text = seed.Options[i].Title;
                uiWindow.TryGetElement<TextMeshProUGUI>("Description").text = seed.Options[i].Description;
            }

            display.InitializeData(seed.Options[i]);
            display.onOptionSelected += OnOptionSelected;
        }
    }

    private Coroutine optionsVisibilityRoutine;
    private Coroutine optionsFadeRoutine;
    private CanvasGroup[] optionsContent;
    private int resourceWindowOriginalIndex = -1;
    private int openOutputWindows;

    private void SetOptionsWindowVisibility(bool condition)
    {
        if (optionsVisibilityRoutine != null) StopCoroutine(optionsVisibilityRoutine);

        RawImage blur = spawnOptionsWindow.TryGetElement<RawImage>("Blur");
        if (condition && blur != null)
        {
            optionsVisibilityRoutine = StartCoroutine(ShowOptionsWindowAfterBlur(blur));
            return;
        }

        if (blur != null) blur.gameObject.SetActive(false);
        RestoreResourceWindowOrder();
        ApplyOptionsWindowVisibility(condition);
    }

    // The blur snapshots the screen at end of frame; the window content must stay hidden until then.
    private IEnumerator ShowOptionsWindowAfterBlur(RawImage blur)
    {
        blur.gameObject.SetActive(true);
        UIBackgroundBlur blurComponent = blur.GetComponent<UIBackgroundBlur>();
        for (int i = 0; i < 3 && blurComponent != null && !blurComponent.IsReady; i++) yield return null;

        KeepResourceWindowSharp(spawnOptionsWindow.transform);
        ApplyOptionsWindowVisibility(true);

        if (optionsFadeRoutine != null) StopCoroutine(optionsFadeRoutine);
        optionsFadeRoutine = StartCoroutine(FadeIn(blurComponent != null ? blurComponent.FadeDuration : 0f, OptionsContent()));
        optionsVisibilityRoutine = null;
    }

    // Same rule for the result window: it stays invisible until its blur has the snapshot.
    private IEnumerator ShowOutputWindowAfterBlur(UIWindow window)
    {
        openOutputWindows++;

        if (!window.TryGetComponent(out CanvasGroup group)) group = window.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        UIBackgroundBlur blur = window.GetComponentInChildren<UIBackgroundBlur>();
        while (blur != null && !blur.IsReady) yield return null;

        if (window != null)
        {
            KeepResourceWindowSharp(window.transform);
            StartCoroutine(FadeIn(blur != null ? blur.FadeDuration : 0f, group));
        }

        while (window != null) yield return null;

        openOutputWindows--;
        if (openOutputWindows > 0) yield break;

        SetOptionsWindowVisibility(false);
    }

    // The resources bar sits at the back of the canvas; draw it right above the blurred window so it stays sharp.
    private void KeepResourceWindowSharp(Transform window)
    {
        Transform resourcesWindow = ResourceManager.ResourceWindowTransform;
        if (resourcesWindow == null) return;

        if (resourceWindowOriginalIndex < 0) resourceWindowOriginalIndex = resourcesWindow.GetSiblingIndex();

        // SetSiblingIndex shifts everything in between, so the target depends on which side the bar comes from.
        int windowIndex = window.GetSiblingIndex();
        resourcesWindow.SetSiblingIndex(resourcesWindow.GetSiblingIndex() < windowIndex ? windowIndex : windowIndex + 1);
    }

    private void RestoreResourceWindowOrder()
    {
        Transform resourcesWindow = ResourceManager.ResourceWindowTransform;
        if (resourcesWindow != null && resourceWindowOriginalIndex >= 0) resourcesWindow.SetSiblingIndex(resourceWindowOriginalIndex);
        resourceWindowOriginalIndex = -1;
    }

    private void ApplyOptionsWindowVisibility(bool condition)
    {
        CanvasGroup[] content = OptionsContent();
        for (int i = 0; i < content.Length; i++)
        {
            content[i].gameObject.SetActive(condition);
        }
    }

    // Everything the options window draws over its blur: title, description, background and options.
    private CanvasGroup[] OptionsContent()
    {
        if (optionsContent != null) return optionsContent;

        GameObject[] objects =
        {
            spawnOptionsWindow.TryGetElement<RectTransform>("Title Rect").gameObject,
            spawnOptionsWindow.TryGetElement<RectTransform>("Description Rect").gameObject,
            spawnOptionsWindow.TryGetElement<Image>("Background").gameObject,
            spawnOptionsWindow.TryGetElement<LayoutGroup>("Layout Group").gameObject
        };

        optionsContent = new CanvasGroup[objects.Length];
        for (int i = 0; i < objects.Length; i++)
        {
            if (!objects[i].TryGetComponent(out optionsContent[i])) optionsContent[i] = objects[i].AddComponent<CanvasGroup>();
        }

        return optionsContent;
    }

    // Same length as the blur fade, so the window and its background come in together.
    private IEnumerator FadeIn(float duration, params CanvasGroup[] groups)
    {
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            SetGroupsAlpha(groups, t / duration);
            yield return null;
        }

        SetGroupsAlpha(groups, 1f);
    }

    private static void SetGroupsAlpha(CanvasGroup[] groups, float alpha)
    {
        for (int i = 0; i < groups.Length; i++)
        {
            if (groups[i] != null) groups[i].alpha = alpha;
        }
    }
}
