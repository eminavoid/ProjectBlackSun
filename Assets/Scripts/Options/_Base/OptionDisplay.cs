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

    public Action<OptionDisplay> onOptionChosen;

    private Option option;
    private Seed seed;
    private readonly List<ResourceDelta> fixedChanges = new List<ResourceDelta>();

    public Option Option => option;
    public Seed Seed => seed;

    public void InitializeData(Option optionReference, Seed seed)
    {
        this.seed = seed;
        option = optionReference;
        button.interactable = option.CanExecute();
        ShowFixedChanges();
    }

    public void ExecuteOptions()
    {
        if (option.CanExecute())
        {
            onOptionChosen?.Invoke(this);
        }
    }

    public void GetFixedChangeIcons(List<(RectTransform icon, ResourceDelta delta)> icons)
    {
        if (resourceRow != null) resourceRow.GetChipIcons(icons);
    }

    private void ShowFixedChanges()
    {
        if (resourceRow == null) return;

        fixedChanges.Clear();
        option.GetFixedResourceChanges(fixedChanges);

        resourceRow.Show(fixedChanges, !button.interactable);
        if (costText != null) costText.SetActive(fixedChanges.Count == 0);
    }
}
