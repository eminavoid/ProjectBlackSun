using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class MapStatsBook : MonoBehaviour
{
    [SerializeField] private GameObject window;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private RectTransform book;
    [SerializeField] private Button shade;
    [SerializeField] private Button closeButton;

    [Header("Tabs")]
    [SerializeField] private MapStatsTab[] tabs;
    [SerializeField] private Button cityButton;

    [Header("Seal")]
    [SerializeField] private Image seal;
    [SerializeField] private Color blindSeal = new Color(0.32f, 0.2f, 0.16f, 0.85f);
    [SerializeField] private TMP_Text sealCaption;
    [SerializeField] private TMP_Text sealName;

    [Header("Page")]
    [SerializeField] private TMP_Text pageKicker;
    [SerializeField] private TMP_Text pageTitle;
    [SerializeField] private TMP_Text pageStatus;
    [SerializeField] private ScrollRect scroll;
    [SerializeField] private RectTransform content;
    [SerializeField] private RectTransform pageTurn;

    [Header("Templates")]
    [SerializeField] private TMP_Text sectionTemplate;
    [SerializeField] private TMP_Text textTemplate;
    [SerializeField] private MapStatsEntry entryTemplate;
    [SerializeField] private Button actionTemplate;
    [Tooltip("Game terms in the page's texts link to their entry here and show it on hover.")]
    [SerializeField] private Zeke.Tooltips.TooltipLinksDB glossary;
    [Tooltip("Room left on each side of an action's label.")]
    [SerializeField, Min(0f)] private float actionPadding = 34f;

    [Header("Motion")]
    [SerializeField, Min(0.01f)] private float openDuration = 0.4f;
    [SerializeField, Min(0.01f)] private float turnDuration = 0.32f;
    [SerializeField, Min(0f)] private float rowStagger = 0.035f;

    [Header("Sounds")]
    [SerializeField] private string openEvent = "Play_UI_EventPopup";
    [SerializeField] private string turnEvent = "Play_UI_Click_Card";
    [SerializeField] private string stampEvent = "Play_UI_Outcome";

    private readonly List<CanvasGroup> rows = new List<CanvasGroup>();
    private Vector2 bookRest;
    private Sequence motion;

    public event Action<Districts?> PageRequested;
    public bool IsOpen { get; private set; }

    private void Awake()
    {
        bookRest = book.anchoredPosition;
        window.SetActive(false);
        NodeTooltipMenuBlock.Ensure(window);

        closeButton.onClick.AddListener(Close);
        shade.onClick.AddListener(Close);
        cityButton.onClick.AddListener(() => PageRequested?.Invoke(null));
        foreach (MapStatsTab tab in tabs)
        {
            Districts district = tab.District;
            tab.Button.onClick.AddListener(() => PageRequested?.Invoke(district));
        }
    }

    public Sprite MarkerFor(Districts district)
    {
        foreach (MapStatsTab tab in tabs)
        {
            if (tab.District == district) return tab.Sprite;
        }

        return null;
    }

    public void Open(Districts? district)
    {
        if (IsOpen) return;
        IsOpen = true;
        transform.SetAsLastSibling();
        window.SetActive(true);
        SelectTab(district);

        group.alpha = 0f;
        group.blocksRaycasts = true;
        book.localScale = Vector3.one * 0.94f;
        book.anchoredPosition = bookRest + Vector2.down * 36f;

        motion?.Kill();
        motion = DOTween.Sequence()
            .Join(group.DOFade(1f, openDuration * 0.6f))
            .Join(book.DOScale(1f, openDuration).SetEase(Ease.OutBack))
            .Join(book.DOAnchorPos(bookRest, openDuration).SetEase(Ease.OutCubic))
            .SetLink(gameObject);

        for (int i = 0; i < tabs.Length; i++)
        {
            if (tabs[i].gameObject.activeInHierarchy) tabs[i].Rise(openDuration * 0.55f + i * 0.05f);
        }

        Play(openEvent);
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        group.blocksRaycasts = false;

        float duration = openDuration * 0.5f;
        motion?.Kill();
        motion = DOTween.Sequence()
            .Join(group.DOFade(0f, duration))
            .Join(book.DOScale(0.96f, duration).SetEase(Ease.InCubic))
            .Join(book.DOAnchorPos(bookRest + Vector2.down * 24f, duration).SetEase(Ease.InCubic))
            .OnComplete(() => window.SetActive(false))
            .SetLink(gameObject);
    }

    public void SelectTab(Districts? district)
    {
        foreach (MapStatsTab tab in tabs)
        {
            tab.SetSelected(district.HasValue && tab.District == district.Value);
        }

        cityButton.gameObject.SetActive(district.HasValue);
    }

    public void SetHeading(string kicker, string title, string status)
    {
        pageKicker.text = kicker;
        pageKicker.gameObject.SetActive(!string.IsNullOrEmpty(kicker));
        pageTitle.text = title;
        pageStatus.text = status;
    }

    public void SetSeal(FactionId? faction, string caption, string name, bool stamp)
    {
        sealCaption.text = caption;
        sealName.text = name;

        RectTransform rect = seal.rectTransform;
        rect.DOKill();
        seal.DOKill();
        rect.localScale = Vector3.one;
        seal.color = faction.HasValue ? FactionPalette.For(faction.Value) : blindSeal;
        if (!faction.HasValue || !stamp) return;

        Color target = seal.color;
        seal.color = new Color(target.r, target.g, target.b, 0f);
        rect.localScale = Vector3.one * 1.8f;
        DOTween.Sequence()
            .AppendInterval(openDuration * 0.6f)
            .Append(rect.DOScale(1f, 0.16f).SetEase(Ease.InQuad))
            .Join(seal.DOFade(1f, 0.1f))
            .AppendCallback(() => Play(stampEvent))
            .Append(rect.DOPunchScale(Vector3.one * 0.08f, 0.3f, 6, 0.5f))
            .SetLink(seal.gameObject);
    }

    public void BeginPage(bool turn)
    {
        foreach (CanvasGroup row in rows)
        {
            if (row != null) Destroy(row.gameObject);
        }

        rows.Clear();
        if (turn) TurnPage();
    }

    public void AddSection(string text) => Spawn(sectionTemplate).text = text;

    public void AddText(string text) => Spawn(textTemplate).text = Link(text);

    public void AddEntry(Sprite icon, Color swatch, string title, string detail, UnityAction onClick = null)
    {
        Spawn(entryTemplate).Bind(icon, swatch, Link(title), Link(detail), onClick);
    }

    public void AddAction(string label, UnityAction onClick)
    {
        Button button = Spawn(actionTemplate);
        TMP_Text text = button.GetComponentInChildren<TMP_Text>();
        text.text = label;
        FitAction(button, text);
        button.onClick.AddListener(onClick);
    }

    private string Link(string text) => glossary != null ? glossary.Highlight(text) : text;

    private void FitAction(Button button, TMP_Text label)
    {
        RectTransform rect = (RectTransform)button.transform;
        float template = ((RectTransform)actionTemplate.transform).rect.width;
        float room = content.rect.width > 0f ? content.rect.width : float.MaxValue;
        float needed = label.GetPreferredValues(label.text, Mathf.Infinity, Mathf.Infinity).x + actionPadding * 2f;
        float width = Mathf.Min(Mathf.Max(template, needed), room);

        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        if (button.TryGetComponent(out LayoutElement layout)) layout.preferredWidth = width;

        if (needed <= width) return;
        label.fontSizeMax = label.fontSize;
        label.fontSizeMin = label.fontSize * 0.7f;
        label.enableAutoSizing = true;
    }

    public void EndPage(bool animate)
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        if (!animate) return;

        scroll.StopMovement();
        scroll.verticalNormalizedPosition = 1f;
        float start = turnDuration * 0.5f;
        for (int i = 0; i < rows.Count; i++)
        {
            CanvasGroup row = rows[i];
            row.alpha = 0f;
            row.DOFade(1f, 0.25f).SetDelay(start + Mathf.Min(i * rowStagger, 0.6f)).SetLink(row.gameObject);
        }
    }

    private T Spawn<T>(T template) where T : Component
    {
        T clone = Instantiate(template, content);
        clone.gameObject.SetActive(true);
        rows.Add(clone.GetComponent<CanvasGroup>());
        return clone;
    }

    private void TurnPage()
    {
        pageTurn.DOKill();
        pageTurn.gameObject.SetActive(true);
        pageTurn.localScale = Vector3.one;
        pageTurn.DOScaleX(0f, turnDuration)
            .SetEase(Ease.InQuad)
            .OnComplete(() => pageTurn.gameObject.SetActive(false))
            .SetLink(pageTurn.gameObject);
        Play(turnEvent);
    }

    private static void Play(string eventName)
    {
        if (!string.IsNullOrEmpty(eventName) && AudioManager.Instance != null) AudioManager.Instance.PlayUIEvent(eventName);
    }
}
