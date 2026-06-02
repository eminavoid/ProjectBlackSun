using UnityEngine;
using System;

public class OptionDisplay : MonoBehaviour
{
    public Action<Option> onOptionSelected;

    private Option option;

    public void InitializeData(Option optionReference)
    {
        option = optionReference;
    }

    public void ExecuteOptions()
    {
        if (option.CanExecute())
        {
            option.ExecuteOption();
            OnOptionExecuted();
        }
    }

    private void OnOptionExecuted()
    {
        onOptionSelected?.Invoke(option);
    }
}