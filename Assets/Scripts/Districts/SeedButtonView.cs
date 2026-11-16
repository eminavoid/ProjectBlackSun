using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SeedButtonView : MonoBehaviour
{
    private static readonly string[] Tiers = { "I", "II", "III" };
    private static readonly System.Globalization.TextInfo TitleCase = System.Globalization.CultureInfo.InvariantCulture.TextInfo;

    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Image iconImage;
    [SerializeField] private string selectedPrefix = "> ";

    [Header("Card")]
    [SerializeField] private TMP_Text tierText;
    [SerializeField] private TMP_Text turnsText;
    [Tooltip("Says it's free, or when a seed you just planted can be planted again.")]
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text resourceText;
    [SerializeField] private Image resourceIcon;
    [SerializeField] private ResourceIcons resourceIcons;
    [SerializeField] private GameObject selectedMark;

    public void Bind(Seed seed, bool selected, int turnsUntilReady = 0)
    {
        if (seed == null) return;

        if (titleText != null)
        {
            string title = TitleCase.ToTitleCase(seed.Title.ToLowerInvariant());
            titleText.text = selected ? $"{selectedPrefix}{title}" : title;
        }

        if (descriptionText != null)
        {
            descriptionText.text = seed.Premise;
        }

        if (costText != null)
        {
            costText.text = turnsUntilReady <= 0 ? "FREE"
                : turnsUntilReady == 1 ? "READY NEXT TURN"
                : $"READY IN {turnsUntilReady} TURNS";
        }

        if (iconImage != null)
        {
            iconImage.sprite = seed.Icon;
            iconImage.enabled = seed.Icon != null;
        }

        if (tierText != null)
        {
            tierText.text = $"TIER {Tiers[Mathf.Clamp(seed.DifficultyValue, 1, Tiers.Length) - 1]}";
        }

        if (turnsText != null)
        {
            turnsText.text = seed.Ticks == 1 ? "1 TURN" : $"{seed.Ticks} TURNS";
        }

        if (resourceText != null)
        {
            bool hasResource = seed.Region.TryGetResource(out Resource resource);
            resourceText.text = hasResource ? resource.ToString().ToUpperInvariant() : "ANY DISTRICT";
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
