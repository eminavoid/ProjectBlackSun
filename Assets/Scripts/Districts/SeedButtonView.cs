using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SeedButtonView : MonoBehaviour
{
    private static readonly string[] Tiers = { "I", "II", "III" };

    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Image iconImage;
    [SerializeField] private string selectedPrefix = "> ";

    [Header("Card")]
    [SerializeField] private TMP_Text tierText;
    [SerializeField] private TMP_Text turnsText;
    [SerializeField] private TMP_Text resourceText;
    [SerializeField] private Image resourceIcon;
    [SerializeField] private ResourceIcons resourceIcons;
    [SerializeField] private GameObject selectedMark;

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

        if (tierText != null)
        {
            tierText.text = seed.DifficultyValue >= 1 && seed.DifficultyValue <= Tiers.Length ? $"TIER {Tiers[seed.DifficultyValue - 1]}" : "-";
        }

        if (turnsText != null)
        {
            turnsText.text = seed.Ticks == 1 ? "1 TURN" : $"{seed.Ticks} TURNS";
        }

        if (resourceText != null)
        {
            bool hasResource = seed.Region.TryGetResource(out Resource resource);
            resourceText.text = hasResource ? resource.ToString().ToUpperInvariant() : "-";
            if (resourceIcon != null)
            {
                resourceIcon.sprite = hasResource ? resourceIcons.Get(resource) : null;
                resourceIcon.gameObject.SetActive(hasResource);
            }
        }

        if (selectedMark != null)
        {
            selectedMark.SetActive(selected);
        }
    }
}
