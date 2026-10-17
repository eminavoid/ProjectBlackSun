using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class FaithEclipseBar : MonoBehaviour
{
    [SerializeField] private TMP_Text turnText;
    [Tooltip("Inactive template, cloned once per faith in play.")]
    [SerializeField] private FaithEclipseChip chipTemplate;

    private readonly List<FaithEclipseChip> chips = new List<FaithEclipseChip>();
    private InfluenceManager influence;
    private int shownTurn;

    private void Start()
    {
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
        turnText.text = $"TURN {turn}";
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
}
