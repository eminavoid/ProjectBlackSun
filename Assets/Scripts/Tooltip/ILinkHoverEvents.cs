using System;

namespace Zeke.Tooltips
{
	public interface ILinkHoverEvents
    {
        public Action<LinkHoverEventInfo> OnHoverEnterLink { get; set; }
        public Action<LinkHoverEventInfo> OnHoverStayLink { get; set; }
        public Action<LinkHoverEventInfo> OnHoverExitLink { get; set; }
    }
}