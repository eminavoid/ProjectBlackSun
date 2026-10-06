using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.UI;

public class OptionDisplay : MonoBehaviour
{
    [SerializeField] private Button button;
    [Tooltip("Icons for what the option spends or gives no matter the outcome.")]
    [SerializeField] private ResourceAmountRow resourceRow;
    [Tooltip("Cost text from the data. Only shown when the row has nothing to show (e.g. \"No cost\").")]
    [SerializeField] private GameObject costText;

    /// <summary>The player picked this option. Whoever listens plays the choice out and executes it.</summary>
    public Action<OptionDisplay> onOptionChosen;

    private Option option;
    private readonly List<ResourceDelta> fixedChanges = new List<ResourceDelta>();

    public Option Option => option;

    public void InitializeData(Option optionReference)
    {
        option = optionReference;
        button.interactable = option.CanExecute();
        ShowFixedChanges();
    }

    // Hooked to the button.
    public void ExecuteOptions()
    {
        if (option.CanExecute())
        {
            onOptionChosen?.Invoke(this);
        }
    }

    /// <summary>The icons of what the option spends (or gives) up front, with their changes.</summary>
    public void GetFixedChangeIcons(List<(RectTransform icon, ResourceDelta delta)> icons)
    {
        if (resourceRow != null) resourceRow.GetChipIcons(icons);
    }

    private void ShowFixedChanges()
    {
        if (resourceRow == null) return;

        fixedChanges.Clear();
        option.GetFixedResourceChanges(fixedChanges);

        // Dimmed like the texts when the option can't be afforded.
        resourceRow.Show(fixedChanges, !button.interactable);
        if (costText != null) costText.SetActive(fixedChanges.Count == 0);
    }
}
