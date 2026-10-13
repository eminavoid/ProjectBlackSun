using UnityEngine;
using Zeke.Tooltips;

/// <summary>
/// Mientras este objeto está activo, los tooltips de nodos del mapa no se abren.
/// </summary>
public class NodeTooltipMenuBlock : MonoBehaviour
{
    private static int openMenus;

    public static bool IsAnyMenuOpen => openMenus > 0;

    public static void Ensure(GameObject target)
    {
        if (target == null) return;
        if (target.GetComponent<NodeTooltipMenuBlock>() == null)
        {
            target.AddComponent<NodeTooltipMenuBlock>();
        }
    }

    public static void Push()
    {
        openMenus++;
        ZoneHoverTooltip.DismissOwned();
    }

    public static void Pop()
    {
        if (openMenus > 0) openMenus--;
    }

    private void OnEnable() => Push();

    private void OnDisable() => Pop();
}
