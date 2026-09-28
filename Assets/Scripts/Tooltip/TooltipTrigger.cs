using UnityEngine;
using UnityEngine.EventSystems;

public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [TextArea]
    [SerializeField] private string tooltipText;

    public void OnPointerEnter(PointerEventData eventData)
    {
        Tooltip.Show(tooltipText);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Tooltip.Hide();
    }
}