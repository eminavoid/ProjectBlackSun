using System.Collections.Generic;
using UnityEngine;

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

    public void FlyToSidebar()
    {
        if (ResourceManager.Sidebar != null)
        {
            ResourceManager.Sidebar.Receive(this, row.TakeChips());
        }
    }
}
