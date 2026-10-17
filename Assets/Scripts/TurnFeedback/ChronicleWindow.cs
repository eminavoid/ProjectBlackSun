using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChronicleWindow : MonoBehaviour
{
    [Serializable]
    private struct DistrictMarker
    {
        public Districts district;
        public Sprite sprite;
    }

    [SerializeField] private CanvasGroup group;
    [SerializeField] private RectTransform book;
    [SerializeField] private TMP_Text turnLabel;

    [Header("This Turn")]
    [SerializeField] private GameObject newsSection;
    [SerializeField] private TMP_Text newsText;

    [Header("Tithes")]
    [SerializeField] private GameObject tithesSection;
    [SerializeField] private TMP_Text tithesHeader;
    [SerializeField] private ResourceAmountRow tithes;
    [SerializeField] private string tithesTitle = "TITHES FROM YOUR DISTRICTS";
    [SerializeField] private string mixedTithesTitle = "GAINS AND LOSSES";

    [Header("Settled Events")]
    [SerializeField] private GameObject eventsSection;
    [SerializeField] private TMP_Text eventsHeader;
    [SerializeField] private ScrollRect eventsScroll;
    [SerializeField] private ChronicleEventEntry entryTemplate;
    [SerializeField] private List<DistrictMarker> markers = new List<DistrictMarker>();

    [Space]
    [SerializeField] private Button closeButton;
    [SerializeField, Min(0.01f)] private float openDuration = 0.35f;

    private readonly List<ChronicleEventEntry> entries = new List<ChronicleEventEntry>();
    private bool closing;

    private void Awake()
    {
        entryTemplate.gameObject.SetActive(false);
        closeButton.onClick.AddListener(Close);
    }

    public void Show(int turn, IReadOnlyList<string> news, IReadOnlyList<ResourceDelta> yield, IReadOnlyList<SettledEvent> settled)
    {
        turnLabel.text = $"TURN {turn}";

        newsSection.SetActive(news.Count > 0);
        newsText.text = string.Join("\n", news);

        tithesSection.SetActive(yield.Count > 0);
        tithesHeader.text = HasLoss(yield) ? mixedTithesTitle : tithesTitle;
        tithes.Show(yield);

        eventsSection.SetActive(settled.Count > 0);
        eventsHeader.text = $"SETTLED BY YOUR ADVISORS ({settled.Count})";
        for (int i = 0; i < settled.Count; i++)
        {
            ChronicleEventEntry entry = Instantiate(entryTemplate, entryTemplate.transform.parent);
            entry.gameObject.SetActive(true);
            entry.Bind(settled[i], MarkerFor(settled[i].Seed));
            entry.Clicked += Toggle;
            entries.Add(entry);
        }

        if (entries.Count == 1) entries[0].SetExpanded(true);

        StartCoroutine(ScrollToTop());
        Open();
    }

    private void Toggle(ChronicleEventEntry clicked)
    {
        bool expand = !clicked.IsExpanded;
        for (int i = 0; i < entries.Count; i++)
        {
            entries[i].SetExpanded(expand && entries[i] == clicked);
        }

        LayoutRebuilder.MarkLayoutForRebuild((RectTransform)entryTemplate.transform.parent);
    }

    private IEnumerator ScrollToTop()
    {
        yield return null;
        eventsScroll.verticalNormalizedPosition = 1f;
    }

    private Sprite MarkerFor(Seed seed)
    {
        Districts? district = seed != null ? seed.CurrentDistrict : null;
        if (!district.HasValue) return null;

        for (int i = 0; i < markers.Count; i++)
        {
            if (markers[i].district == district.Value) return markers[i].sprite;
        }

        return null;
    }

    private static bool HasLoss(IReadOnlyList<ResourceDelta> yield)
    {
        for (int i = 0; i < yield.Count; i++)
        {
            if (yield[i].Amount < 0) return true;
        }

        return false;
    }

    private void Open()
    {
        group.alpha = 0f;
        book.localScale = Vector3.one * 0.94f;
        DOTween.Sequence()
            .Join(group.DOFade(1f, openDuration))
            .Join(book.DOScale(1f, openDuration).SetEase(Ease.OutBack))
            .SetLink(gameObject).SetUpdate(true);
    }

    private void Close()
    {
        if (closing) return;
        closing = true;
        group.blocksRaycasts = false;

        float duration = openDuration * 0.6f;
        DOTween.Sequence()
            .Join(group.DOFade(0f, duration))
            .Join(book.DOScale(0.96f, duration).SetEase(Ease.InCubic))
            .OnComplete(() => Destroy(gameObject))
            .SetLink(gameObject).SetUpdate(true);
    }
}
