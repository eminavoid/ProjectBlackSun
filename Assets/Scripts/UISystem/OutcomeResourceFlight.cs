using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The result window's resource chips fly to the resources bar when the player continues.
/// Until then the bar keeps its old values, so the change lands with the chips.
/// </summary>
[RequireComponent(typeof(ResourceAmountRow))]
public class OutcomeResourceFlight : MonoBehaviour
{
    private ResourceAmountRow row;

    private void Awake()
    {
        row = GetComponent<ResourceAmountRow>();
        row.Shown += Hold;
    }

    private void OnDestroy()
    {
        if (row != null)
        {
            row.Shown -= Hold;
        }
    }

    private void Hold(IReadOnlyList<ResourceDelta> deltas)
    {
        if (ResourceManager.Sidebar != null)
        {
            ResourceManager.Sidebar.Hold(this, deltas);
        }
    }

    /// <summary>Hooked to the window's continue button, next to closing the window.</summary>
    public void FlyToSidebar()
    {
        if (ResourceManager.Sidebar != null)
        {
            ResourceManager.Sidebar.Receive(this, row.TakeChips());
        }
    }
}
