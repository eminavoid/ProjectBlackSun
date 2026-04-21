using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SeedButtonView : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Image iconImage;
    [SerializeField] private string selectedPrefix = "> ";

    public void Bind(Seed seed, bool selected)
    {
        if (seed == null) return;

        if (titleText != null)
        {
            titleText.text = selected ? $"{selectedPrefix}{seed.Title}" : seed.Title;
        }

        if (descriptionText != null)
        {
            descriptionText.text = seed.Description;
        }

        if (iconImage != null)
        {
            iconImage.sprite = seed.Icon;
            iconImage.enabled = seed.Icon != null;
        }
    }
}
