using UnityEngine;

namespace Zeke.Tooltips
{
    public readonly struct LinkHoverEventInfo
    {
        public readonly string id;
        public readonly Vector2 mouse;

        public LinkHoverEventInfo(string id, Vector2 mouse)
        {
            this.id = id;
            this.mouse = mouse;
        }
    }
}