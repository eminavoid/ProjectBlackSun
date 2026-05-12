using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zeke.UI;
using TMPro;

public class SeedEventManager : Singleton<SeedEventManager>
{
    [SerializeField] private UIWindow spawnOptionsWindow;
    [SerializeField] private UIWindow eventOutputWindowPrefab;
    [SerializeField] private OptionDisplay optionDisplayPrefab;

    private readonly Queue<Seed> seedEvents = new Queue<Seed>();

    public static void EnqueueSeedEvent(Seed seed)
    {
        Instance.seedEvents.Enqueue(seed);
    }

    private void Start()
    {
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
    }

    private void StartChoosingOptionsPhase()
    {
        if (seedEvents.Count <= 0)
        {
            return;
        }

        SetOptionsWindowVisibility(true);
        CreateSeedOptionsInCanvas(seedEvents.Dequeue());
    }

    private void CreateSeedOptionsInCanvas(Seed seed)
    {
        LayoutGroup layout = spawnOptionsWindow.TryGetElement<LayoutGroup>("Layout Group");
        spawnOptionsWindow.TryGetElement<TextMeshProUGUI>("Title").text = seed.Title;
        spawnOptionsWindow.TryGetElement<TextMeshProUGUI>("Description").text = seed.Description;

        for (int i = 0; i < seed.Options.Count; i++)
        {
            OptionDisplay display = Instantiate(optionDisplayPrefab, layout.transform);

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