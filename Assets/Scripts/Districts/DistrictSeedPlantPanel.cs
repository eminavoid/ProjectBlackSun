using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DistrictSeedPlantPanel : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private SeedsPool seedsPool;

    [Header("UI Wiring (Your Panel)")]
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text districtLabel;
    [SerializeField] private TMP_Text statusLabel;
    [SerializeField] private Transform seedListContent;
    [SerializeField] private Button seedItemButtonPrefab;
    [SerializeField] private Button plantButton;
    [SerializeField] private Button closeButton;

    [Header("Fallback (debug only)")]
    [SerializeField] private bool useOnGuiFallback = false;
    [SerializeField] private Rect panelRect = new Rect(24f, 24f, 360f, 420f);
    [SerializeField] private string panelTitle = "District Seed Selector";

    private readonly List<Seed> cachedSeeds = new List<Seed>();
    private readonly List<Button> runtimeSeedButtons = new List<Button>();

    private bool isOpen;
    private Vector2 scrollPosition;
    private Districts selectedDistrict;
    private int selectedSeedIndex = -1;
    private string statusMessage = string.Empty;

    private void OnEnable()
    {
        DistrictMeshView.OnDistrictActionSelected += OnDistrictActionSelected;
        WireButtons();
        SetPanelVisible(false);
    }

    private void OnDisable()
    {
        DistrictMeshView.OnDistrictActionSelected -= OnDistrictActionSelected;
        UnwireButtons();
        ClearSeedButtons();
    }

    private void OnDistrictActionSelected(Districts district, DistrictMeshView.DistrictAction action)
    {
        if (action != DistrictMeshView.DistrictAction.Seed) return;

        selectedDistrict = district;
        selectedSeedIndex = -1;
        statusMessage = string.Empty;
        RefreshSeedList();
        isOpen = true;
        BuildSeedButtons();
        UpdatePanelTexts();
        SetPanelVisible(true);

        if (showDebugLogs)
        {
            Debug.Log($"DistrictSeedPlantPanel: opened for {selectedDistrict} with {cachedSeeds.Count} seed option(s).", this);
        }
    }

    private void RefreshSeedList()
    {
        cachedSeeds.Clear();

        if (seedsPool == null || seedsPool.EvilSeeds == null) return;

        for (int i = 0; i < seedsPool.EvilSeeds.Count; i++)
        {
            Seed seed = seedsPool.EvilSeeds[i];
            if (seed == null) continue;
            cachedSeeds.Add(seed);
        }
    }

    private void OnGUI()
    {
        if (!useOnGuiFallback || panelRoot != null) return;
        if (!isOpen) return;

        panelRect = GUI.Window(GetInstanceID(), panelRect, DrawPanel, panelTitle);
    }

    private void DrawPanel(int windowId)
    {
        GUILayout.Label($"District: {selectedDistrict}");
        GUILayout.Space(4f);

        if (cachedSeeds.Count == 0)
        {
            GUILayout.Label("No seeds available. Assign a SeedsPool.");
        }
        else
        {
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(260f));
            for (int i = 0; i < cachedSeeds.Count; i++)
            {
                bool isSelected = selectedSeedIndex == i;
                string label = isSelected ? $"> {cachedSeeds[i].name}" : cachedSeeds[i].name;
                if (GUILayout.Button(label))
                {
                    selectedSeedIndex = i;
                    statusMessage = string.Empty;
                }
            }
            GUILayout.EndScrollView();
        }

        GUILayout.Space(8f);
        GUILayout.BeginHorizontal();

        GUI.enabled = selectedSeedIndex >= 0 && selectedSeedIndex < cachedSeeds.Count;
        if (GUILayout.Button("Plantar"))
        {
            TryPlantSelectedSeed();
        }
        GUI.enabled = true;

        if (GUILayout.Button("Cerrar"))
        {
            isOpen = false;
        }

        GUILayout.EndHorizontal();

        if (!string.IsNullOrEmpty(statusMessage))
        {
            GUILayout.Space(8f);
            GUILayout.Label(statusMessage);
        }

        GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
    }

    private void TryPlantSelectedSeed()
    {
        if (selectedSeedIndex < 0 || selectedSeedIndex >= cachedSeeds.Count)
        {
            statusMessage = "Selecciona una seed primero.";
            UpdatePanelTexts();
            return;
        }

        if (!DistrictsManager.TryGetDistrictData(selectedDistrict, out DistrictData districtData) || districtData == null)
        {
            statusMessage = $"No hay DistrictData para {selectedDistrict}.";
            UpdatePanelTexts();
            return;
        }

        Node randomNode = districtData.GetRandomNode();
        if (randomNode == null)
        {
            statusMessage = $"El distrito {selectedDistrict} no tiene nodos disponibles.";
            UpdatePanelTexts();
            return;
        }

        Seed selectedSeed = cachedSeeds[selectedSeedIndex];
        bool planted = randomNode.AddSeed(selectedSeed);
        if (!planted)
        {
            statusMessage = $"No se pudo plantar '{selectedSeed.name}' porque el nodo '{randomNode.name}' ya está ocupado.";
            UpdatePanelTexts();
            return;
        }

        statusMessage = $"Seed '{selectedSeed.name}' plantada en nodo '{randomNode.name}' (turnos: {selectedSeed.Ticks}).";
        UpdatePanelTexts();

        if (showDebugLogs)
        {
            Debug.Log($"DistrictSeedPlantPanel: planted '{selectedSeed.name}' in district {selectedDistrict}, node '{randomNode.name}', completion in {selectedSeed.Ticks} turn(s).", this);
        }

        ClosePanel();
    }

    private void WireButtons()
    {
        if (plantButton != null) plantButton.onClick.AddListener(TryPlantSelectedSeed);
        if (closeButton != null) closeButton.onClick.AddListener(ClosePanel);
    }

    private void UnwireButtons()
    {
        if (plantButton != null) plantButton.onClick.RemoveListener(TryPlantSelectedSeed);
        if (closeButton != null) closeButton.onClick.RemoveListener(ClosePanel);
    }

    private void SetPanelVisible(bool visible)
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(visible);
        }
    }

    private void ClosePanel()
    {
        isOpen = false;
        SetPanelVisible(false);
    }

    private void UpdatePanelTexts()
    {
        if (districtLabel != null)
        {
            districtLabel.text = $"District: {selectedDistrict}";
        }

        if (statusLabel != null)
        {
            statusLabel.text = statusMessage;
        }
    }

    private void BuildSeedButtons()
    {
        if (seedListContent == null) return;

        ClearSeedButtons();

        Button templateButton = GetTemplateButton();
        if (templateButton == null)
        {
            statusMessage = "No hay prefab/template de botón para seeds.";
            UpdatePanelTexts();
            return;
        }

        templateButton.gameObject.SetActive(false);

        for (int i = 0; i < cachedSeeds.Count; i++)
        {
            int indexCopy = i;
            Seed seed = cachedSeeds[i];

            Button instance = Instantiate(templateButton, seedListContent);
            instance.gameObject.SetActive(true);
            ConfigureSeedButton(instance, seed, false);
            instance.onClick.AddListener(() => SelectSeed(indexCopy));

            runtimeSeedButtons.Add(instance);
        }

        UpdateSelectionVisuals();
    }

    private Button GetTemplateButton()
    {
        if (seedItemButtonPrefab != null) return seedItemButtonPrefab;
        if (seedListContent == null) return null;

        Button existing = seedListContent.GetComponentInChildren<Button>(true);
        return existing;
    }

    private void ClearSeedButtons()
    {
        for (int i = 0; i < runtimeSeedButtons.Count; i++)
        {
            if (runtimeSeedButtons[i] == null) continue;
            Destroy(runtimeSeedButtons[i].gameObject);
        }

        runtimeSeedButtons.Clear();
    }

    private void SelectSeed(int index)
    {
        selectedSeedIndex = index;
        statusMessage = string.Empty;
        UpdatePanelTexts();
        UpdateSelectionVisuals();
    }

    private void UpdateSelectionVisuals()
    {
        for (int i = 0; i < runtimeSeedButtons.Count; i++)
        {
            if (runtimeSeedButtons[i] == null || i >= cachedSeeds.Count) continue;
            bool isSelected = i == selectedSeedIndex;
            ConfigureSeedButton(runtimeSeedButtons[i], cachedSeeds[i], isSelected);
        }
    }

    private static void ConfigureSeedButton(Button button, Seed seed, bool selected)
    {
        if (button == null || seed == null) return;

        SeedButtonView customView = button.GetComponent<SeedButtonView>();
        if (customView != null)
        {
            customView.Bind(seed, selected);
            return;
        }

        string finalLabel = selected ? $"> {seed.Title}" : seed.Title;

        TMP_Text tmpText = button.GetComponentInChildren<TMP_Text>(true);
        if (tmpText != null)
        {
            tmpText.text = finalLabel;
            return;
        }

        Text uiText = button.GetComponentInChildren<Text>(true);
        if (uiText != null)
        {
            uiText.text = finalLabel;
        }
    }
}
