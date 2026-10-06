using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TheologyMenu : MonoBehaviour
{
    [SerializeField] private Resource costResource = Resource.Zeal;
    [SerializeField, Min(0)] private int startCost = 0;
    [SerializeField, Min(0)] private int costIncrease = 15;
    [SerializeField] private ResourceIcons icons;
    [SerializeField] private Unlocked unlockedSeeds;
    [SerializeField] private Unlocked unlockedDoctrines;

    [SerializeField] private RectTransform panel;
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

    private int cost;
    private int phase;

    private void Awake() => cost = startCost;

    private void OnEnable()
    {
        panel.localScale = Vector3.one * (((RectTransform)transform).rect.height / (panel.rect.height * (1f - panel.pivot.y)));
        closedArt.color = Color.white;
        openMask.enabled = true;
        openMask.gameObject.SetActive(false);
        communeButton.interactable = ResourceManager.Resources.GetResourceAmount(costResource) >= cost;
        phase = 0;
        eye.sprite = eyePhases[0];
        eyeNext.color = new Color(1f, 1f, 1f, 0f);
        foreach (Button plate in plates) plate.interactable = false;
        Refresh();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        DOTween.Kill(this);
        if (!ResourceManager.IsNull && ResourceManager.Sidebar != null) ResourceManager.Sidebar.Release(this);
    }

    public void Commune() => StartCoroutine(PayAndOpen());

    public void AdvanceEye()
    {
        if (phase == eyePhases.Length - 1 || DOTween.IsTweening(eyeNext)) return;

        eyeNext.sprite = eyePhases[++phase];
        eyeNext.DOFade(1f, 0.35f).SetId(this).OnComplete(() =>
        {
            eye.sprite = eyeNext.sprite;
            eyeNext.color = new Color(1f, 1f, 1f, 0f);
            if (phase == eyePhases.Length - 1) foreach (Button plate in plates) plate.interactable = true;
        });
    }

    private void Refresh()
    {
        costText.text = cost > 0 ? cost.ToString() : "FREE";
        costIcon.sprite = icons.Get(costResource);
        costIcon.gameObject.SetActive(cost > 0);
        seedsCount.text = unlockedSeeds.GetCopy().Count.ToString();
        doctrinesCount.text = unlockedDoctrines.GetCopy().Count.ToString();
    }

    private IEnumerator PayAndOpen()
    {
        communeButton.interactable = false;
        if (cost > 0)
        {
            Tween flight = ResourceManager.Sidebar.Send(this, costResource, -cost, costIcon.rectTransform, 0f);
            if (flight != null) yield return flight.WaitForCompletion();
            ResourceManager.Resources.AddResource(costResource, -cost);
            ResourceManager.Sidebar.Release(this);
        }

        cost += costIncrease;
        Refresh();

        openMask.gameObject.SetActive(true);
        openArt.anchoredPosition = new Vector2(closedArt.rectTransform.rect.width - openArt.rect.width, 0f);
        DOTween.Sequence()
            .Append(openArt.DOAnchorPosX(0f, 0.7f).SetEase(Ease.InOutCubic))
            .AppendCallback(() => openMask.enabled = false)
            .Append(closedArt.DOFade(0f, 0.2f))
            .SetId(this);
    }
}
