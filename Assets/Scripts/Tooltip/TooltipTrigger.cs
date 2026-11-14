using UnityEngine;
using UnityEngine.EventSystems;

namespace Zeke.Tooltips
{
    public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [TextArea]
        [SerializeField] private string tooltipText;

        private bool showing;

        public string Text => tooltipText;

        public void SetText(string text) => tooltipText = text;

        public void OnPointerEnter(PointerEventData eventData)
        {
            Tooltip.Show(tooltipText);
            showing = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            showing = false;
            Tooltip.Hide();
        }

        private void OnDisable()
        {
            if (showing && !Tooltip.IsNull) Tooltip.Hide();
            showing = false;
        }
    }
}
