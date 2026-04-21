using UnityEngine;
using System;
using TMPro;
using UnityEngine.UI;

public class OptionDisplay : MonoBehaviour
{
    public Action<Option> onOptionSelected;

    [Header("Optional UI Bindings")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Image iconImage;

    private Option option;

    public void InitializeData(Option optionReference)
    {
        option = optionReference;
        RefreshView();
    }

    public void ExecuteOptions()
    {
        option.ExecuteOption();
        OnOptionExecuted();
    }

    private void OnOptionExecuted()
    {
        onOptionSelected?.Invoke(option);
    }

    private void RefreshView()
    {
        if (option == null) return;

        if (titleText != null)
        {
            titleText.text = option.Title;
        }

        if (descriptionText != null)
        {
            descriptionText.text = option.Description;
        }

        if (iconImage != null)
        {
            iconImage.sprite = option.Icon;
            iconImage.enabled = option.Icon != null;
        }
    }
}