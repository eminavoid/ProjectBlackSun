using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TheologyMenu : MonoBehaviour
{
    private const string HumanSacrificeDoctrine = "Human Sacrifices";

    private static readonly string[] DefaultBlessings =
    {
        "A Quiet Favor",
        "The Street Bows",
        "Zeal Flickers"
    };

    private static readonly string[] DefaultCurses =
    {
        "The Flock Grows Restless",
        "A Tithe Goes Missing",
        "Whispers Turn Sour"
    };

    [SerializeField] private Resource costResource = Resource.Zeal;
    [SerializeField, Min(0)] private int startCost = 0;
    [SerializeField, Min(0)] private int costIncrease = 15;
    [SerializeField] private ResourceIcons icons;
    [SerializeField] private Unlocked unlockedSeeds;
    [SerializeField] private Unlocked unlockedDoctrines;
    [SerializeField] private Doctrine[] doctrinePool;

    [SerializeField] private Image closedArt;
    [SerializeField] private RectMask2D openMask;
    [SerializeField] private RectTransform openArt;
    [SerializeField] private Button communeButton;
    [SerializeField] private Image costIcon;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text seedsCount;
    [SerializeField] private TMP_Text doctrinesCount;
    [SerializeField] private Sprite[] eyePhases;
    [SerializeField] private Image eye;
    [SerializeField] private Image eyeNext;
    [SerializeField] private Button[] plates;

    private Button offeringButton;
    private Button sacrificeButton;
    private TMP_Text blessingsCount;
    private int cost;
    private int phase;
    private int offeringTier;
    private int sacrificeTier;
    private int blessingsGranted;
    private bool riteOpen;
    private bool opening;
    private bool revealed;
    private bool stakesLocked;
    private bool rewarding;
    private bool paying;
    private bool communeLabelStored;
    private bool communeWasAffordable = true;
    private Color communeLabelColor;

    public static event System.Action Communed;
    public static event System.Action RewardGranted;

    public static void PrepareNewMatch()
    {
        TheologyMenu menu = FindAnyObjectByType<TheologyMenu>(FindObjectsInactive.Include);
        if (menu == null) return;
        menu.blessingsGranted = 0;
        if (menu.unlockedDoctrines != null) menu.unlockedDoctrines.Clear();
    }

    private void Awake()
    {
        costResource = Resource.Zeal;
        cost = startCost;
        offeringButton = PlateNamed("Offerings");
        sacrificeButton = PlateNamed("Sacrifices");
        blessingsCount = FindCount("Blessings Count");
        if (offeringButton != null) offeringButton.onClick.AddListener(() => StartCoroutine(PayStake(false)));
        if (sacrificeButton != null) sacrificeButton.onClick.AddListener(() => StartCoroutine(PayStake(true)));
    }

    private void OnEnable()
    {
        closedArt.color = Color.white;
        openMask.enabled = true;
        openMask.gameObject.SetActive(false);
        communeButton.interactable = CanAfford(costResource, cost);
        phase = 0;
        riteOpen = false;
        opening = false;
        revealed = false;
        stakesLocked = false;
        offeringTier = 0;
        sacrificeTier = 0;
        rewarding = false;
        paying = false;
        if (eyePhases != null && eyePhases.Length > 0) eye.sprite = eyePhases[0];
        eyeNext.color = new Color(1f, 1f, 1f, 0f);
        SetPlates(false);
        if (!ResourceManager.IsNull && ResourceManager.Resources != null)
            ResourceManager.Resources.onResourceGained += OnWalletChanged;
        Refresh();
    }

    private void OnDisable()
    {
        if (!ResourceManager.IsNull && ResourceManager.Resources != null)
            ResourceManager.Resources.onResourceGained -= OnWalletChanged;
        StopAllCoroutines();
        DOTween.Kill(this);
        if (!ResourceManager.IsNull && ResourceManager.Sidebar != null) ResourceManager.Sidebar.Release(this);
    }

    private void LateUpdate()
    {
        ApplyCommuneGate();
    }

    public void Commune()
    {
        if (!CommuneAllowed())
        {
            ApplyCommuneGate();
            TopScreenNotification.Show("Not enough Zeal.", 3f);
            return;
        }

        opening = true;
        if (communeButton != null) communeButton.interactable = false;
        Communed?.Invoke();
        StartCoroutine(PayAndOpen());
    }

    public void AdvanceEye()
    {
        if (!riteOpen || rewarding || eyePhases == null || eyePhases.Length == 0) return;
        if (phase == eyePhases.Length - 1 || DOTween.IsTweening(eyeNext)) return;

        if (!stakesLocked)
        {
            stakesLocked = true;
            SetPlates(false);
        }

        eyeNext.sprite = eyePhases[++phase];
        eyeNext.DOFade(1f, 0.35f).SetId(this).OnComplete(() =>
        {
            eye.sprite = eyeNext.sprite;
            eyeNext.color = new Color(1f, 1f, 1f, 0f);
            if (phase == eyePhases.Length - 1) GrantReward();
        });
    }

    private IEnumerator PayAndOpen()
    {
        if (cost > 0)
        {
            if (!CanAfford(costResource, cost))
            {
                opening = false;
                Refresh();
                TopScreenNotification.Show("Not enough Zeal.", 3f);
                yield break;
            }

            Tween flight = ResourceManager.Sidebar != null
                ? ResourceManager.Sidebar.Send(this, costResource, -cost, costIcon.rectTransform, 0f)
                : null;
            if (flight != null) yield return flight.WaitForCompletion();
            if (!WalletHas(costResource, cost))
            {
                if (ResourceManager.Sidebar != null) ResourceManager.Sidebar.Release(this);
                opening = false;
                Refresh();
                TopScreenNotification.Show("Not enough Zeal.", 3f);
                yield break;
            }

            ResourceManager.Resources.AddResource(costResource, -cost);
            if (ResourceManager.Sidebar != null) ResourceManager.Sidebar.Release(this);
        }

        cost += costIncrease;
        ResetRite();
        Refresh();

        if (revealed)
        {
            opening = false;
            riteOpen = true;
            SetPlates(true);
            yield break;
        }

        revealed = true;
        openMask.enabled = true;
        openMask.gameObject.SetActive(true);
        openArt.anchoredPosition = new Vector2(closedArt.rectTransform.rect.width - openArt.rect.width, openArt.anchoredPosition.y);
        closedArt.color = Color.white;
        DOTween.Kill(this);
        DOTween.Sequence()
            .Append(openArt.DOAnchorPosX(0f, 0.7f).SetEase(Ease.InOutCubic))
            .AppendCallback(() => openMask.enabled = false)
            .Append(closedArt.DOFade(0f, 0.2f))
            .AppendCallback(() =>
            {
                opening = false;
                riteOpen = true;
                SetPlates(true);
            })
            .SetId(this);
    }

    private void ResetRite()
    {
        phase = 0;
        stakesLocked = false;
        offeringTier = 0;
        sacrificeTier = 0;
        rewarding = false;
        if (eyePhases != null && eyePhases.Length > 0 && eye != null) eye.sprite = eyePhases[0];
        if (eyeNext != null) eyeNext.color = new Color(1f, 1f, 1f, 0f);
    }

    private IEnumerator PayStake(bool sacrifice)
    {
        if (paying || !riteOpen || stakesLocked) yield break;
        if (sacrifice && !SacrificeUnlocked()) yield break;

        int tier = sacrifice ? sacrificeTier : offeringTier;
        int price = TheologyOdds.NextCost(tier);
        if (price <= 0) yield break;

        Resource resource = sacrifice ? Resource.Flock : Resource.Wealth;
        if (!CanAfford(resource, price))
        {
            TopScreenNotification.Show(sacrifice ? "Not enough Flock." : "Not enough Wealth.", 3f);
            yield break;
        }

        paying = true;
        if (!CanAfford(resource, price))
        {
            paying = false;
            Refresh();
            TopScreenNotification.Show(sacrifice ? "Not enough Flock." : "Not enough Wealth.", 3f);
            yield break;
        }

        Button button = sacrifice ? sacrificeButton : offeringButton;
        RectTransform target = button != null ? (RectTransform)button.transform : costIcon.rectTransform;
        Tween flight = ResourceManager.Sidebar != null
            ? ResourceManager.Sidebar.Send(this, resource, -price, target, 0f)
            : null;
        if (flight != null) yield return flight.WaitForCompletion();
        if (!WalletHas(resource, price))
        {
            if (ResourceManager.Sidebar != null) ResourceManager.Sidebar.Release(this);
            paying = false;
            Refresh();
            TopScreenNotification.Show(sacrifice ? "Not enough Flock." : "Not enough Wealth.", 3f);
            yield break;
        }

        ResourceManager.Resources.AddResource(resource, -price);
        if (ResourceManager.Sidebar != null) ResourceManager.Sidebar.Release(this);
        paying = false;

        if (sacrifice) sacrificeTier++;
        else offeringTier++;
        Refresh();
    }

    private void GrantReward()
    {
        if (rewarding) return;
        rewarding = true;
        riteOpen = false;
        SetPlates(false);

        if (TutorialSession.FixedUnlock != null && TutorialSession.PendingDoctrine != null)
        {
            Doctrine fixedDoctrine = TutorialSession.PendingDoctrine;
            Unlockeable unlock = TutorialSession.FixedUnlock;
            TutorialSession.ClaimFixedUnlock(unlock);
            GiveDoctrine(fixedDoctrine);
            Reveal("Doctrine", fixedDoctrine.Name, fixedDoctrine.Description);
            RewardGranted?.Invoke();
            CloseAfterReward();
            return;
        }

        TheologyChances chances = TheologyOdds.Resolve(offeringTier, sacrificeTier);
        TheologyRewardKind kind = TheologyOdds.Roll(chances);
        switch (kind)
        {
            case TheologyRewardKind.Seed:
                RevealSeed();
                break;
            case TheologyRewardKind.Doctrine:
                RevealDoctrine();
                break;
            case TheologyRewardKind.Blessing:
                blessingsGranted++;
                Reveal("Blessing", Pick(DefaultBlessings), "The eye grants a blessing.");
                break;
            default:
                Reveal("Curse", Pick(DefaultCurses), "The eye turns against you.");
                break;
        }

        RewardGranted?.Invoke();
        CloseAfterReward();
    }

    private void CloseAfterReward()
    {
        MenuTransition transition = GetComponent<MenuTransition>();
        if (transition != null) transition.Close();
        else gameObject.SetActive(false);
    }

    private void RevealSeed()
    {
        Seed seed = PickSeed();
        if (seed == null)
        {
            Reveal("Seed", "Seed", "The eye offers a seed, but none are prepared.");
            return;
        }

        if (unlockedSeeds != null && !unlockedSeeds.Contains(seed)) unlockedSeeds.Add(seed, Rarity.Common);
        DistrictSeedPlantPanel panel = FindAnyObjectByType<DistrictSeedPlantPanel>();
        if (panel != null) panel.Grant(seed, 1);
        Reveal("Seed", seed.Title, seed.Description);
    }

    private void RevealDoctrine()
    {
        Doctrine doctrine = PickDoctrine();
        if (doctrine == null)
        {
            Reveal("Doctrine", "Doctrine", "The eye offers a doctrine, but none are prepared.");
            return;
        }

        GiveDoctrine(doctrine);
        Reveal("Doctrine", doctrine.Name, doctrine.Description);
    }

    private void GiveDoctrine(Doctrine doctrine)
    {
        if (doctrine == null) return;
        if (unlockedDoctrines != null && !unlockedDoctrines.Contains(doctrine))
            unlockedDoctrines.Add(doctrine, Rarity.Common);

        DoctrinesController doctrines = FindAnyObjectByType<DoctrinesController>();
        if (doctrines != null && doctrines.Stats != null)
            doctrines.Stats.RememberDoctrine(doctrine);
    }

    private static void Reveal(string kind, string title, string body)
    {
        string heading = string.IsNullOrWhiteSpace(title) ? kind : title;
        string detail = string.IsNullOrWhiteSpace(body) ? kind : body;
        SeedEventManager.CreateEventOutputWindow(heading, detail, null, false);
    }

    private Seed PickSeed()
    {
        DistrictSeedPlantPanel panel = FindAnyObjectByType<DistrictSeedPlantPanel>();
        IReadOnlyList<Seed> known = panel != null ? panel.KnownSeeds : null;
        if (known == null || known.Count == 0) return null;

        List<Seed> choices = new List<Seed>();
        for (int i = 0; i < known.Count; i++)
        {
            Seed seed = known[i];
            if (seed == null || !seed.HasDisplayTitle) continue;
            choices.Add(seed);
        }

        if (choices.Count == 0) return null;
        return choices[Random.Range(0, choices.Count)];
    }

    private Doctrine PickDoctrine()
    {
        if (doctrinePool == null || doctrinePool.Length == 0) return null;

        DoctrinesController doctrines = FindAnyObjectByType<DoctrinesController>();
        List<Doctrine> owned = doctrines != null && doctrines.Stats != null
            ? doctrines.Stats.DoctrineInventory
            : null;

        List<Doctrine> available = new List<Doctrine>();
        for (int i = 0; i < doctrinePool.Length; i++)
        {
            Doctrine doctrine = doctrinePool[i];
            if (doctrine == null) continue;
            if (owned != null && owned.Contains(doctrine)) continue;
            available.Add(doctrine);
        }

        if (available.Count == 0) return null;
        return available[Random.Range(0, available.Count)];
    }

    private static string Pick(string[] options)
    {
        if (options == null || options.Length == 0) return string.Empty;
        return options[Random.Range(0, options.Length)];
    }

    private void SetPlates(bool open)
    {
        bool allow = open && !stakesLocked && !paying;
        int offeringCost = TheologyOdds.NextCost(offeringTier);
        int sacrificeCost = TheologyOdds.NextCost(sacrificeTier);
        if (offeringButton != null)
            offeringButton.interactable = allow && offeringCost > 0 && CanAfford(Resource.Wealth, offeringCost);
        if (sacrificeButton != null)
            sacrificeButton.interactable = allow && SacrificeUnlocked() && sacrificeCost > 0 && CanAfford(Resource.Flock, sacrificeCost);
        RefreshPlates(open);
    }

    private void Refresh()
    {
        costText.text = cost > 0 ? cost.ToString() : "FREE";
        if (icons != null && costIcon != null)
        {
            costIcon.sprite = icons.Get(costResource);
            costIcon.gameObject.SetActive(cost > 0);
        }

        if (seedsCount != null && unlockedSeeds != null) seedsCount.text = unlockedSeeds.GetCopy().Count.ToString();
        if (doctrinesCount != null && unlockedDoctrines != null) doctrinesCount.text = unlockedDoctrines.GetCopy().Count.ToString();
        if (blessingsCount != null) blessingsCount.text = blessingsGranted.ToString();
        ApplyCommuneGate();
        SetPlates(riteOpen);
    }

    private void ApplyCommuneGate()
    {
        if (communeButton == null) return;

        bool affordable = CommuneAllowed();
        communeButton.interactable = affordable;
        communeButton.enabled = affordable;

        Image hit = communeButton.GetComponent<Image>();
        if (hit != null) hit.raycastTarget = affordable;

        RaycastGate gate = communeButton.GetComponent<RaycastGate>();
        if (gate == null) gate = communeButton.gameObject.AddComponent<RaycastGate>();
        gate.open = affordable;

        TMP_Text label = communeButton.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            if (!communeLabelStored)
            {
                communeLabelColor = label.color;
                communeLabelStored = true;
            }

            if (!affordable) label.color = new Color(0.42f, 0.4f, 0.38f, 0.45f);
            else if (!communeWasAffordable) label.color = communeLabelColor;
        }

        communeWasAffordable = affordable;
    }

    private bool CommuneAllowed()
    {
        if (opening || riteOpen || rewarding || paying) return false;
        int price = ShownCost();
        if (price <= 0) return true;
        return ShownAmount() >= price;
    }

    private int ShownCost()
    {
        if (costText != null && TryParseAmount(costText.text, out int parsed)) return parsed;
        return cost;
    }

    private int ShownAmount()
    {
        int have = 0;
        bool any = false;
        if (!ResourceManager.IsNull && ResourceManager.Resources != null)
        {
            have = ResourceManager.Resources.GetResourceAmount(Resource.Zeal);
            any = true;
        }

        if (ResourceManager.IsNull || ResourceManager.Sidebar == null) return any ? have : 0;

        int visible = ResourceManager.Sidebar.Visible(Resource.Zeal);
        have = any ? Mathf.Min(have, visible) : visible;
        if (costIcon != null && ResourceManager.Sidebar.TryMatchIcon(costIcon.sprite, out int matched))
            have = Mathf.Min(have, matched);
        return have;
    }

    private static bool TryParseAmount(string text, out int amount)
    {
        amount = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        int start = -1;
        int end = -1;
        for (int i = 0; i < text.Length; i++)
        {
            if (!char.IsDigit(text[i])) continue;
            if (start < 0) start = i;
            end = i;
        }

        if (start < 0) return false;
        return int.TryParse(text.Substring(start, end - start + 1), out amount);
    }

    private sealed class RaycastGate : MonoBehaviour, ICanvasRaycastFilter
    {
        public bool open = true;
        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera) => open;
    }

    private void RefreshPlates(bool open)
    {
        WritePlate(offeringButton, "OFFERINGS", offeringTier, Resource.Wealth, open && !stakesLocked);
        bool sacrificeOpen = open && !stakesLocked && SacrificeUnlocked();
        WritePlate(sacrificeButton, "SACRIFICES", sacrificeTier, Resource.Flock, sacrificeOpen);
    }

    private static void WritePlate(Button button, string title, int tier, Resource resource, bool priced)
    {
        if (button == null) return;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label == null) return;
        if (!priced)
        {
            label.text = title;
            return;
        }

        int next = TheologyOdds.NextCost(tier);
        label.text = next > 0 ? title + "\n" + next + " " + resource.ToString().ToUpperInvariant() : title + "\nMAX";
    }

    private bool SacrificeUnlocked()
    {
        DoctrinesController doctrines = FindAnyObjectByType<DoctrinesController>();
        return doctrines != null && doctrines.HasEquippedDoctrine(HumanSacrificeDoctrine);
    }

    private void OnWalletChanged(Resource resource, int amount) => Refresh();

    private static bool CanAfford(Resource resource, int amount)
    {
        if (amount <= 0) return true;
        return Spendable(resource) >= amount;
    }

    private static bool WalletHas(Resource resource, int amount)
    {
        if (amount <= 0) return true;
        return !ResourceManager.IsNull && ResourceManager.Resources != null
            && ResourceManager.Resources.GetResourceAmount(resource) >= amount;
    }

    private static int Spendable(Resource resource)
    {
        if (ResourceManager.IsNull || ResourceManager.Resources == null) return 0;
        if (ResourceManager.Sidebar != null) return ResourceManager.Sidebar.Visible(resource);
        return ResourceManager.Resources.GetResourceAmount(resource);
    }

    private Button PlateNamed(string plateName)
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null && buttons[i].gameObject.name == plateName) return buttons[i];
        }

        return null;
    }

    private TMP_Text FindCount(string objectName)
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null && texts[i].gameObject.name == objectName) return texts[i];
        }

        return null;
    }
}
