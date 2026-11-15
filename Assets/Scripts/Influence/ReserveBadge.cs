using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zeke.Tooltips;
using Zeke.UI;

[RequireComponent(typeof(RectTransform))]
public class ReserveBadge : MonoBehaviour
{
    [SerializeField] private PopUpManager.Direction direction = PopUpManager.Direction.upperRight;
    [SerializeField] private float distance = -20f;

    private UIWindow badge;
    private TooltipTrigger tooltip;
    private string baseTooltip;
    private int shown = -1;

    private void Start()
    {
        tooltip = GetComponent<TooltipTrigger>();
        if (tooltip != null) baseTooltip = tooltip.Text;
    }

    private void Update()
    {
        if (InfluenceManager.IsNull) return;
        int reserve = InfluenceManager.Get.GetClericPool(FactionId.Player);
        if (reserve != shown) Show(reserve);
    }

    private void Show(int reserve)
    {
        bool grew = shown >= 0 && reserve > shown;
        shown = reserve;

        if (tooltip != null)
        {
            string line = reserve == 1 ? "1 cleric in reserve, ready to send." : $"{reserve} clerics in reserve, ready to send.";
            tooltip.SetText($"{baseTooltip}\n<size=80%><b>{line}</b></size>");
        }

        if (reserve <= 0)
        {
            PopUpManager.RemovePopUp(badge);
            badge = null;
            return;
        }

        bool spawned = badge == null;
        if (spawned)
        {
            badge = PopUpManager.SpawnPopUp((RectTransform)transform, direction, distance, true);
            if (badge == null) return;
            if (badge.TryGetComponent(out Button badgeButton) && TryGetComponent(out Button opener)) badgeButton.onClick.AddListener(opener.onClick.Invoke);
        }

        badge.TryGetElement<TextMeshProUGUI>("Amount").SetText(reserve.ToString());
        if (!spawned && grew) PopUpManager.BumpPopUp(badge);
    }
}
