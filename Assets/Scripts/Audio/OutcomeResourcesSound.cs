using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plays the outcome's resource sound when its chips pop in.
/// Refunds only give the option's cost back, so they count as neither gain nor loss.
/// </summary>
[RequireComponent(typeof(ResourceAmountRow))]
public class OutcomeResourcesSound : MonoBehaviour
{
    private ResourceAmountRow row;

    private void Awake()
    {
        row = GetComponent<ResourceAmountRow>();
        row.ChipsAppeared += PlaySound;
    }

    private void OnDestroy()
    {
        if (row != null)
        {
            row.ChipsAppeared -= PlaySound;
        }
    }

    private void PlaySound(IReadOnlyList<ResourceDelta> deltas)
    {
        bool gained = false;
        bool lost = false;

        for (int i = 0; i < deltas.Count; i++)
        {
            if (deltas[i].IsRefund) continue;
            gained |= deltas[i].Amount > 0;
            lost |= deltas[i].Amount < 0;
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayOutcomeResources(gained, lost);
        }
    }
}
