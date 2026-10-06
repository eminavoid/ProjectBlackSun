using UnityEngine;
using Zeke.UI;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class EventsMenu : MonoBehaviour
{
    [SerializeField] private GameObject ui;
    [SerializeField] private RectTransform content;
    [SerializeField] private UIWindow elementPrefab;

    private readonly Dictionary<Seed, UIWindow> seedWindows = new Dictionary<Seed, UIWindow>();

    private void Start()
    {
        SeedEventManager.onSeedStored += OnSeedQueued;
        SeedEventManager.onSeedRemoved += OnSeedRemoved;
    }

    //Temporal
    private void OnSeedQueued(Seed seed)
    {
        CreateElement(seed);
    }

    private void OnSeedRemoved(Seed seed)
    {
        seedWindows[seed].DestroyWindow();
        seedWindows.Remove(seed);
    }

    //Needs a dequeue method to destroy them 

    private void CreateElement(Seed seed)
    {
        UIWindow window = Instantiate(elementPrefab, content);

        window.TryGetElement<TextMeshProUGUI>("Name").text = seed.Title;
        window.TryGetElement<TextMeshProUGUI>("Position Text").text = "Position Name: WIP";

        //window.TryGetElement<Button>("Position Clickbox").onClick.AddListener(); <-- Implement move to camera method here ex: "() => MoveCamera(node.positionidk)"

        Button elementClickbox = window.TryGetElement<Button>("Event Clickbox");

        elementClickbox.onClick.AddListener(() => SeedEventManager.CreateSeedEventMenu(seed));
        elementClickbox.onClick.AddListener(() => ui.SetActive(false));

        seedWindows.Add(seed, window);
    }
}