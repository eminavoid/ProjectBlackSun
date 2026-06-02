using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DistrictSeedPlantPanel : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private SeedsPool seedsPool;

    [Header("UI")]
    [SerializeField] private bool showDebugLogs;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text districtLabel;
    [SerializeField] private TMP_Text statusLabel;
    [SerializeField] private Transform seedListContent;
    [SerializeField] private Button seedItemButtonPrefab;
    [SerializeField] private Button plantButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button openMenuButton;
    [SerializeField] private Button toggleMenuButton;
    [SerializeField] private string optionsRectName = "OptionsRect";
    [SerializeField] private string optionsOpenButtonName = "Button (2)";

    private readonly List<Seed> cachedSeeds = new List<Seed>();
    private readonly List<Button> runtimeSeedButtons = new List<Button>();

    private bool isOpen;
    private int selectedSeedIndex = -1;
    private string statusMessage = string.Empty;

    private void Awake()
    {
        SetPanelVisible(false);
    }

    private void OnEnable()
    {
        DistrictSelectionController.OnSelectionChanged += OnDistrictSelectionChanged;
    }

    private void Start()
    {
        ResolveOpenMenuButton();
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
        if (seedsPool == null || seedsPool.EvilSeeds == null) return;

        Districts? filterDistrict = DistrictSelectionController.SelectedDistrict;

        for (int i = 0; i < seedsPool.EvilSeeds.Count; i++)
        {
            Seed seed = seedsPool.EvilSeeds[i];
            if (seed == null) continue;

            if (filterDistrict.HasValue && !seed.CanPlantInDistrict(filterDistrict.Value)) continue;

            cachedSeeds.Add(seed);
        }
    }

    private void TryPlantSelectedSeed()
    {
        if (!isOpen)
        {
            statusMessage = "Abre el menú de seeds primero.";
            UpdatePanelTexts();
            return;
        }

        if (!DistrictSelectionController.SelectedDistrict.HasValue)
        {
            statusMessage = "Selecciona un distrito en el mapa antes de plantar.";
            UpdatePanelTexts();
            UpdatePlantButtonState();
            return;
        }

        if (selectedSeedIndex < 0 || selectedSeedIndex >= cachedSeeds.Count)
        {
            statusMessage = "Selecciona una seed primero.";
            UpdatePanelTexts();
            return;
        }

        Districts targetDistrict = DistrictSelectionController.SelectedDistrict.Value;
        Seed selectedSeed = cachedSeeds[selectedSeedIndex];

        if (!DistrictsManager.TryGetRandomNodeInDistrict(targetDistrict, out Node targetNode) || targetNode == null)
        {
            statusMessage = $"El distrito {targetDistrict} no tiene nodos disponibles.";
            UpdatePanelTexts();
            return;
        }

        if (!targetNode.AddSeed(selectedSeed))
        {
            statusMessage = $"No se pudo plantar en '{targetNode.name}' (nodo ocupado).";
            UpdatePanelTexts();
            return;
        }

        statusMessage = $"Seed '{selectedSeed.Title}' plantada en {targetDistrict}.";
        UpdatePanelTexts();

        if (showDebugLogs)
        {
            Debug.Log($"DistrictSeedPlantPanel: planted '{selectedSeed.Title}' in {targetDistrict}.", this);
        }

        CloseMenu();
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
        if (openMenuButton != null) openMenuButton.onClick.AddListener(OpenMenu);
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
        if (openMenuButton != null) openMenuButton.onClick.RemoveListener(OpenMenu);
        if (toggleMenuButton != null) toggleMenuButton.onClick.RemoveListener(ToggleMenu);
    }

    private void SetPanelVisible(bool visible)
    {
        if (panelRoot != null) panelRoot.SetActive(visible);
    }

    private void UpdatePlantButtonState()
    {
        if (plantButton == null) return;

        plantButton.interactable = isOpen
            && DistrictSelectionController.SelectedDistrict.HasValue
            && selectedSeedIndex >= 0
            && selectedSeedIndex < cachedSeeds.Count;
    }

    private void UpdatePanelTexts()
    {
        if (districtLabel != null)
        {
            districtLabel.text = DistrictSelectionController.SelectedDistrict.HasValue
                ? $"District: {DistrictSelectionController.SelectedDistrict.Value}"
                : "District: (ninguno)";
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
