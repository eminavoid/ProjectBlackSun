using UnityEngine;

namespace Zeke.Tooltips
{
    public class WorldTooltipTrigger : MonoBehaviour
    {
        [TextArea]
        [SerializeField] private string tooltipText;

        private void OnMouseEnter()
        {
            Tooltip.Show(tooltipText);
        }

        private void OnMouseExit()
        {
            Tooltip.Hide();
        }
    }
}