using UnityEngine;
using System;

public class OptionDisplay : MonoBehaviour
{
    public Action onOptionSelected;

    private Option option;

    public void InitializeData(Option optionReference)
    {
        option = optionReference;
    }

    public void ExecuteOptions()
    {
        option.ExecuteOption();
        OnOptionExecuted();
    }

    private void OnOptionExecuted()
    {
        onOptionSelected?.Invoke();
    }
}