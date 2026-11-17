using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zeke.Tooltips;

public class TheologyMenu : MonoBehaviour
{
    private const string HumanSacrificeDoctrine = "Human Sacrifices";

    private static readonly Omen[] Blessings =
    {
        new Omen("A Quiet Favor", "A new believer takes the cloth. One more cleric waits in your reserve.", Resource.Clerics, 1),
        new Omen("The Street Bows", "The street bows its head. New faithful join your flock.", Resource.Flock, 20),
        new Omen("Zeal Flickers", "A spark runs through your faithful. Their zeal burns brighter.", Resource.Zeal, 30),
        new Omen("A Generous Patron", "A wealthy patron leaves a heavy purse at your altar.", Resource.Wealth, 25),
        new Omen("Loose Tongues", "Confessions pour in. Your informants have much to tell.", Resource.Secrets, 15)
    };

    private static readonly Omen[] Curses =
    {
        new Omen("The Flock Grows Restless", "Doubt spreads through the pews. Some of your faithful drift away.", Resource.Flock, -15),
        new Omen("A Tithe Goes Missing", "A collection plate comes back empty. Someone has taken the tithe.", Resource.Wealth, -20),
        new Omen("Whispers Turn Sour", "Your informants fall silent. Some of your secrets are spent.", Resource.Secrets, -10)
    };

    private static readonly Color Ink = new Color(0.27f, 0.16f, 0.11f, 1f);
    private static readonly Color Shortfall = new Color(0.62f, 0.12f, 0.08f, 1f);

    private readonly struct Omen
    {
        public readonly string Name;
        public readonly string Text;
        public readonly Resource Resource;
        public readonly int Amount;

        public Omen(string name, string text, Resource resource, int amount)
        {
            Name = name;
            Text = text;
            Resource = resource;
            Amount = amount;
        }
    }

    [SerializeField] private Locked seedsPool;
    [SerializeField] private Locked doctrinesPool;
    [SerializeField] private DistrictSeedPlantPanel districtSeedPlantPanel;
    [SerializeField] private DoctrinePopUp doctrinePopUp;

    [Space]

    [SerializeField] private Resource costResource = Resource.Zeal;
    [SerializeField, Min(0)] private int startCost = 0;
    [SerializeField, Min(0)] private int costIncrease = 15;
    [SerializeField] private ResourceIcons icons;
    [SerializeField] private Unlocked unlockedSeeds;
    [SerializeField] private Unlocked unlockedDoctrines;

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

    [Header("Close Button")]
    [SerializeField] private Button closeButton;
    [Tooltip("From the visible art's top-right corner to the close button's, with the temple closed.")]
    [SerializeField] private Vector2 closedCloseInset = new Vector2(70f, 150f);
    [Tooltip("The same, once the rite has opened the wall.")]
    [SerializeField] private Vector2 openCloseInset = new Vector2(70f, 150f);

    private bool firstTime = true;

    private readonly Vector3[] artCorners = new Vector3[4];

    private Button offeringButton;
    private Button sacrificeButton;
    private Button dismissButton;
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
    private bool dismissLocked;
    private bool communeLabelStored;
    private bool communeWasAffordable = true;
    private Color communeLabelColor;
    private Color costColor;
    private TMP_Text oddsText;
    private TMP_Text eyePrompt;
    private bool eyeIdling;
    private bool punching;

    public static event System.Action Communed;
    public static event System.Action RewardGranted;

    private Image communePlate;

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
        costColor = costText.color;
        BuildFeedback();
        Transform plate = communeButton.transform.Find("Plate");
        if (plate != null) communePlate = plate.GetComponent<Image>();
        PressFeedback communeFeedback = communeButton.GetComponent<PressFeedback>();
        if (communeFeedback != null) communeFeedback.Denied += OnCommuneDenied;
        if (offeringButton != null) offeringButton.onClick.AddListener(() => StartCoroutine(PayStake(false)));
        if (sacrificeButton != null) sacrificeButton.onClick.AddListener(() => StartCoroutine(PayStake(true)));
        if (closeButton != null)
        {
            closeButton.onClick = new Button.ButtonClickedEvent();
            closeButton.onClick.AddListener(Dismiss);
        }

