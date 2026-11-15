using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using System;

namespace Zeke.Tooltips
{
    public class Tooltip : Singleton<Tooltip>
    {
        [Header("Dependency")]
        [SerializeField] private GameObject tooltipObject;
        [SerializeField] private RectTransform tooltipRect;
        [SerializeField] private TMP_Text tooltipText;
        [Tooltip("Game terms that menus built at runtime can link to.")]
        [SerializeField] private TooltipLinksDB glossary;

        [Space]

        [SerializeField] private float showDelay = 1f;
        [SerializeField, Min(0.01f)] private float appearDuration = 0.14f;
        [SerializeField, Min(0.01f)] private float hideDuration = 0.08f;

        [Header("Layout")]
        [Tooltip("The box grows with its text up to this width, then wraps.")]
        [SerializeField, Min(50f)] private float maxWidth = 400f;
        [Tooltip("Space between a UI element and the tooltip shown beside it.")]
        [SerializeField, Min(0f)] private float anchorGap = 10f;
        [Tooltip("Shift from the cursor for tooltips that follow it, so the arrow doesn't cover the box.")]
        [SerializeField] private Vector2 cursorOffset = new Vector2(14f, 10f);

        public static TooltipLinksDB Glossary => IsNull ? null : Instance.glossary;

        public static Action onTooltipShow;
        public static Action onTooltipHide;

        private const float CursorHeight = 28f;

        private State state = State.Hide;
        private float timer = 0f;
        private CanvasGroup group;
        private bool hiding;
        private RectTransform anchor;

        private enum State
        {
            Show,
            Hide,
        }

        public static void SetText(string text)
        {
            Instance.tooltipText.text = text;
            if (Instance.tooltipObject.activeSelf) Instance.Refit();
        }

        public static void Show(string text, RectTransform besides = null)
        {
            Instance.anchor = besides;
            SetText(text);
            Instance.state = State.Show;
            if (Instance.hiding) Instance.Appear(false);
        }

        public static void Hide()
        {
            Instance.timer = 0f;
            Instance.state = State.Hide;
            Instance.Disappear();
        }

        public static bool IsPartOfTooltip(GameObject candidate)
        {
            if (IsNull || candidate == null) return false;

            Transform root = Instance.transform;
            Transform hit = candidate.transform;
            return hit == root || hit.IsChildOf(root);
        }

        protected override void OnInitialization()
        {
            group = tooltipObject.GetComponent<CanvasGroup>();
            if (group == null) group = tooltipObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            tooltipObject.SetActive(false);
        }

        private void Update()
        {
            UpdateState();

            if (tooltipObject.activeSelf)
            {
                UpdatePosition();
            }
        }

        private void UpdatePosition()
        {
            if (anchor != null && anchor.gameObject.activeInHierarchy) PlaceBeside(anchor);
            else PlaceAtPointer();

            ClampToScreen(tooltipRect);
        }

        private void PlaceAtPointer()
        {
            Vector2 pointer = ReadPointer();
            float height = tooltipRect.rect.height * tooltipRect.lossyScale.y;
            bool fitsAbove = pointer.y + cursorOffset.y + height <= Screen.height;

            if (fitsAbove) Place(Vector2.zero, pointer.x + cursorOffset.x, pointer.y + cursorOffset.y);
            else Place(Vector2.up, pointer.x + cursorOffset.x, pointer.y - CursorHeight);
        }

        private void PlaceBeside(RectTransform target)
        {
            Bounds footprint = RectTransformUtility.CalculateRelativeRectTransformBounds(target);
            Vector3 min = target.TransformPoint(footprint.min);
            Vector3 max = target.TransformPoint(footprint.max);
            float left = min.x;
            float bottom = min.y;
            float right = max.x;
            float top = max.y;
            float center = (left + right) * 0.5f;
            bool onSideEdge = center < Screen.width * 0.15f || center > Screen.width * 0.85f;

            if (!onSideEdge && top > Screen.height * 0.85f) Place(Vector2.up, left, bottom - anchorGap);
            else if (!onSideEdge && bottom < Screen.height * 0.15f) Place(Vector2.zero, left, top + anchorGap);
            else if (center < Screen.width * 0.5f) Place(Vector2.up, right + anchorGap, top);
            else Place(Vector2.one, left - anchorGap, top);
        }

        private void Place(Vector2 pivot, float x, float y)
        {
            tooltipRect.pivot = pivot;
            tooltipRect.position = new Vector3(x, y, tooltipRect.position.z);
        }

        private void Refit()
        {
            FitWidth();
            tooltipText.ForceMeshUpdate();
            LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRect);
            UpdatePosition();
        }

        private void FitWidth()
        {
            float padding = tooltipText.margin.x + tooltipText.margin.z;
            if (tooltipRect.TryGetComponent(out LayoutGroup layout)) padding += layout.padding.horizontal;

            float textWidth = tooltipText.GetPreferredValues(tooltipText.text, maxWidth - padding, 0f).x;
            tooltipRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Min(maxWidth, Mathf.Ceil(textWidth) + padding));
        }

        private static Vector2 ReadPointer()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null) return Mouse.current.position.ReadValue();
#endif
            return Input.mousePosition;
        }

        private void ClampToScreen(RectTransform rectTransform)
        {
            Vector2 size = Vector2.Scale(rectTransform.rect.size, rectTransform.lossyScale);

            Vector3 position = rectTransform.position;
            Vector2 pivot = rectTransform.pivot;

            position.x = Mathf.Clamp(position.x, size.x * pivot.x, Screen.width - size.x * (1f - pivot.x));
            position.y = Mathf.Clamp(position.y, size.y * pivot.y, Screen.height - size.y * (1f - pivot.y));

            rectTransform.position = position;
        }

        private void Appear(bool grow)
        {
            hiding = false;
            group.DOKill();
            tooltipRect.DOKill();
            if (grow)
            {
                group.alpha = 0f;
                tooltipRect.localScale = Vector3.one * 0.9f;
            }

            tooltipRect.DOScale(1f, appearDuration).SetEase(Ease.OutBack).SetUpdate(true).SetLink(tooltipObject);
            group.DOFade(1f, appearDuration).SetUpdate(true).SetLink(tooltipObject);
        }

        private void Disappear()
        {
            if (!tooltipObject.activeSelf || hiding) return;
            hiding = true;
            group.DOKill();
            tooltipRect.DOKill();
            tooltipRect.DOScale(0.95f, hideDuration).SetUpdate(true).SetLink(tooltipObject);
            group.DOFade(0f, hideDuration).SetUpdate(true).SetLink(tooltipObject).OnComplete(() =>
            {
                hiding = false;
                onTooltipHide?.Invoke();
                tooltipObject.SetActive(false);
            });
        }

        private void UpdateState()
        {
            if (state == State.Show && !tooltipObject.activeSelf)
            {
                timer += Time.deltaTime;

                if (timer > showDelay)
                {
                    tooltipObject.SetActive(true);
                    Refit();
                    onTooltipShow?.Invoke();
                    Appear(true);
                }
            }
        }
    }
}