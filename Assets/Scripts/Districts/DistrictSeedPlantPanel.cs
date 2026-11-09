using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DistrictSeedPlantPanel : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private SeedsPool loadSeeds;

    [Header("UI")]
    [SerializeField] private bool showDebugLogs;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text districtLabel;
    [SerializeField] private TMP_Text statusLabel;
    [SerializeField] private Transform seedListContent;
    [SerializeField] private ScrollRect seedListScrollRect;
    [SerializeField] private Button seedItemButtonPrefab;
    [SerializeField] private Button plantButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button openMenuButton;
    [SerializeField] private Button toggleMenuButton;
    [SerializeField] private string optionsRectName = "OptionsRect";
    [SerializeField] private string optionsOpenButtonName = "Button (2)";

    private readonly Dictionary<Seed, int> currentSeeds = new Dictionary<Seed, int>();
    private readonly List<Seed> cachedSeeds = new List<Seed>();
    private readonly List<Button> runtimeSeedButtons = new List<Button>();

    public static event System.Action Planted;

    private bool isOpen;
    private int selectedSeedIndex = -1;
    private string statusMessage = string.Empty;

    private bool standardPoolReleased;

    private void Awake()
    {
        SetPanelVisible(false);
        if (!TutorialSession.SuppressAmbientEvents) ReleaseStandardPool();
    }

    private void DebugLoadSeedsFromPool()
    {
        for (int i = 0; i < loadSeeds.EvilSeeds.Count; i++)
        {
            Seed seed = loadSeeds.EvilSeeds[i];
            if (seed == null || !seed.HasDisplayTitle) continue;
            if (!currentSeeds.ContainsKey(seed)) currentSeeds.Add(seed, 0);
            currentSeeds[seed] += 1;
        }
    }

    private void OnEnable()
    {
        DistrictSelectionController.OnSelectionChanged += OnDistrictSelectionChanged;
    }

    private void Start()
    {
        ResolveOpenMenuButton();
        ResolveSeedListScrollRect();
        EnsureScrollContentLayout();
        WireButtons();
        SetPanelVisible(false);
    }

    private void OnDisable()
    {
        DistrictSelectionController.OnSelectionChanged -= OnDistrictSelectionChanged;
        UnwireButtons();
        ClearSeedButtons();
    }

    public void OpenMenu()
    {
        if (panelRoot == null)
        {
            Debug.LogError("DistrictSeedPlantPanel: panelRoot no asignado.", this);
            return;
        }

        isOpen = true;
        selectedSeedIndex = -1;
        statusMessage = string.Empty;
        RefreshSeedList();
        BuildSeedButtons();
        UpdatePanelTexts();
        UpdatePlantButtonState();
        ResetScrollToTop();
        SetPanelVisible(true);

        if (showDebugLogs)
        {
            Debug.Log($"DistrictSeedPlantPanel: menú abierto ({cachedSeeds.Count} seed(s)).", this);
        }
    }

    public void CloseMenu()
    {
        isOpen = false;
        SetPanelVisible(false);
    }

    public static bool TryCloseOpen()
    {
        DistrictSeedPlantPanel panel = FindAnyObjectByType<DistrictSeedPlantPanel>();
        if (panel == null || !panel.isOpen) return false;
        panel.CloseMenu();
        return true;
    }

    public bool MenuOpen => isOpen;
    public Button MenuButton => openMenuButton;
    public Button PlantButton => plantButton;
    public System.Collections.Generic.IReadOnlyList<Seed> KnownSeeds =>
        loadSeeds != null && loadSeeds.EvilSeeds != null ? loadSeeds.EvilSeeds : System.Array.Empty<Seed>();

    public static void ReleaseStandardSeeds()
    {
        DistrictSeedPlantPanel panel = FindAnyObjectByType<DistrictSeedPlantPanel>();
        if (panel != null) panel.ReleaseStandardPool();
    }

    public void ReleaseStandardPool()
    {
        if (standardPoolReleased) return;
        standardPoolReleased = true;
        DebugLoadSeedsFromPool();
        if (!isOpen) return;

        RefreshSeedList();
        BuildSeedButtons();
        UpdatePanelTexts();
        UpdatePlantButtonState();
    }

    public static void ForgetSeed(Seed seed)
    {
        DistrictSeedPlantPanel panel = FindAnyObjectByType<DistrictSeedPlantPanel>();
        if (panel != null) panel.Forget(seed);
    }

    public void Forget(Seed seed)
    {
        if (seed == null || !currentSeeds.ContainsKey(seed)) return;
        currentSeeds.Remove(seed);
        if (!isOpen) return;

        RefreshSeedList();
        BuildSeedButtons();
        UpdatePanelTexts();
        UpdatePlantButtonState();
    }

    public int Owned(Seed seed)
    {
        if (seed == null) return 0;
        return currentSeeds.TryGetValue(seed, out int amount) ? amount : 0;
    }

    public void Grant(Seed seed, int amount)
    {
        if (seed == null || amount == 0) return;
        if (!currentSeeds.ContainsKey(seed)) currentSeeds.Add(seed, 0);
        currentSeeds[seed] += amount;
        if (currentSeeds[seed] <= 0)
        {
            currentSeeds.Remove(seed);
        }

        if (!isOpen) return;
        RefreshSeedList();
        BuildSeedButtons();
        UpdatePanelTexts();
        UpdatePlantButtonState();
    }

    public void ToggleMenu()
    {
        if (isOpen) CloseMenu();
        else OpenMenu();
    }

    private void OnDistrictSelectionChanged(Districts? district)
    {
        if (!isOpen) return;

        selectedSeedIndex = -1;
        statusMessage = string.Empty;
        RefreshSeedList();
        BuildSeedButtons();
        UpdatePanelTexts();
        UpdatePlantButtonState();
    }

    private void RefreshSeedList()
    {
        cachedSeeds.Clear();

        Districts? filterDistrict = DistrictSelectionController.SelectedDistrict;

        foreach (Seed seed in currentSeeds.Keys)
        {
            if (seed == null || !seed.HasDisplayTitle) continue;
            if (seed == TutorialContent.ReturnedSeed && !TutorialSession.SuppressAmbientEvents) continue;

            if (filterDistrict.HasValue && !seed.CanPlantInDistrict(filterDistrict.Value)) continue;

            cachedSeeds.Add(seed);
        }
    }

    private void TryPlantSelectedSeed()
    {
        if (!isOpen)
        {
            statusMessage = "Open the seeds menu first.";
            UpdatePanelTexts();
            return;
        }

        DistrictZone targetZone = DistrictSelectionController.SelectedZone;
        if (targetZone == null)
        {
            statusMessage = "Pick a zone on the map first.";
            UpdatePanelTexts();
            UpdatePlantButtonState();
            return;
        }

        if (selectedSeedIndex < 0 || selectedSeedIndex >= cachedSeeds.Count)
        {
            statusMessage = "Pick a seed first.";
            UpdatePanelTexts();
            return;
        }

        Seed selectedSeed = cachedSeeds[selectedSeedIndex];

        if (targetZone.IsOccupied || !targetZone.AddSeed(selectedSeed, FactionId.Player))
        {
            ShowPlantPopup("That zone already has a seed growing. Pick another one.");
            return;
        }

        if (showDebugLogs)
        {
            Debug.Log($"DistrictSeedPlantPanel: planted '{selectedSeed.Title}' in {targetZone.SectorName} ({targetZone.District}).", this);
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySeedPlant();
        }

        ShowPlantPopup($"{selectedSeed.Title} planted in the {MapStatsQuery.DistrictLabel(targetZone.District)} district.");

        UpdateSeedInventory(selectedSeed, -1);
        if (selectedSeed == TutorialContent.FirstSeed) ReleaseStandardPool();
        RefreshSeedList();
        Planted?.Invoke();
    }

    private void UpdateSeedInventory(Seed seed, int amount)
    {
        currentSeeds[seed] += amount;

        if (currentSeeds[seed] <= 0) currentSeeds.Remove(seed);
    }

    private void ShowPlantPopup(string message)
    {
        CloseMenu();
        TopScreenNotification.Show(message, 4f);
    }

    private void ResolveOpenMenuButton()
    {
        if (openMenuButton != null) return;

        GameObject optionsRect = GameObject.Find(optionsRectName);
        if (optionsRect == null)
        {
            Debug.LogWarning($"DistrictSeedPlantPanel: no se encontró '{optionsRectName}'.", this);
            return;
        }

        Transform buttonTransform = optionsRect.transform.Find(optionsOpenButtonName);
        if (buttonTransform == null)
        {
            Debug.LogWarning($"DistrictSeedPlantPanel: no se encontró '{optionsOpenButtonName}' bajo '{optionsRectName}'.", this);
            return;
        }

        openMenuButton = buttonTransform.GetComponent<Button>();
        if (openMenuButton == null)
        {
            Debug.LogWarning($"DistrictSeedPlantPanel: '{optionsOpenButtonName}' no tiene componente Button.", this);
        }
    }

    private void WireButtons()
    {
        UnwireButtons();

        if (plantButton != null) plantButton.onClick.AddListener(TryPlantSelectedSeed);
        if (closeButton != null) closeButton.onClick.AddListener(CloseMenu);
        if (openMenuButton != null) openMenuButton.onClick.AddListener(ToggleMenu);
        if (toggleMenuButton != null) toggleMenuButton.onClick.AddListener(ToggleMenu);

        if (openMenuButton == null)
        {
            Debug.LogWarning("DistrictSeedPlantPanel: openMenuButton no asignado; el menú no se abrirá con UI.", this);
        }
    }

    private void UnwireButtons()
    {
        if (plantButton != null) plantButton.onClick.RemoveListener(TryPlantSelectedSeed);
        if (closeButton != null) closeButton.onClick.RemoveListener(CloseMenu);
        if (openMenuButton != null) openMenuButton.onClick.RemoveListener(ToggleMenu);
        if (toggleMenuButton != null) toggleMenuButton.onClick.RemoveListener(ToggleMenu);
    }

    private void SetPanelVisible(bool visible)
    {
        if (panelRoot == null) return;
        if (!visible)
        {
            MenuTransition.Hide(panelRoot);
            return;
        }

        NodeTooltipMenuBlock.Ensure(panelRoot);
        if (closeButton != null) closeButton.transform.SetAsLastSibling();
        MenuTransition.Show(panelRoot);
    }

    private void UpdatePlantButtonState()
    {
        if (plantButton == null) return;

        DistrictZone selectedZone = DistrictSelectionController.SelectedZone;
        plantButton.interactable = isOpen
            && selectedZone != null
            && selectedSeedIndex >= 0
            && selectedSeedIndex < cachedSeeds.Count;
    }

    private void UpdatePanelTexts()
    {
        if (districtLabel != null)
        {
            DistrictZone selectedZone = DistrictSelectionController.SelectedZone;
            if (selectedZone == null)
            {
                districtLabel.text = "Zone: none";
            }
            else
            {
                districtLabel.text = $"Zone: {MapStatsQuery.DistrictLabel(selectedZone.District)} district";
            }
        }

        if (statusLabel != null) statusLabel.text = statusMessage;
    }

    private void BuildSeedButtons()
    {
        if (seedListContent == null) return;

        ClearSeedButtons();

        Button templateButton = seedItemButtonPrefab != null
            ? seedItemButtonPrefab
            : seedListContent.GetComponentInChildren<Button>(true);

        if (templateButton == null) return;

        templateButton.gameObject.SetActive(false);

        for (int i = 0; i < cachedSeeds.Count; i++)
        {
            int index = i;
            Button instance = Instantiate(templateButton, seedListContent);
            instance.gameObject.SetActive(true);
            ConfigureSeedButton(instance, cachedSeeds[i], false);
            instance.onClick.AddListener(() => SelectSeed(index));
            runtimeSeedButtons.Add(instance);
        }

        UpdateSelectionVisuals();
        RefreshScrollContentLayout();
    }

    private void ResolveSeedListScrollRect()
    {
        if (seedListScrollRect != null) return;
        if (seedListContent == null) return;

        seedListScrollRect = seedListContent.GetComponentInParent<ScrollRect>();
    }

    private void EnsureScrollContentLayout()
    {
        if (seedListContent is not RectTransform content) return;

        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;

        if (content.TryGetComponent(out GridLayoutGroup _))
        {
            if (content.TryGetComponent(out ContentSizeFitter gridFitter))
            {
                Destroy(gridFitter);
            }

            return;
        }

        if (!content.TryGetComponent(out ContentSizeFitter fitter))
        {
            fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        }

        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        if (seedListScrollRect != null && seedListScrollRect.viewport != null)
        {
            RectTransform viewport = seedListScrollRect.viewport;
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
        }
    }

    private void RefreshScrollContentLayout()
    {
        if (seedListContent is not RectTransform content) return;

        if (content.TryGetComponent(out GridLayoutGroup grid))
        {
            FitGridContentHeight(content, grid);
            return;
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
    }

    private static void FitGridContentHeight(RectTransform content, GridLayoutGroup grid)
    {
        int visible = 0;
        for (int i = 0; i < content.childCount; i++)
        {
            if (content.GetChild(i).gameObject.activeSelf) visible++;
        }

        int columns = grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount
            ? Mathf.Max(1, grid.constraintCount)
            : Mathf.Max(1, visible);
        int rows = visible <= 0 ? 1 : Mathf.CeilToInt(visible / (float)columns);

        float height = grid.padding.top + grid.padding.bottom
            + rows * grid.cellSize.y
            + Mathf.Max(0, rows - 1) * grid.spacing.y;

        content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
    }

    private void ResetScrollToTop()
    {
        if (seedListScrollRect == null) return;
        seedListScrollRect.StopMovement();
        seedListScrollRect.verticalNormalizedPosition = 1f;
    }

    private void ClearSeedButtons()
    {
        for (int i = 0; i < runtimeSeedButtons.Count; i++)
        {
            if (runtimeSeedButtons[i] != null) Destroy(runtimeSeedButtons[i].gameObject);
        }

        runtimeSeedButtons.Clear();
    }

    private void SelectSeed(int index)
    {
        selectedSeedIndex = index;
        statusMessage = string.Empty;
        UpdatePanelTexts();
        UpdateSelectionVisuals();
        UpdatePlantButtonState();
    }

    private void UpdateSelectionVisuals()
    {
        for (int i = 0; i < runtimeSeedButtons.Count; i++)
        {
            if (runtimeSeedButtons[i] == null || i >= cachedSeeds.Count) continue;
            ConfigureSeedButton(runtimeSeedButtons[i], cachedSeeds[i], i == selectedSeedIndex);
        }
    }

    private static void ConfigureSeedButton(Button button, Seed seed, bool selected)
    {
        if (button == null || seed == null) return;

        SeedButtonView view = button.GetComponent<SeedButtonView>();
        if (view != null)
        {
            view.Bind(seed, selected);
            return;
        }

        TMP_Text tmp = button.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null) tmp.text = selected ? $"> {seed.Title}" : seed.Title;
    }
}