        Transform dismiss = transform.Find("Dismiss");
        if (dismiss == null) return;
        dismissButton = dismiss.GetComponent<Button>();
        if (dismissButton == null) return;
        dismissButton.onClick = new Button.ButtonClickedEvent();
        dismissButton.onClick.AddListener(Dismiss);
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
        dismissLocked = false;
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
        dismissLocked = false;
        punching = false;
        StopEyeIdle();
        StopAllCoroutines();
        DOTween.Kill(this);
        if (!ResourceManager.IsNull && ResourceManager.Sidebar != null) ResourceManager.Sidebar.Release(this);
    }

    private void LateUpdate()
    {
        if (communePlate != null) communePlate.color = new Color(1f, 1f, 1f, closedArt.color.a);
        ApplyCommuneGate();
        PlaceCloseButton();
    }

    private void PlaceCloseButton()
    {
        if (closeButton == null) return;

        bool open = openMask.gameObject.activeSelf;
        RectTransform art = open ? openArt : closedArt.rectTransform;
        art.GetWorldCorners(artCorners);
        Vector2 inset = open ? openCloseInset : closedCloseInset;
        Vector3 scale = art.lossyScale;
        closeButton.transform.position = artCorners[2] - new Vector3(inset.x * scale.x, inset.y * scale.y, 0f);
    }

    private void Dismiss()
    {
        if (dismissLocked) return;
        MenuTransition transition = GetComponent<MenuTransition>();
        if (transition != null) transition.Close();
        else gameObject.SetActive(false);
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
        dismissLocked = true;
        if (communeButton != null) communeButton.interactable = false;
        closedArt.transform.DOComplete();
        closedArt.transform.DOPunchScale(Vector3.one * 0.035f, 0.4f, 6, 0.5f).SetUpdate(true).SetLink(gameObject);
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
        bool waking = phase == eyePhases.Length - 1;
        StopEyeIdle();
        punching = true;
        eye.transform.DOPunchScale(Vector3.one * (waking ? 0.16f : 0.08f), waking ? 0.5f : 0.3f, waking ? 10 : 8, 0.6f)
            .SetUpdate(true).SetLink(gameObject).OnComplete(() =>
            {
                punching = false;
                RefreshFeedback();
            });
        if (waking) eye.transform.DOShakeRotation(0.5f, new Vector3(0f, 0f, 6f), 14, 90f).SetUpdate(true).SetLink(gameObject);
        RefreshFeedback();
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
                dismissLocked = false;
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
                dismissLocked = false;
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
            RevealDoctrine(fixedDoctrine);
            return;
        }

        TheologyChances chances = CurrentChances();
        firstTime = false;

        TheologyRewardKind kind = TheologyOdds.Roll(chances);
        switch (kind)
        {
            case TheologyRewardKind.Seed:
                RevealSeed();
                break;
            case TheologyRewardKind.Doctrine:
                Doctrine doctrine = PickDoctrine();
                GiveDoctrine(doctrine);
                RevealDoctrine(doctrine);
                break;
            case TheologyRewardKind.Blessing:
                blessingsGranted++;
                RevealOmen("Blessing", PickOmen(Blessings, true));
                break;
            default:
                RevealOmen("Curse", PickOmen(Curses, false));
                break;
        }
    }

    private TheologyChances CurrentChances()
    {
        return firstTime
            ? TheologyOdds.GuaranteedResolve(offeringTier, sacrificeTier)
            : TheologyOdds.Resolve(offeringTier, sacrificeTier);
    }

    private void CloseAfterReward()
    {
        RewardGranted?.Invoke();
        MenuTransition transition = GetComponent<MenuTransition>();
        if (transition != null) transition.Close();
        else gameObject.SetActive(false);
    }

    private void RevealSeed()
    {
        Seed seed = PickSeed();
        if (seed == null)
        {
            Reveal("New Seed", "The eye offers a seed, but none are prepared.");
            return;
        }

        if (unlockedSeeds != null && !unlockedSeeds.Contains(seed)) unlockedSeeds.Add(seed, Rarity.Common);
        DistrictSeedPlantPanel panel = FindAnyObjectByType<DistrictSeedPlantPanel>();
        if (panel != null) panel.Grant(seed, 1);
        Reveal($"New Seed: {seed.Title}", $"{seed.Description}\n\n<size=85%>It waits in your seed menu. Plant it on a free zone.</size>");
        districtSeedPlantPanel.NewSeeds += 1;
    }

    private void RevealDoctrine(Doctrine doctrine)
    {
        if (doctrine == null)
        {
            Reveal("New Doctrine", "The eye offers a doctrine, but none are prepared.");
            return;
        }

        Reveal($"New Doctrine: {doctrine.Name}", $"{doctrine.Description}\n\n<size=85%>{DoctrineEffect(doctrine)}Adopt it from Doctrines to put it to work.</size>");
    }

    private void RevealOmen(string kind, Omen omen)
    {
        ResourceManager.AddResource(omen.Resource, omen.Amount);
        Reveal($"{kind}: {omen.Name}", omen.Text, new[] { new ResourceDelta(omen.Resource, omen.Amount) });
    }

    private static string DoctrineEffect(Doctrine doctrine)
    {
        var parts = new List<string>();
        foreach (Doctrine.StatUpdate update in doctrine.StatUpdates)
        {
            if (update.changeAmount == 0) continue;
            parts.Add($"{(update.changeAmount > 0 ? "+" : string.Empty)}{update.changeAmount} {Virtues.Name(update.playerStat)}");
        }

        return parts.Count > 0 ? $"When adopted: {string.Join(", ", parts)}. " : string.Empty;
    }

    private void GiveDoctrine(Doctrine doctrine)
    {
        if (doctrine == null) return;
        if (unlockedDoctrines != null && !unlockedDoctrines.Contains(doctrine))
            unlockedDoctrines.Add(doctrine, Rarity.Common);

        DoctrinesController doctrines = FindAnyObjectByType<DoctrinesController>();
        if (doctrines != null && doctrines.Stats != null)
        {
            doctrines.Stats.RememberDoctrine(doctrine);
            doctrinePopUp.OnDoctrineReceived();
        }
    }

    private void Reveal(string title, string body, IReadOnlyList<ResourceDelta> changes = null)
    {
        SeedEventManager.CreateEventOutputWindow(title, body, changes, false, CloseAfterReward);
    }

    private Seed PickSeed()
    {
        if (seedsPool == null) return null;

        List<Seed> choices = new List<Seed>();
        foreach (Unlockeable unlock in seedsPool.GetCopy())
        {
            if (unlock == null || !(unlock.Scriptable is Seed seed)) continue;
            if (unlockedSeeds != null && unlockedSeeds.Contains(seed)) continue;
            choices.Add(seed);
        }

        if (choices.Count == 0) return null;
        return choices[Random.Range(0, choices.Count)];
    }

    private Doctrine PickDoctrine()
    {
        if (doctrinesPool == null) return null;

        List<Doctrine> choices = new List<Doctrine>();
        foreach (Unlockeable unlock in doctrinesPool.GetCopy())
        {
            if (unlock == null || !(unlock.Scriptable is Doctrine doctrine)) continue;
            if (unlockedDoctrines != null && unlockedDoctrines.Contains(doctrine)) continue;
            choices.Add(doctrine);
        }

        if (choices.Count == 0) return null;
        return choices[Random.Range(0, choices.Count)];
    }

    private static Omen PickOmen(Omen[] omens, bool blessing)
    {
        int extreme = -1;
        float extremeLevel = 0f;
        for (int i = 0; i < omens.Length; i++)
        {
            if (omens[i].Resource == Resource.Clerics) continue;
            float level = Level(omens[i]);
            if (extreme >= 0 && (blessing ? level >= extremeLevel : level <= extremeLevel)) continue;
            extreme = i;
            extremeLevel = level;
        }

        float[] weights = new float[omens.Length];
        float total = 0f;
        for (int i = 0; i < omens.Length; i++)
        {
            int have = ResourceManager.IsNull ? 0 : ResourceManager.Resources.GetResourceAmount(omens[i].Resource);
            float weight = 1f;
            if (blessing && have < 0) weight += 4f;
            else if (i == extreme) weight += blessing ? 2f : 3f;
            if (!blessing && have <= 0) weight = 0.25f;
            weights[i] = weight;
            total += weight;
        }

        float roll = Random.value * total;
        for (int i = 0; i < omens.Length; i++)
        {
            roll -= weights[i];
            if (roll < 0f) return omens[i];
        }

        return omens[omens.Length - 1];
    }

    private static float Level(Omen omen)
    {
        int have = ResourceManager.IsNull ? 0 : ResourceManager.Resources.GetResourceAmount(omen.Resource);
        return have / (float)Mathf.Max(1, Mathf.Abs(omen.Amount));
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
        RefreshFeedback();
    }

    private void Refresh()
    {
        costText.text = cost > 0 ? cost.ToString() : "FREE";
        costText.color = cost > 0 && !riteOpen && !opening && !CanAfford(costResource, cost) ? Shortfall : costColor;
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

        bool idle = RiteIdle();
        bool affordable = idle && CommuneAllowed();
        communeButton.interactable = affordable;

        Image hit = communeButton.GetComponent<Image>();
        if (hit != null) hit.raycastTarget = idle;

        RaycastGate gate = communeButton.GetComponent<RaycastGate>();
        if (gate == null) gate = communeButton.gameObject.AddComponent<RaycastGate>();
        gate.open = idle;

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

    private bool RiteIdle() => !opening && !riteOpen && !rewarding && !paying;

    public int NextCost => cost;

    private void OnCommuneDenied()
    {
        if (!RiteIdle()) return;
        int price = ShownCost();
        TopScreenNotification.Show($"Not enough Zeal. The rite costs {price} and you have {ShownAmount()}.", 3f);
        Transform plate = costText.transform.parent;
        plate.DOComplete();
        plate.DOPunchScale(Vector3.one * 0.18f, 0.4f, 8, 0.6f).SetUpdate(true).SetLink(gameObject);
        costText.DOComplete();
        costText.color = Color.white;
        costText.DOColor(Shortfall, 0.45f).SetUpdate(true).SetLink(gameObject);
    }

    private bool CommuneAllowed()
    {
        if (!RiteIdle()) return false;
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

    private void BuildFeedback()
    {
        RectTransform eyeRect = (RectTransform)eye.transform;
        oddsText = BuildPlate("Odds", eyeRect, new Vector2(eyeRect.anchoredPosition.x - 40f, 800f), new Vector2(500f, 92f), 21f);
        eyePrompt = BuildPlate("Eye Prompt", eyeRect, new Vector2(eyeRect.anchoredPosition.x, 198f), new Vector2(480f, 54f), 20f);
    }

    private TMP_Text BuildPlate(string plateName, RectTransform anchor, Vector2 position, Vector2 size, float fontSize)
    {
        GameObject plate = new GameObject(plateName, typeof(RectTransform), typeof(Image));
        RectTransform rect = (RectTransform)plate.transform;
        rect.SetParent(anchor.parent, false);
        rect.anchorMin = anchor.anchorMin;
        rect.anchorMax = anchor.anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = plate.GetComponent<Image>();
        image.sprite = UiArt.Find("Papel_Con_Borde");
        image.color = new Color(1f, 1f, 1f, 0.94f);
        image.raycastTarget = false;

        GameObject label = new GameObject("Text", typeof(RectTransform));
        RectTransform labelRect = (RectTransform)label.transform;
        labelRect.SetParent(rect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(26f, 8f);
        labelRect.offsetMax = new Vector2(-26f, -8f);

        TextMeshProUGUI text = label.AddComponent<TextMeshProUGUI>();
        text.font = costText.font;
        text.fontSize = fontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = 12f;
        text.fontSizeMax = fontSize;
        text.color = Ink;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        plate.SetActive(false);
        return text;
    }

    private void RefreshFeedback()
    {
        if (oddsText == null || eyePrompt == null) return;

        bool live = riteOpen && !rewarding;
        RefreshOdds(live);
        RefreshPrompt(live);
        RefreshEyeIdle(live);
        SetTip(communeButton, CommuneTip());
        SetTip(offeringButton, StakeTip(false));
        SetTip(sacrificeButton, StakeTip(true));
    }

    private void RefreshOdds(bool live)
    {
        GameObject plate = oddsText.transform.parent.gameObject;
        plate.SetActive(live);
        if (!live) return;

        string line = OddsLine();
        if (line == oddsText.text) return;

        bool changed = !string.IsNullOrEmpty(oddsText.text);
        oddsText.text = line;
        if (!changed) return;
        plate.transform.DOComplete();
        plate.transform.DOPunchScale(Vector3.one * 0.06f, 0.3f, 6, 0.6f).SetUpdate(true).SetLink(plate);
    }

    private string OddsLine()
    {
        if (TutorialSession.FixedUnlock != null && TutorialSession.PendingDoctrine != null)
            return "<size=80%>THE EYE WILL GRANT</size>\nA doctrine for your cult";

        TheologyChances chances = CurrentChances();
        float total = Mathf.Max(0.01f, chances.Curse + chances.Blessing + chances.Seed + chances.Doctrine);
        return "<size=80%>THE EYE MAY GRANT</size>\n" + string.Join("   ",
            Chance("Curse", chances.Curse, total, "8B1A1A"),
            Chance("Blessing", chances.Blessing, total, "8A6400"),
            Chance("Seed", chances.Seed, total, "3D6B2A"),
            Chance("Doctrine", chances.Doctrine, total, "34407F"));
    }

    private static string Chance(string label, float value, float total, string hex)
    {
        return $"<color=#{hex}>{label} {Mathf.RoundToInt(value * 100f / total)}%</color>";
    }

    private void RefreshPrompt(bool live)
    {
        int touches = eyePhases != null ? eyePhases.Length - 1 : 0;
        GameObject plate = eyePrompt.transform.parent.gameObject;
        plate.SetActive(live && touches > 0);
        if (!plate.activeSelf) return;

        eyePrompt.text = stakesLocked
            ? $"Touch the eye to wake it  ({phase}/{touches})"
            : "Make offerings if you wish, then touch the eye";
    }

    private void RefreshEyeIdle(bool live)
    {
        bool wanted = live && eyePhases != null && phase < eyePhases.Length - 1 && !punching;
        if (wanted == eyeIdling) return;
        if (!wanted)
        {
            if (!punching) StopEyeIdle();
            return;
        }

        eyeIdling = true;
        eye.transform.localScale = Vector3.one;
        eye.transform.DOScale(1.04f, 0.9f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(gameObject);
    }

    private void StopEyeIdle()
    {
        eyeIdling = false;
        eye.transform.DOKill();
        eye.transform.localScale = Vector3.one;
        eye.transform.localRotation = Quaternion.identity;
    }

    private string CommuneTip()
    {
        if (riteOpen || opening) return "The rite is open. Touch the eye.";
        if (cost <= 0) return "Open the rite. This one is free.\n<size=80%>Then make offerings and touch the eye for a reward.</size>";
        if (!CommuneAllowed()) return $"Not enough Zeal.\n<size=80%>The rite costs {cost} Zeal and you have {ShownAmount()}. Zones you hold in the Ocularium bring the most Zeal.</size>";
        return $"Open the rite for {cost} Zeal.\n<size=80%>Each rite costs {costIncrease} more than the last.</size>";
    }

    private string StakeTip(bool sacrifice)
    {
        if (sacrifice && !SacrificeUnlocked()) return "Locked.\n<size=80%>Your cult must keep the Human Sacrifices doctrine.</size>";

        Resource resource = sacrifice ? Resource.Flock : Resource.Wealth;
        string effect = sacrifice
            ? "Moves the odds twice as far as an offering."
            : "Moves the odds away from curses, toward seeds and doctrines.";
        if (!riteOpen) return $"{(sacrifice ? "Sacrifice Flock" : "Offer Wealth")} once the rite is open.\n<size=80%>{effect}</size>";
        if (stakesLocked) return "Too late: you already touched the eye.";

        int next = TheologyOdds.NextCost(sacrifice ? sacrificeTier : offeringTier);
        if (next <= 0) return "The eye wants no more.";
        if (!CanAfford(resource, next)) return $"Not enough {resource}. You need {next}.";
        return $"{(sacrifice ? "Sacrifice" : "Offer")} {next} {resource}.\n<size=80%>{effect} Only before you touch the eye.</size>";
    }

    private static void SetTip(Button button, string text)
    {
        if (button == null) return;
        TooltipTrigger trigger = button.GetComponent<TooltipTrigger>();
        if (trigger == null) trigger = button.gameObject.AddComponent<TooltipTrigger>();
        trigger.SetText(text);
    }
}
