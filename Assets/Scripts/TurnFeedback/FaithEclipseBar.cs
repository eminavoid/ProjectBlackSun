using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using Zeke.Tooltips;

public class FaithEclipseBar : MonoBehaviour
{
    [SerializeField] private TMP_Text turnText;
    [SerializeField] private TooltipTrigger turnTooltip;
    [Tooltip("Inactive template, cloned once per faith in play.")]
    [SerializeField] private FaithEclipseChip chipTemplate;

    [Header("Eclipse Countdown")]
    [Tooltip("From this many turns left, the turn counter warns that the eclipse is near.")]
    [SerializeField, Min(0)] private int warnTurns = 5;
    [SerializeField] private Color warnColor = new Color(0.62f, 0.12f, 0.08f, 1f);

    private readonly List<FaithEclipseChip> chips = new List<FaithEclipseChip>();
    private InfluenceManager influence;
    private GameLoopEvents gameLoop;
    private Color turnColor;
    private int shownTurn;

    private void Start()
    {
        gameLoop = FindAnyObjectByType<GameLoopEvents>();
        turnColor = turnText.color;

        chipTemplate.gameObject.SetActive(false);
        foreach (FactionId faction in FactionIdUtil.All)
        {
            if (!FactionIdUtil.IsPlayer(faction) && !RivalAIDirector.IsActiveRival(faction)) continue;

            FaithEclipseChip chip = Instantiate(chipTemplate, chipTemplate.transform.parent);
            chip.name = $"Chip ({FactionIdUtil.DisplayName(faction)})";
            chip.gameObject.SetActive(true);
            chip.Bind(faction);
            chips.Add(chip);
        }

        if (!InfluenceManager.IsNull)
        {
            influence = InfluenceManager.Get;
            influence.OnControlChanged += Refresh;
        }

        GameTime.OnTurnResolved += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        GameTime.OnTurnResolved -= Refresh;
        if (influence != null) influence.OnControlChanged -= Refresh;
    }

    private void Refresh()
    {
        int turn = GameTime.CurrentTurn;
        RefreshTurn(turn);
        if (shownTurn != 0 && shownTurn != turn)
        {
            turnText.transform.DOKill(true);
            turnText.transform.DOPunchScale(Vector3.one * 0.3f, 0.5f, 6, 0.6f).SetLink(gameObject);
        }
        shownTurn = turn;

        if (influence == null) return;

        FactionId? leader = influence.GetFaithEclipseLeader(out _);
        for (int i = 0; i < chips.Count; i++)
        {
            FactionId faction = chips[i].Faction;
            chips[i].Show(influence.CountControlledZones(faction), leader == faction);
        }
    }

    private void RefreshTurn(int turn)
    {
        if (gameLoop == null)
        {
            turnText.text = $"TURN {turn}";
            return;
        }

        int eclipse = gameLoop.EclipseTurn;
        int left = gameLoop.TurnsUntilEclipse;
        turnText.text = $"TURN {Mathf.Min(turn, eclipse)}/{eclipse}";
        turnText.color = left <= warnTurns ? warnColor : turnColor;

        if (turnTooltip == null) return;
        string countdown = left <= 1
            ? "The Faith Eclipse comes when this turn ends."
            : $"The Faith Eclipse comes in {left} turns.";
        turnTooltip.SetText($"TURN\n{countdown} The faith controlling the most zones then rules Beheld.\n\nEach time you end the turn, your districts pay their tithes, planted seeds ripen into events and rival faiths make their moves.");
    }

    public void RefreshNames()
    {
        for (int i = 0; i < chips.Count; i++) chips[i].RefreshName();
    }
}
