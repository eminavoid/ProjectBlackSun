using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zeke.Tooltips;
using Zeke.UI;

[RequireComponent(typeof(RectTransform))]
public class TheologyBadge : MonoBehaviour
{
    [SerializeField] private PopUpManager.Direction direction = PopUpManager.Direction.upperRight;
    [SerializeField] private float distance = -20f;

    private UIWindow badge;
    private TooltipTrigger tooltip;
    private string baseTooltip;
    private TheologyMenu theology;
    private bool shown;

    private void Start()
    {
        tooltip = GetComponent<TooltipTrigger>();
        if (tooltip != null) baseTooltip = tooltip.Text;
        theology = FindAnyObjectByType<TheologyMenu>(FindObjectsInactive.Include);
    }

    private void Update()
    {
        if (theology == null || ResourceManager.IsNull || ResourceManager.Resources == null) return;

        bool ready = !theology.gameObject.activeInHierarchy
            && ResourceManager.Resources.GetResourceAmount(Resource.Zeal) >= theology.NextCost;
        if (ready == shown) return;
        shown = ready;

        if (tooltip != null)
        {
            tooltip.SetText(ready
                ? $"{baseTooltip}\n<size=80%><b>You have enough Zeal to commune with the Beholder.</b></size>"
                : baseTooltip);
        }

        if (!ready)
        {
            PopUpManager.RemovePopUp(badge);
            badge = null;
            return;
        }

        badge = PopUpManager.SpawnPopUp((RectTransform)transform, direction, distance, true);
        if (badge == null) return;
        if (badge.TryGetComponent(out Button badgeButton) && TryGetComponent(out Button opener)) badgeButton.onClick.AddListener(opener.onClick.Invoke);
        badge.TryGetElement<TextMeshProUGUI>("Amount").SetText("!");
        PopUpManager.BumpPopUp(badge);
    }
}
