using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MapStatsPanel : MonoBehaviour
{
    public static MapStatsPanel Instance { get; private set; }

    [SerializeField] private GameObject windowPrefab;
    [SerializeField] private Button openMenuButton;
    [SerializeField] private string optionsRectName = "OptionsRect";
    [SerializeField] private string optionsOpenButtonName = "Button (1)";

    private MapStatsWindowView view;
    private readonly List<GameObject> spawned = new List<GameObject>();
    private readonly List<Button> tabButtons = new List<Button>();
    private readonly List<Districts?> tabScopes = new List<Districts?>();

    private bool isOpen;
    private Districts? selectedDistrict;
    private DistrictZone focusedZone;
    private bool gameTimeBound;
    private bool influenceBound;
    private bool intentsBound;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        UnwireHudButton();
        UnbindEvents();
    }

    private void Start()
    {
        ResolveOpenMenuButton();
        WireHudButton();
        BindEvents();
        EnsureView();
    }

    private void OnEnable()
    {
        BindEvents();
    }

    private void OnDisable()
    {
        UnbindEvents();
    }

    public void OpenGlobal()
    {
        selectedDistrict = null;
        focusedZone = null;
        ShowWindow();
        Refresh();
    }

    public void OpenDistrict(Districts district)
    {
        selectedDistrict = district;
        focusedZone = null;
        ShowWindow();
        Refresh();
    }

    public void OpenNode(DistrictZone zone)
    {
        if (zone == null || !zone.IsPlayable)
        {
            OpenGlobal();
            return;
        }

        selectedDistrict = zone.District;
        focusedZone = zone;
        ShowWindow();
        Refresh();
    }

    public void Close()
    {
        isOpen = false;
        if (view != null && view.Root != null) view.Root.SetActive(false);
    }

    public bool IsOpen => isOpen;

    private void ShowWindow()
    {
        EnsureView();
        if (view == null || view.Root == null) return;

        isOpen = true;
        view.Root.SetActive(true);
        view.Root.transform.SetAsLastSibling();

        MapContextMenu contextMenu = FindAnyObjectByType<MapContextMenu>();
        if (contextMenu != null) contextMenu.Close();
    }

    private void RefreshIfOpen()
    {
        if (this == null || !isOpen) return;
        Refresh();
    }

    private void Refresh()
    {
        EnsureView();
        if (view == null || view.Content == null) return;

        if (focusedZone == null) focusedZone = null;

        EnsureTabs();
        UpdateTabVisuals();
        ClearSpawned();

        if (view.Title != null)
        {
            view.Title.text = BuildTitle();
        }

        if (focusedZone != null)
        {
            RenderNode(MapStatsQuery.CaptureNode(focusedZone));
            Button back = MapStatsUiBuilder.SpawnActionButton(view, "Volver al distrito");
            if (back != null)
            {
                Districts district = focusedZone.District;
                back.onClick.AddListener(() => OpenDistrict(district));
                Track(back.gameObject);
            }
        }

        if (!selectedDistrict.HasValue)
        {
            RenderGlobal(MapStatsQuery.CaptureGlobal());
        }
        else
        {
            RenderDistrict(MapStatsQuery.CaptureDistrict(selectedDistrict.Value));
        }

        ResetScrollToTop();
        Canvas.ForceUpdateCanvases();
        if (view.Content != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(view.Content);
        }
    }

    private string BuildTitle()
    {
        if (focusedZone != null)
        {
            return "Estadísticas — " + MapStatsQuery.DistrictLabel(focusedZone.District) + " / " + focusedZone.SectorName;
        }

        if (selectedDistrict.HasValue)
        {
            return "Estadísticas — " + MapStatsQuery.DistrictLabel(selectedDistrict.Value);
        }

        return "Estadísticas — Ciudad";
    }

    private void RenderGlobal(MapStatsGlobalSnapshot snapshot)
    {
        Track(MapStatsUiBuilder.SpawnSection(view, "Faith Eclipse"));
        Track(MapStatsUiBuilder.SpawnBody(view, snapshot.FaithEclipseLeaderLine));
        Track(MapStatsUiBuilder.SpawnBody(view,
            "Zonas: " + snapshot.PlayableZones
            + "  ·  controladas " + snapshot.ControlledZones
            + "  ·  en disputa " + snapshot.ContestedZones
            + "  ·  seeds " + snapshot.OccupiedZones));

        Track(MapStatsUiBuilder.SpawnSection(view, "Control por facción"));
        for (int i = 0; i < snapshot.Factions.Count; i++)
        {
            MapStatsFactionRow row = snapshot.Factions[i];
            string value = row.ControlledZones + " zonas  ·  pool " + row.PoolClerics + "  ·  asignados " + row.Clerics;
            Track(MapStatsUiBuilder.SpawnFactionRow(view, row, value));
        }

        Track(MapStatsUiBuilder.SpawnSection(view, "Distritos"));
        for (int i = 0; i < snapshot.Districts.Count; i++)
        {
            MapStatsDistrictRow row = snapshot.Districts[i];
            Button button = MapStatsUiBuilder.SpawnDistrictRow(view, row);
            Districts district = row.District;
            if (button != null) button.onClick.AddListener(() => OpenDistrict(district));
            Track(button != null ? button.gameObject : null);
        }

        Track(MapStatsUiBuilder.SpawnSection(view, "Seeds activas"));
        if (snapshot.Seeds.Count == 0)
        {
            Track(MapStatsUiBuilder.SpawnBody(view, "Ninguna seed plantada."));
            return;
        }

        for (int i = 0; i < snapshot.Seeds.Count; i++)
        {
            Button seedButton = MapStatsUiBuilder.SpawnSeedRow(view, snapshot.Seeds[i]);
            DistrictZone zone = snapshot.Seeds[i].Zone;
            if (seedButton != null) seedButton.onClick.AddListener(() => OpenNode(zone));
            Track(seedButton != null ? seedButton.gameObject : null);
        }
    }

    private void RenderDistrict(MapStatsDistrictSnapshot snapshot)
    {
        Track(MapStatsUiBuilder.SpawnSection(view, snapshot.DisplayName));
        Track(MapStatsUiBuilder.SpawnBody(view, snapshot.ControlLabel));
        Track(MapStatsUiBuilder.SpawnBody(view, snapshot.ProductionLine));
        Track(MapStatsUiBuilder.SpawnBody(view,
            "Nodos: " + snapshot.ZoneCount
            + "  ·  controlados " + snapshot.ControlledCount
            + "  ·  seeds " + snapshot.OccupiedCount
            + (snapshot.SpecialEventsUnlocked ? "  ·  eventos especiales" : "")));

        Track(MapStatsUiBuilder.SpawnSection(view, "Breakdown de control"));
        for (int i = 0; i < snapshot.Factions.Count; i++)
        {
            MapStatsFactionRow row = snapshot.Factions[i];
            string value = row.ControlledZones + " zonas  ·  inf "
                + row.Influence + " (" + Mathf.RoundToInt(row.SharePercent) + "%)  ·  "
                + row.Clerics + " clérigos";
            Track(MapStatsUiBuilder.SpawnFactionRow(view, row, value));
        }

        Track(MapStatsUiBuilder.SpawnSection(view, "Seeds en el distrito"));
        if (snapshot.Seeds.Count == 0)
        {
            Track(MapStatsUiBuilder.SpawnBody(view, "Ninguna seed plantada."));
        }
        else
        {
            for (int i = 0; i < snapshot.Seeds.Count; i++)
            {
                Button seedButton = MapStatsUiBuilder.SpawnSeedRow(view, snapshot.Seeds[i]);
                DistrictZone zone = snapshot.Seeds[i].Zone;
                if (seedButton != null) seedButton.onClick.AddListener(() => OpenNode(zone));
                Track(seedButton != null ? seedButton.gameObject : null);
            }
        }

        Track(MapStatsUiBuilder.SpawnSection(view, "Nodos"));
        for (int i = 0; i < snapshot.Nodes.Count; i++)
        {
            MapStatsNodeRow row = snapshot.Nodes[i];
            Button button = MapStatsUiBuilder.SpawnNodeRow(view, row);
            DistrictZone zone = row.Zone;
            if (button != null) button.onClick.AddListener(() => OpenNode(zone));
            Track(button != null ? button.gameObject : null);
        }
    }

    private void RenderNode(MapStatsNodeSnapshot snapshot)
    {
        Track(MapStatsUiBuilder.SpawnSection(view, snapshot.SectorName));
        Track(MapStatsUiBuilder.SpawnBody(view, snapshot.DistrictName + "  ·  " + snapshot.ControlLabel));
        Track(MapStatsUiBuilder.SpawnBody(view, "Influencia: " + snapshot.TotalInfluence + "/" + snapshot.Cap));
        Track(MapStatsUiBuilder.SpawnBody(view, snapshot.IntentLine));
        Track(MapStatsUiBuilder.SpawnBody(view, snapshot.TitheLine));

        Track(MapStatsUiBuilder.SpawnSection(view, "Presencia"));
        if (snapshot.Factions.Count == 0)
        {
            Track(MapStatsUiBuilder.SpawnBody(view, "Sin presencia de facciones."));
        }
        else
        {
            for (int i = 0; i < snapshot.Factions.Count; i++)
            {
                MapStatsFactionRow row = snapshot.Factions[i];
                string expelled = row.ExpelledAtCap ? "  ·  expulsada (cap)" : string.Empty;
                string value = row.Influence + " (" + Mathf.RoundToInt(row.SharePercent) + "%)  ·  "
                    + row.Clerics + " clérigos" + expelled;
                Track(MapStatsUiBuilder.SpawnFactionRow(view, row, value));
            }
        }

        Track(MapStatsUiBuilder.SpawnSection(view, "Seed"));
        if (snapshot.PlantedSeed == null)
        {
            Track(MapStatsUiBuilder.SpawnBody(view, "Libre — no hay seed corriendo."));
        }
        else
        {
            Button seedButton = MapStatsUiBuilder.SpawnSeedRow(view, snapshot.PlantedSeed);
            Track(seedButton != null ? seedButton.gameObject : null);
        }
    }

    private void EnsureTabs()
    {
        if (view == null || view.TabsRoot == null) return;
        if (tabButtons.Count > 0) return;

        AddTab("Ciudad", null);

        foreach (Districts district in Enum.GetValues(typeof(Districts)))
        {
            AddTab(MapStatsQuery.DistrictLabel(district), district);
        }
    }

    private void AddTab(string label, Districts? district)
    {
        Button button = MapStatsUiBuilder.CreateTabButton(view.TabsRoot, label);
        Districts? scope = district;
        button.onClick.AddListener(() =>
        {
            focusedZone = null;
            selectedDistrict = scope;
            Refresh();
        });
        tabButtons.Add(button);
        tabScopes.Add(scope);
    }

    private void UpdateTabVisuals()
    {
        for (int i = 0; i < tabButtons.Count; i++)
        {
            bool selected = selectedDistrict.HasValue
                ? tabScopes[i].HasValue && tabScopes[i].Value == selectedDistrict.Value
                : !tabScopes[i].HasValue;
            MapStatsUiBuilder.SetTabSelected(tabButtons[i], selected);
        }
    }

    private void Track(GameObject go)
    {
        if (go != null) spawned.Add(go);
    }

    private void ClearSpawned()
    {
        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] != null) Destroy(spawned[i]);
        }

        spawned.Clear();
    }

    private void ResetScrollToTop()
    {
        if (view == null || view.Scroll == null) return;
        view.Scroll.StopMovement();
        view.Scroll.verticalNormalizedPosition = 1f;
    }

    private void EnsureView()
    {
        if (view != null && view.Root != null) return;

        Transform canvas = ResolveCanvas();
        if (canvas == null)
        {
            Debug.LogWarning("MapStatsPanel: no hay ScreenCanvas.", this);
            return;
        }

        if (windowPrefab != null)
        {
            GameObject instance = Instantiate(windowPrefab, canvas);
            instance.name = "MapStatsWindow";
            view = MapStatsUiBuilder.BindWindow(instance);
        }
        else
        {
            view = MapStatsUiBuilder.BuildWindow(canvas);
        }

        if (view != null && view.CloseButton != null)
        {
            view.CloseButton.onClick.AddListener(Close);
        }

        if (view != null && view.Root != null) view.Root.SetActive(false);
    }

    private static Transform ResolveCanvas()
    {
        if (!GlobalReferences.IsNull && GlobalReferences.ScreenCanvas != null)
        {
            return GlobalReferences.ScreenCanvas.transform;
        }

        Canvas canvas = FindAnyObjectByType<Canvas>();
        return canvas != null ? canvas.transform : null;
    }

    private void ResolveOpenMenuButton()
    {
        if (openMenuButton != null) return;

        GameObject optionsRect = GameObject.Find(optionsRectName);
        if (optionsRect == null)
        {
            Debug.LogWarning($"MapStatsPanel: no se encontró '{optionsRectName}'.", this);
            return;
        }

        Transform buttonTransform = optionsRect.transform.Find(optionsOpenButtonName);
        if (buttonTransform == null)
        {
            Debug.LogWarning($"MapStatsPanel: no se encontró '{optionsOpenButtonName}' bajo '{optionsRectName}'.", this);
            return;
        }

        openMenuButton = buttonTransform.GetComponent<Button>();
        if (openMenuButton == null)
        {
            Debug.LogWarning($"MapStatsPanel: '{optionsOpenButtonName}' no tiene componente Button.", this);
            return;
        }

        TMP_Text label = openMenuButton.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = "Stats";
    }

    private void WireHudButton()
    {
        if (openMenuButton != null) openMenuButton.onClick.AddListener(OpenGlobal);
    }

    private void UnwireHudButton()
    {
        if (openMenuButton != null) openMenuButton.onClick.RemoveListener(OpenGlobal);
    }

    private void BindEvents()
    {
        if (!gameTimeBound)
        {
            GameTime.OnTurnEnded += RefreshIfOpen;
            gameTimeBound = true;
        }

        if (!influenceBound && !InfluenceManager.IsNull)
        {
            InfluenceManager.Get.OnControlChanged += RefreshIfOpen;
            influenceBound = true;
        }

        if (!intentsBound && !AIIntentBoard.IsNull)
        {
            AIIntentBoard.Get.OnIntentsChanged += RefreshIfOpen;
            intentsBound = true;
        }
    }

    private void UnbindEvents()
    {
        if (gameTimeBound)
        {
            GameTime.OnTurnEnded -= RefreshIfOpen;
            gameTimeBound = false;
        }

        if (influenceBound && !InfluenceManager.IsNull)
        {
            InfluenceManager.Get.OnControlChanged -= RefreshIfOpen;
            influenceBound = false;
        }

        if (intentsBound && !AIIntentBoard.IsNull)
        {
            AIIntentBoard.Get.OnIntentsChanged -= RefreshIfOpen;
            intentsBound = false;
        }
    }
}
