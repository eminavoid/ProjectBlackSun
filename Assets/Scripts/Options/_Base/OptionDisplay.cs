using UnityEngine;
using System;
using TMPro;

public class OptionDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI title;
    [SerializeField] private TextMeshProUGUI description;

    public Action<Option> onOptionSelected;

    private Option option;

    public void InitializeData(Option optionReference)
    {
        option = optionReference;
        title.text = optionReference.Title;
        description.text = optionReference.Description;
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