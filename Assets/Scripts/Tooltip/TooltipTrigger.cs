using UnityEngine;
using UnityEngine.EventSystems;

namespace Zeke.Tooltips
{
    public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [TextArea]
        [SerializeField] private string tooltipText;

        public string Text => tooltipText;

        public void SetText(string text) => tooltipText = text;

        public void OnPointerEnter(PointerEventData eventData)
        {
            Tooltip.Show(tooltipText);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Tooltip.Hide();
        }
    }
}