using UnityEngine;
using System;
using UnityEngine.UI;

public class OptionDisplay : MonoBehaviour
{
    [SerializeField] private Button button;

    public Action<Option> onOptionSelected;

    private Option option;

    public void InitializeData(Option optionReference)
    {
        option = optionReference;
        button.interactable = option.CanExecute();
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