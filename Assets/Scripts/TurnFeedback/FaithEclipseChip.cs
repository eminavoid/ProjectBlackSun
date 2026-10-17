using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zeke.Tooltips;

public class FaithEclipseChip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image swatch;
    [Tooltip("Only shown for the player's own faith.")]
    [SerializeField] private TMP_Text label;
    [SerializeField] private TMP_Text count;
    [SerializeField] private GameObject leaderMark;
    [SerializeField] private Color leaderCountColor = new Color(0.62f, 0.36f, 0.04f, 1f);

    private Color countColor;
    private int zones = -1;
    private bool leads;

    public FactionId Faction { get; private set; }

    public void Bind(FactionId faction)
    {
        Faction = faction;
        countColor = count.color;
        swatch.color = FactionPalette.For(faction);
        label.gameObject.SetActive(FactionIdUtil.IsPlayer(faction));
        leaderMark.SetActive(false);
    }

    public void Show(int controlledZones, bool isLeader)
    {
        bool changed = zones >= 0 && controlledZones != zones;
        zones = controlledZones;
        leads = isLeader;

        count.text = controlledZones.ToString();
        count.color = isLeader ? leaderCountColor : countColor;
        leaderMark.SetActive(isLeader);

        if (!changed) return;
        count.transform.DOKill(true);
        count.transform.DOPunchScale(Vector3.one * 0.45f, 0.45f, 6, 0.6f).SetLink(gameObject);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        string title = FactionIdUtil.IsPlayer(Faction) ? "YOUR FAITH" : FactionIdUtil.DisplayName(Faction).ToUpperInvariant();
        string zonesText = zones == 1 ? "1 zone" : $"{Mathf.Max(0, zones)} zones";
        string text = $"{title}\nControls {zonesText}.";
        if (leads) text += "\nControls the most zones in the city.";
        Tooltip.Show(text);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Tooltip.Hide();
    }
}
