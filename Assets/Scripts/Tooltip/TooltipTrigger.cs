using UnityEngine;
using UnityEngine.EventSystems;

namespace Zeke.Tooltips
{
    public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [TextArea]
        [SerializeField] private string tooltipText;
        [SerializeField] private bool showBesides = true;

        private bool showing;

        public string Text => tooltipText;

        public bool ShowBesides
        {
            get
            {
                return showBesides;
            }
            set
            {
                showBesides = value;
            }
        }

        public void SetText(string text) => tooltipText = text;

        public void OnPointerEnter(PointerEventData eventData)
        {
            TooltipTrigger innermost = InnermostUnder(eventData);
            if (innermost != null && innermost != this) return;
            Show();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            showing = false;
            Tooltip.Hide();

            TooltipTrigger under = InnermostUnder(eventData);
            if (under != null && under != this && under.isActiveAndEnabled && transform.IsChildOf(under.transform)) under.Show();
        }

        private void Show()
        {
            Tooltip.Show(tooltipText, showBesides ? (RectTransform)transform : null);
            showing = true;
        }

        private static TooltipTrigger InnermostUnder(PointerEventData eventData)
        {
            GameObject target = eventData.pointerCurrentRaycast.gameObject;
            return target != null ? target.GetComponentInParent<TooltipTrigger>() : null;
        }

        private void OnDisable()
        {
            if (showing && !Tooltip.IsNull) Tooltip.Hide();
            showing = false;
        }
    }
}
