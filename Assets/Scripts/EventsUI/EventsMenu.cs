using UnityEngine;
using Zeke.UI;
using TMPro;
using UnityEngine.UI;

public class EventsMenu : MonoBehaviour
{
    [SerializeField] private RectTransform content;
    [SerializeField] private UIWindow elementPrefab;

    private void Start()
    {
        SeedEventManager.onSeedQueued += OnSeedQueued;
    }

    //Temporal
    private void OnSeedQueued(Seed seed)
    {
        CreateElement(seed);
    }

    //Needs a dequeue method to destroy them 

    private void CreateElement(Seed seed)
    {
        UIWindow window = Instantiate(elementPrefab, content);

        window.TryGetElement<TextMeshProUGUI>("Name").text = seed.Title;
        window.TryGetElement<TextMeshProUGUI>("Position Text").text = "Position Name: WIP";

        //window.TryGetElement<Button>("Position Clickbox").onClick.AddListener(); <-- Implement move to camera method here ex: "() => MoveToCamera"

        window.TryGetElement<Button>("Event Clickbox").onClick.AddListener(() => DebugLog());
    }

    private void DebugLog()
    {
        Debug.Log("Event clicked");
    }
}