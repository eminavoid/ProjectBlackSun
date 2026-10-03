using UnityEngine;
using Zeke.Tooltips;

namespace Zeke.Abilities
{
    [RequireComponent(typeof(ILinkHoverEvents))]
    public class LinkTooltipTrigger : MonoBehaviour
    {
        [SerializeField] private TooltipLinksDB tooltipLinksDB;

        private ILinkHoverEvents linkHoverEvents;

        private void Awake()
        {
            linkHoverEvents = GetComponent<ILinkHoverEvents>();
        }

        private void OnEnable()
        {
            linkHoverEvents.OnHoverEnterLink += OnHoverEnterLink;
            linkHoverEvents.OnHoverExitLink += OnHoverExitLink;
        }

        private void OnDisable()
        {
            linkHoverEvents.OnHoverEnterLink -= OnHoverEnterLink;
            linkHoverEvents.OnHoverExitLink -= OnHoverExitLink;
        }

        private void OnHoverEnterLink(LinkHoverEventInfo linkHoverEventInfo)
        {
            if (tooltipLinksDB.TryGetValue(linkHoverEventInfo.id, out TooltipLinksDB.TooltipInfo toolTipInfo))
            {
                Tooltip.Show(toolTipInfo.description);
            }
        }

        private void OnHoverExitLink(LinkHoverEventInfo linkHoverEventInfo)
        {
            Tooltip.Hide();
        }
    }
}