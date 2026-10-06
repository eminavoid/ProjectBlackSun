using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
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

    [Header("Choosing an Option")]
    [Tooltip("The other options fade out, one after another outwards from the chosen one.")]
    [SerializeField, Min(0.01f)] private float othersFadeDuration = 0.32f;
    [SerializeField, Min(0f)] private float othersFadeStagger = 0.06f;
    [Tooltip("From the click to the cost leaving the resources bar.")]
    [SerializeField, Min(0f)] private float costFlightDelay = 0.15f;
    [SerializeField, Min(0f)] private float costFlightStagger = 0.13f;
    [Tooltip("Pause after the cost lands, before the option runs.")]
    [SerializeField, Min(0f)] private float choiceBeat = 0.2f;

    [Header("Result Transition")]
    [Tooltip("The result's parchment unrolls out of the chosen option.")]
    [SerializeField, Min(0.01f)] private float unrollDuration = 0.5f;
    [Tooltip("The event's window fades away under the unrolling result.")]
    [SerializeField, Min(0.01f)] private float optionsFadeOutDuration = 0.25f;

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

    private void OnOptionChosen(OptionDisplay chosen)
    {
        if (choosing) return;
        StartCoroutine(PlayChoice(chosen));
    }

    // The chosen option stays lit while the others fade away, its cost flies out of the resources bar into it,
    // and only then the option runs; its result unrolls out of it.
    private IEnumerator PlayChoice(OptionDisplay chosen)
    {
        choosing = true;
        LayoutGroup layout = spawnOptionsWindow.TryGetElement<LayoutGroup>("Layout Group");
        if (layout.TryGetComponent(out CanvasGroup optionsGroup)) optionsGroup.blocksRaycasts = false;

        if (chosen.TryGetComponent(out OptionHoverTextFeedback feedback)) feedback.LockHighlighted();
        Pulse(chosen.transform, -0.05f);
        FadeOutOtherOptions(layout, chosen);

        // Only what the option spends flies in; anything it gives up front shows in the bar when it runs.
        var fixedChanges = new List<(RectTransform icon, ResourceDelta delta)>();
        chosen.GetFixedChangeIcons(fixedChanges);
        var flights = new List<Tween>();
        for (int i = 0; i < fixedChanges.Count; i++)
        {
            (RectTransform icon, ResourceDelta delta) = fixedChanges[i];
            if (delta.Amount >= 0 || ResourceManager.Sidebar == null) continue;

            Tween flight = ResourceManager.Sidebar.Send(chosen, delta.Resource, delta.Amount, icon, costFlightDelay + flights.Count * costFlightStagger);
            if (flight != null) flights.Add(flight);
        }

        for (int i = 0; i < flights.Count; i++) yield return flights[i].WaitForCompletion();
        if (flights.Count > 0) Pulse(chosen.transform, 0.04f);
        yield return new WaitForSecondsRealtime(flights.Count > 0 ? choiceBeat : othersFadeDuration);

        // The cost really leaves now: the bar already shows it, so it stops holding it in the same frame.
        RectTransform rect = (RectTransform)chosen.transform;
        choiceOrigin = (rect.TransformPoint(rect.rect.center), Vector2.Scale(rect.rect.size, rect.lossyScale));
        chosen.Option.ExecuteOption();
        if (ResourceManager.Sidebar != null) ResourceManager.Sidebar.Release(chosen);
        choiceOrigin = null;

        if (optionsGroup != null) optionsGroup.blocksRaycasts = true;
        choosing = false;
        OnOptionExecuted();
    }

    // A quick wobble of the option's size. Negative amounts dip first (a press), positive ones grow first (a thump).
    private static void Pulse(Transform option, float amount)
    {
        option.DOPunchScale(Vector3.one * amount, 0.27f, 2, 0.5f).SetLink(option.gameObject).SetUpdate(true);
    }

    // The others shrink a bit and fade, one after another outwards from the chosen one.
    // They stay in place (just invisible) so the layout doesn't move the chosen option.
    private void FadeOutOtherOptions(LayoutGroup layout, OptionDisplay chosen)
    {
        int chosenIndex = chosen.transform.GetSiblingIndex();
        foreach (Transform option in layout.transform)
        {
            if (option == chosen.transform) continue;

            DOTween.Sequence()
                .Join(GroupOf(option.gameObject).DOFade(0f, othersFadeDuration))
                .Join(option.DOScale(option.localScale * 0.92f, othersFadeDuration))
                .SetDelay(Mathf.Abs(option.GetSiblingIndex() - chosenIndex) * othersFadeStagger)
                .SetEase(Ease.OutCubic).SetLink(option.gameObject).SetUpdate(true);
        }
    }

    private void OnOptionExecuted()
    {
        LayoutGroup layout = spawnOptionsWindow.TryGetElement<LayoutGroup>("Layout Group");

        // A result window is taking over: this event fades away under it but keeps its blur; the queue resumes once it closes.
        if (openOutputWindows > 0)
        {
            optionsFade?.Kill();
            optionsFade = FadeGroups(OptionsContent(), 0f, optionsFadeOutDuration).OnComplete(() =>
            {
                ApplyOptionsWindowVisibility(false);
                DestroyOptions(layout);
            });
            return;
        }

        DestroyOptions(layout);

        if (seedEvents.Count > 0)
        {
            StartChoosingOptionsPhase();
        }
        else
        {
            SetOptionsWindowVisibility(false);
        }
    }

    private static void DestroyOptions(LayoutGroup layout)
    {
        foreach (Transform option in layout.transform)
        {
            Destroy(option.gameObject);
        }
    }

    public static void CreateEventOutputWindow(string title, string description, IReadOnlyList<ResourceDelta> resourceChanges = null)
    {
        UIWindow windowInstance = Instantiate(Instance.eventOutputWindowPrefab, GlobalReferences.ScreenCanvas.transform);
        windowInstance.TryGetElement<TextMeshProUGUI>("Title").text = title;
        windowInstance.TryGetElement<TextMeshProUGUI>("Description").text = description;

        ResourceAmountRow resourceRow = windowInstance.GetComponentInChildren<ResourceAmountRow>(true);
        if (resourceRow != null) resourceRow.Show(resourceChanges);

        Instance.StartCoroutine(Instance.ShowOutputWindowAfterBlur(windowInstance, Instance.choiceOrigin));
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
        DestroyOptions(layout);
        spawnOptionsWindow.TryGetElement<TextMeshProUGUI>("Title").text = seed.Title;
        spawnOptionsWindow.TryGetElement<TextMeshProUGUI>("Description").text = seed.Description;

        RegionSeal seal = spawnOptionsWindow.GetComponentInChildren<RegionSeal>(true);
        if (seal != null) seal.Show(seed.Region);

        for (int i = 0; i < seed.Options.Count; i++)
        {
            OptionDisplay display = Instantiate(optionDisplayPrefab, layout.transform);

            if (display.TryGetComponent(out UIWindow uiWIndow))
            {
                uiWIndow.TryGetElement<TextMeshProUGUI>("Title").text = seed.Options[i].Title;
                uiWIndow.TryGetElement<TextMeshProUGUI>("Description").text = seed.Options[i].Description;
            }

            display.InitializeData(seed.Options[i]);
            display.onOptionChosen += OnOptionChosen;
        }
    }

    private Coroutine optionsVisibilityRoutine;
    private Tween optionsFade;
    private CanvasGroup[] optionsContent;
    private int resourceWindowOriginalIndex = -1;
    private int openOutputWindows;
    private bool choosing;
    // Where the chosen option is on screen (world space) while it runs, for its result to unroll from.
    private (Vector3 center, Vector2 size)? choiceOrigin;

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

        // Same length as the blur fade, so the window and its background come in together.
        optionsFade?.Kill();
        SetGroupsAlpha(OptionsContent(), 0f);
        optionsFade = FadeGroups(OptionsContent(), 1f, blurComponent != null ? blurComponent.FadeDuration : 0f);
        optionsVisibilityRoutine = null;
    }

    // Same rule for the result window: it stays invisible until its blur has the snapshot.
    private IEnumerator ShowOutputWindowAfterBlur(UIWindow window, (Vector3 center, Vector2 size)? origin)
    {
        openOutputWindows++;

        CanvasGroup group = GroupOf(window.gameObject);
        group.alpha = 0f;

        UIBackgroundBlur blur = window.GetComponentInChildren<UIBackgroundBlur>();
        while (blur != null && !blur.IsReady) yield return null;

        if (window != null)
        {
            KeepResourceWindowSharp(window.transform);
            if (origin.HasValue) Unroll(window, group, origin.Value);
            else group.DOFade(1f, blur != null ? blur.FadeDuration : 0f).SetLink(window.gameObject).SetUpdate(true);
        }

        while (window != null) yield return null;

        // Let the result's chips land in the resources bar before the next event blurs the screen.
        while (ResourceManager.Sidebar != null && ResourceManager.Sidebar.IsBusy) yield return null;

        openOutputWindows--;
        if (openOutputWindows > 0) yield break;

        if (seedEvents.Count > 0)
        {
            StartChoosingOptionsPhase();
        }
        else
        {
            SetOptionsWindowVisibility(false);
        }
    }

    // The result's parchment starts as the chosen option's strip and opens up to its size on its way to its place;
    // the rest of the window fades in once it's open.
    private void Unroll(UIWindow window, CanvasGroup windowGroup, (Vector3 center, Vector2 size) origin)
    {
        Image background = window.TryGetElement<Image>("Background");
        if (background == null)
        {
            windowGroup.DOFade(1f, unrollDuration).SetLink(window.gameObject).SetUpdate(true);
            return;
        }

        // Its blur shows the same snapshot as the event's, still behind it: fading it in fades the event out.
        RectTransform parchment = background.rectTransform;
        CanvasGroup parchmentGroup = GroupOf(parchment.gameObject);
        CanvasGroup blurGroup = null;
        var content = new List<CanvasGroup>();
        foreach (Transform child in window.transform)
        {
            if (child == parchment) continue;
            if (child.GetComponent<UIBackgroundBlur>() != null) blurGroup = GroupOf(child.gameObject);
            else content.Add(GroupOf(child.gameObject));
        }

        Vector3 endPosition = parchment.localPosition;
        Vector3 endScale = parchment.localScale;
        Vector2 size = Vector2.Scale(parchment.rect.size, parchment.lossyScale);
        parchment.localPosition = parchment.parent.InverseTransformPoint(origin.center);
        parchment.localScale = new Vector3(endScale.x * origin.size.x / size.x, endScale.y * origin.size.y / size.y, endScale.z);
        parchmentGroup.alpha = 0f;
        SetGroupsAlpha(content, 0f);
        windowGroup.alpha = 1f;

        // The parchment dissolves in over the option it comes out of and snaps open; the texts come in once it's open.
        Sequence unroll = DOTween.Sequence()
            .Insert(0f, parchment.DOLocalMove(endPosition, unrollDuration).SetEase(Ease.OutCubic))
            .Insert(0f, parchment.DOScaleX(endScale.x, unrollDuration).SetEase(Ease.OutCubic))
            .Insert(0f, parchment.DOScaleY(endScale.y, unrollDuration).SetEase(Ease.OutBack))
            .Insert(0f, parchmentGroup.DOFade(1f, unrollDuration * 0.3f))
            .Insert(unrollDuration * 0.6f, FadeGroups(content, 1f, unrollDuration * 0.4f))
            .SetLink(window.gameObject).SetUpdate(true);

        if (blurGroup != null)
        {
            blurGroup.alpha = 0f;
            unroll.Insert(0f, blurGroup.DOFade(1f, optionsFadeOutDuration));
        }
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
            optionsContent[i] = GroupOf(objects[i]);
        }

        return optionsContent;
    }

    private static CanvasGroup GroupOf(GameObject target)
    {
        return target.TryGetComponent(out CanvasGroup group) ? group : target.AddComponent<CanvasGroup>();
    }

    // Fades the groups together, from wherever they are to alpha.
    private static Sequence FadeGroups(IReadOnlyList<CanvasGroup> groups, float alpha, float duration)
    {
        Sequence fade = DOTween.Sequence().SetUpdate(true);
        for (int i = 0; i < groups.Count; i++)
        {
            fade.Insert(0f, groups[i].DOFade(alpha, duration));
        }
        return fade;
    }

    private static void SetGroupsAlpha(IReadOnlyList<CanvasGroup> groups, float alpha)
    {
        for (int i = 0; i < groups.Count; i++)
        {
            if (groups[i] != null) groups[i].alpha = alpha;
        }
    }
}
