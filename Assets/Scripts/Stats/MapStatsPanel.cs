using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class MapStatsPanel : MonoBehaviour
{
    public static MapStatsPanel Instance { get; private set; }

    [SerializeField] private MapStatsBook book;

    private static readonly Color Ink = new Color(0.2f, 0.14f, 0.09f);

    private Districts? selectedDistrict;
    private DistrictZone focusedZone;
    private DistrictSelectionController selection;

    public bool IsOpen => book.IsOpen;

    private void Awake()
    {
        Instance = this;
        book.PageRequested += district => Show(district, null);
    }

    private void OnEnable()
    {
        MapCameraController.OnMapContextRequested += OnMapContextRequested;
        GameTime.OnTurnEnded += RefreshIfOpen;
    }

    private void OnDisable()
    {
        MapCameraController.OnMapContextRequested -= OnMapContextRequested;
        GameTime.OnTurnEnded -= RefreshIfOpen;
    }

    private void Start()
    {
        selection = FindAnyObjectByType<DistrictSelectionController>();
        if (!InfluenceManager.IsNull) InfluenceManager.Get.OnControlChanged += RefreshIfOpen;
        if (!AIIntentBoard.IsNull) AIIntentBoard.Get.OnIntentsChanged += RefreshIfOpen;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (!InfluenceManager.IsNull) InfluenceManager.Get.OnControlChanged -= RefreshIfOpen;
        if (!AIIntentBoard.IsNull) AIIntentBoard.Get.OnIntentsChanged -= RefreshIfOpen;
    }

    public void OpenGlobal() => Show(null, null);

    public void OpenDistrict(Districts district) => Show(district, null);

    public void OpenNode(DistrictZone zone)
    {
        if (zone == null || !zone.IsPlayable)
        {
            OpenGlobal();
            return;
        }

        Show(zone.District, zone);
    }

    public void Close() => book.Close();

    private void OnMapContextRequested(Vector2 screenPosition)
    {
        if (selection == null || !selection.TryPickPlayableZone(screenPosition, out DistrictZone zone) || zone == null) return;

        if (MapViewLod.IsClose(InfluenceOverlaySettings.LoadOrCreate())) OpenNode(zone);
        else OpenDistrict(zone.District);
    }

    private void Show(Districts? district, DistrictZone zone)
    {
        bool opening = !book.IsOpen;
        if (!opening && district == selectedDistrict && zone == focusedZone) return;

        selectedDistrict = district;
        focusedZone = zone;
        book.Open(district);
        Render(true, !opening);
    }

    private void RefreshIfOpen()
    {
        if (book.IsOpen) Render(false, false);
    }

    private void Render(bool animate, bool turn)
    {
        book.SelectTab(selectedDistrict);
        book.BeginPage(turn);

        if (focusedZone != null) RenderZone(MapStatsQuery.CaptureNode(focusedZone), animate);
        else if (selectedDistrict.HasValue) RenderDistrict(MapStatsQuery.CaptureDistrict(selectedDistrict.Value), animate);
        else RenderCity(MapStatsQuery.CaptureGlobal(), animate);

        book.EndPage(animate);
    }

    private void RenderCity(MapStatsGlobalSnapshot snapshot, bool animate)
    {
        book.SetHeading(string.Empty, "THE CITY",
            $"{snapshot.PlayableZones} zones  ·  {snapshot.ControlledZones} ruled  ·  {snapshot.ContestedZones} contested");

        if (snapshot.FaithEclipseLeader.HasValue) book.SetSeal(snapshot.FaithEclipseLeader, "LEADS THE ECLIPSE", FactionIdUtil.DisplayName(snapshot.FaithEclipseLeader.Value), animate);
        else book.SetSeal(null, "THE ECLIPSE", "No faith leads it yet", animate);

        book.AddSection("FAITHS");
        foreach (MapStatsFactionRow row in snapshot.Factions)
        {
            string detail = $"{Count(row.ControlledZones, "zone")} ruled  ·  {row.Clerics} in the field  ·  {row.PoolClerics} in reserve";
            if (!string.IsNullOrEmpty(row.Behavior)) detail += "\n" + row.Behavior;
            AddFaction(row, detail);
        }

        book.AddSection("DISTRICTS");
        foreach (MapStatsDistrictRow row in snapshot.Districts)
        {
            Districts district = row.District;
            book.AddEntry(book.MarkerFor(district), Color.clear, $"{row.DisplayName} district",
                $"{row.ControlLabel}  ·  {row.ControlledCount}/{row.ZoneCount} zones ruled  ·  {Count(row.OccupiedCount, "seed")}",
                () => OpenDistrict(district));
        }

        AddSeeds("ACTIVE SEEDS", snapshot.Seeds);
    }

    private void RenderDistrict(MapStatsDistrictSnapshot snapshot, bool animate)
    {
        book.SetHeading(string.Empty, $"{snapshot.DisplayName} district".ToUpperInvariant(), snapshot.ControlLabel);
        SealFor(snapshot.Controller, snapshot.ControlLabel, animate);

        book.AddText(snapshot.ProductionLine);
        book.AddText($"{Count(snapshot.ZoneCount, "zone")}  ·  {snapshot.ControlledCount} ruled  ·  {Count(snapshot.OccupiedCount, "seed")}"
            + (snapshot.SpecialEventsUnlocked ? "  ·  special events unlocked" : string.Empty));

        book.AddSection("CONTROL");
        foreach (MapStatsFactionRow row in snapshot.Factions)
        {
            AddFaction(row, $"{Count(row.ControlledZones, "zone")} ruled  ·  {row.Influence} influence ({Mathf.RoundToInt(row.SharePercent)}%)  ·  {Count(row.Clerics, "cleric")}");
        }

        AddSeeds("SEEDS", snapshot.Seeds);

        book.AddSection("ZONES");
        foreach (MapStatsNodeRow row in snapshot.Nodes)
        {
            DistrictZone zone = row.Zone;
            string detail = $"{row.ControlLabel}  ·  {row.Influence}/{row.Cap} influence";
            if (zone != null && zone.IsOccupied) detail += $"\nSeed: {row.SeedTitle}";
            book.AddEntry(null, Swatch(row.Controller), row.SectorName, detail, () => OpenNode(zone));
        }
    }

    private void RenderZone(MapStatsNodeSnapshot snapshot, bool animate)
    {
        book.SetHeading($"{snapshot.DistrictName} district".ToUpperInvariant(), snapshot.SectorName.ToUpperInvariant(), snapshot.ControlLabel);
        SealFor(snapshot.Controller, snapshot.ControlLabel, animate);

        book.AddText($"Influence {snapshot.TotalInfluence}/{snapshot.Cap}");
        book.AddText(snapshot.IntentLine);
        book.AddText(snapshot.TitheLine);

        book.AddSection("PRESENCE");
        if (snapshot.Factions.Count == 0) book.AddText("No faith has set foot here.");
        foreach (MapStatsFactionRow row in snapshot.Factions)
        {
            string detail = $"{row.Influence} influence ({Mathf.RoundToInt(row.SharePercent)}%)  ·  {Count(row.Clerics, "cleric")}";
            if (row.ExpelledAtCap) detail += "  ·  expelled";
            AddFaction(row, detail);
        }

        book.AddSection("SEED");
        if (snapshot.PlantedSeed == null) book.AddText("Free. No seed is growing here.");
        else AddSeed(snapshot.PlantedSeed, null);

        Districts district = snapshot.District;
        book.AddAction($"BACK TO {snapshot.DistrictName.ToUpperInvariant()}", () => OpenDistrict(district));
    }

    private void SealFor(FactionId? controller, string controlLabel, bool animate)
    {
        if (controller.HasValue) book.SetSeal(controller, "RULED BY", FactionIdUtil.DisplayName(controller.Value), animate);
        else book.SetSeal(null, "NO RULER", controlLabel, animate);
    }

    private void AddFaction(MapStatsFactionRow row, string detail)
    {
        Color ink = Color.Lerp(row.Color, Color.black, 0.45f);
        book.AddEntry(null, Color.Lerp(row.Color, Color.black, 0.2f),
            $"<color=#{ColorUtility.ToHtmlStringRGB(ink)}>{row.DisplayName}</color>", detail);
    }

    private void AddSeeds(string title, List<MapStatsSeedRow> seeds)
    {
        book.AddSection(title);
        if (seeds.Count == 0) book.AddText("No seeds planted.");
        foreach (MapStatsSeedRow row in seeds)
        {
            DistrictZone zone = row.Zone;
            AddSeed(row, () => OpenNode(zone));
        }
    }

    private void AddSeed(MapStatsSeedRow row, UnityAction onClick)
    {
        string turns = row.TurnsRemaining == 1 ? "1 turn left" : $"{row.TurnsRemaining} turns left";
        book.AddEntry(book.MarkerFor(row.District), Color.clear, row.Title,
            $"{row.DistrictName} district, {row.SectorName}  ·  {turns}  ·  {row.EventType} {row.Difficulty}", onClick);
    }

    private static Color Swatch(FactionId? faction)
    {
        return faction.HasValue ? Color.Lerp(FactionPalette.For(faction.Value), Color.black, 0.2f) : new Color(Ink.r, Ink.g, Ink.b, 0.25f);
    }

    private static string Count(int amount, string noun) => amount == 1 ? $"1 {noun}" : $"{amount} {noun}s";
}
