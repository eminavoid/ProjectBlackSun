using UnityEngine;
using Zeke.UI;
using TMPro;

public class ResourceManager : Singleton<ResourceManager>
{
    [SerializeField] private PlayerResources playerResources;

    [Header("Start values")]
    [SerializeField] private int startWealth;
    [SerializeField] private int startZeal;
    [SerializeField] private int startFlock;
    [SerializeField] private int startAuthority;
    [SerializeField] private int startHappiness = 100;

    [Header("Modifiers")]
    [SerializeField] private float tithe = 0.5f;

    [Header("Passive production")]
    [SerializeField] private float monthlyWealthPerFlock;
    [SerializeField] private float monthlyZealPerFlock;

    [Header("User Interface")]
    [SerializeField] private UIWindow uiWindow;

    public static PlayerResources Resources => Instance.playerResources;

    private UIWindow resourceWindow = null;

    public void SetTithe(float value)
    {
        tithe = value;
    }

    protected override void OnInitialization()
    {
        playerResources.AddResource(Resource.Wealth, startWealth);
        playerResources.AddResource(Resource.Zeal, startZeal);
        playerResources.AddResource(Resource.Flock, startFlock);
        playerResources.AddResource(Resource.Authority, startAuthority);
        playerResources.AddResource(Resource.Happiness, startHappiness);

        resourceWindow = Instantiate(uiWindow, GlobalReferences.ScreenCanvas.transform);

        resourceWindow.TryGetElement<TextMeshProUGUI>("Wealth").text = playerResources.GetResourceAmount(Resource.Wealth).ToString();
        resourceWindow.TryGetElement<TextMeshProUGUI>("Zeal").text = playerResources.GetResourceAmount(Resource.Zeal).ToString();
        resourceWindow.TryGetElement<TextMeshProUGUI>("Flock").text = playerResources.GetResourceAmount(Resource.Flock).ToString();
        resourceWindow.TryGetElement<TextMeshProUGUI>("Authority").text = playerResources.GetResourceAmount(Resource.Authority).ToString();

        resourceWindow.TryGetElement<TextMeshProUGUI>("Happiness").text = playerResources.GetResourceAmount(Resource.Happiness).ToString() + "%";

        playerResources.onResourceGained += OnResourceGained;
    }

    private void OnResourceGained(Resource resource, int amount)
    {
        resourceWindow.TryGetElement<TextMeshProUGUI>(resource.ToString()).text = playerResources.GetResourceAmount(resource).ToString();
    }

    private void Start()
    {
        GameTime.OnTurnEnded += OnTurnEnd;
    }

    private void OnTurnEnd()
    {
        int flock = playerResources.GetResourceAmount(Resource.Flock);

        int wealthGain = Mathf.FloorToInt(monthlyWealthPerFlock * flock * (1 - tithe));
        int zealGain = Mathf.FloorToInt(monthlyZealPerFlock * flock * tithe);

        playerResources.AddResource(Resource.Wealth, wealthGain);
        playerResources.AddResource(Resource.Zeal, zealGain);
    }
}