using UnityEngine;
using UnityEngine.EventSystems;

namespace Zeke.UI
{
    public class DraggableWindow : MonoBehaviour, IDragHandler
    {
        [SerializeField] private RectTransform grabBounds;

        public void SetTarget(RectTransform target)
        {
            grabBounds = target;
        }

        public void OnDrag(PointerEventData eventData)
        {
            RectTransform target = grabBounds != null ? grabBounds : transform as RectTransform;
            if (target == null) return;
            if (GlobalReferences.IsNull || GlobalReferences.ScreenCanvas == null) return;

            target.anchoredPosition += eventData.delta / GlobalReferences.ScreenCanvas.scaleFactor;
        }
    }
}