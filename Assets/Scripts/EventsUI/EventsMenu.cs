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

    private MapCameraController mapCameraController;

    private readonly Dictionary<int, UIWindow> seedWindows = new Dictionary<int, UIWindow>();

    private void Awake()
    {
        mapCameraController = FindAnyObjectByType<MapCameraController>();
    }

    private void Start()
    {
        SeedEventManager.onSeedStored += OnSeedQueued;
        SeedEventManager.onSeedRemoved += OnSeedRemoved;
    }

    //Temporal
    private void OnSeedQueued(Seed seed)
    {
        UIWindow window = CreateElement(seed);
        Debug.Log($"Adding {window} window to dictionary with seed: {seed} with id: {seed.UniqueID}");
        seedWindows.Add(seed.UniqueID, window);
    }

    private void OnSeedRemoved(Seed seed)
    {
        Debug.Log($"Trying to remove {seed} with id: {seed.UniqueID}");
        seedWindows[seed.UniqueID].DestroyWindow();
        seedWindows.Remove(seed.UniqueID);
    }

    //Needs a dequeue method to destroy them 

    private UIWindow CreateElement(Seed seed)
    {
        UIWindow window = Instantiate(elementPrefab, content);

        window.TryGetElement<TextMeshProUGUI>("Name").text = seed.Title;
        window.TryGetElement<TextMeshProUGUI>("Position Text").text = seed.CurrentZone.name;

        window.TryGetElement<Button>("Position Clickbox").onClick.AddListener(() => mapCameraController.FocusOnZone(seed.CurrentZone));
        window.TryGetElement<Button>("Position Clickbox").onClick.AddListener(() => ui.SetActive(false));

        Button elementClickbox = window.TryGetElement<Button>("Event Clickbox");

        elementClickbox.onClick.AddListener(() => SeedEventManager.CreateSeedEventMenu(seed));
        elementClickbox.onClick.AddListener(() => ui.SetActive(false));

        return window;
    }
}