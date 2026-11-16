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
            if (linkHoverEvents == null) linkHoverEvents = GetComponent<ILinkHoverEvents>();
            if (linkHoverEvents == null) return;
            linkHoverEvents.OnHoverEnterLink += OnHoverEnterLink;
            linkHoverEvents.OnHoverExitLink += OnHoverExitLink;
        }

        private void OnDisable()
        {
            if (linkHoverEvents != null)
            {
                linkHoverEvents.OnHoverEnterLink -= OnHoverEnterLink;
                linkHoverEvents.OnHoverExitLink -= OnHoverExitLink;
            }

            if (showing && !Tooltip.IsNull) Tooltip.Hide();
            showing = false;
        }

        private void OnHoverEnterLink(LinkHoverEventInfo linkHoverEventInfo)
        {
            string text = tooltipLinksDB != null ? tooltipLinksDB.Describe(linkHoverEventInfo.id) : null;
            if (text == null) return;

            Tooltip.Show(text);
            showing = true;
        }

        private void OnHoverExitLink(LinkHoverEventInfo linkHoverEventInfo)
        {
            showing = false;
            Tooltip.Hide();
        }
    }
}
