using UnityEngine;
using UnityEngine.EventSystems;

namespace Zeke.UI
{
    public class DraggableWindow : MonoBehaviour, IDragHandler
    {
        [SerializeField] private RectTransform grabBounds;

        public void OnDrag(PointerEventData eventData)
        {
            grabBounds.anchoredPosition += eventData.delta / GlobalReferences.ScreenCanvas.scaleFactor;
        }
    }
}