using UnityEngine;
using Zeke.UI;

public class ResourceManager : Singleton<ResourceManager>
{
    [SerializeField] private PlayerResources playerResources;

    [Header("Start values")]
    [SerializeField] private int startWealth;
    [SerializeField] private int startZeal;
    [SerializeField] private int startFlock;
    [SerializeField] private int startAuthority;
    [SerializeField] private int startHappiness = 100;
    [SerializeField] private int startMaterials;
    [SerializeField] private int startSecrets;

    [Header("Modifiers")]
    [SerializeField] private float tithe = 0.5f;

    [Header("User Interface")]
    [SerializeField] private UIWindow uiWindow;

    public static PlayerResources Resources => Instance.playerResources;
    public static Transform ResourceWindowTransform => Instance.resourceWindow != null ? Instance.resourceWindow.transform : null;
    public static ResourceSidebar Sidebar => Instance.sidebar;

    private UIWindow resourceWindow = null;
    private ResourceSidebar sidebar = null;

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
        playerResources.AddResource(Resource.Materials, startMaterials);
        playerResources.AddResource(Resource.Secrets, startSecrets);

        resourceWindow = Instantiate(uiWindow, GlobalReferences.ScreenCanvas.transform);
        sidebar = resourceWindow.GetComponent<ResourceSidebar>();
        sidebar.Initialize(playerResources);
    }

    private void Start()
    {
        GameTime.OnTurnEnded += OnTurnEnd;
    }

    private void OnTurnEnd()
    {
        //Nothing
    }
}