using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zeke.UI;

public class EventsMenu : MonoBehaviour
{
    [SerializeField] private GameObject ui;
    [SerializeField] private RectTransform content;
    [SerializeField] private UIWindow elementPrefab;
    [SerializeField] private GameObject emptyState;

    private MapCameraController mapCameraController;

    public GameObject Panel => ui;

    private readonly Dictionary<int, UIWindow> seedWindows = new Dictionary<int, UIWindow>();

    private void Awake()
    {
        mapCameraController = FindAnyObjectByType<MapCameraController>();
        NodeTooltipMenuBlock.Ensure(ui);
    }

    private void Start()
    {
        SeedEventManager.onSeedStored += OnSeedQueued;
        SeedEventManager.onSeedRemoved += OnSeedRemoved;
        RefreshEmptyState();
    }

    private void OnDestroy()
    {
        SeedEventManager.onSeedStored -= OnSeedQueued;
        SeedEventManager.onSeedRemoved -= OnSeedRemoved;
    }

    //Temporal
    private void OnSeedQueued(Seed seed)
    {
        UIWindow window = CreateElement(seed);
        Debug.Log($"Adding {window} window to dictionary with seed: {seed} with id: {seed.UniqueID}");
        seedWindows.Add(seed.UniqueID, window);
        RefreshEmptyState();
    }

    private void OnSeedRemoved(Seed seed)
    {
        Debug.Log($"Trying to remove {seed} with id: {seed.UniqueID}");
        seedWindows[seed.UniqueID].DestroyWindow();
        seedWindows.Remove(seed.UniqueID);
        RefreshEmptyState();
    }

    private void RefreshEmptyState()
    {
        if (emptyState != null) emptyState.SetActive(seedWindows.Count == 0);
    }

    //Needs a dequeue method to destroy them 

    private UIWindow CreateElement(Seed seed)
    {
        UIWindow window = Instantiate(elementPrefab, content);

        DistrictZone zone = seed.CurrentZone;
        window.TryGetElement<TextMeshProUGUI>("Name").text = seed.Title;
        window.TryGetElement<TextMeshProUGUI>("Position Text").text = zone != null
            ? $"{MapStatsQuery.DistrictLabel(zone.District)} district  ·  {MapStatsQuery.ZoneLabel(zone)}"
            : "The city";

        Button position = window.TryGetElement<Button>("Position Clickbox");
        if (position != null)
        {
            if (zone != null && mapCameraController != null)
                position.onClick.AddListener(() => mapCameraController.FocusOnZone(zone));
            position.onClick.AddListener(() => MenuTransition.Hide(ui));
        }

        Button elementClickbox = window.TryGetElement<Button>("Event Clickbox");

        elementClickbox.onClick.AddListener(() => SeedEventManager.CreateSeedEventMenu(seed));
        elementClickbox.onClick.AddListener(() => MenuTransition.Hide(ui));

        return window;
    }
}