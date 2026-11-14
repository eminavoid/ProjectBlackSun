using UnityEngine;
using Zeke.Tooltips;

namespace Zeke.Abilities
{
    [RequireComponent(typeof(HoverLinkHandlerTMPro))]
    public class LinkTooltipTrigger : MonoBehaviour
    {
        [SerializeField] private TooltipLinksDB tooltipLinksDB;

        private ILinkHoverEvents linkHoverEvents;
        private bool showing;

        public TooltipLinksDB Database
        {
            get => tooltipLinksDB;
            set => tooltipLinksDB = value;
        }

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
            if (showing && !Tooltip.IsNull) Tooltip.Hide();
            showing = false;
        }

        private void OnHoverEnterLink(LinkHoverEventInfo linkHoverEventInfo)
        {
            if (tooltipLinksDB == null || !tooltipLinksDB.TryGetValue(linkHoverEventInfo.id, out TooltipLinksDB.TooltipInfo toolTipInfo)) return;

            Tooltip.Show(string.IsNullOrEmpty(toolTipInfo.title)
                ? toolTipInfo.description
                : $"{toolTipInfo.title}\n<size=80%>{toolTipInfo.description}</size>");
            showing = true;
        }

        private void OnHoverExitLink(LinkHoverEventInfo linkHoverEventInfo)
        {
            showing = false;
            Tooltip.Hide();
        }
    }
}
