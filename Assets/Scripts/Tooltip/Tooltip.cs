using DG.Tweening;
using TMPro;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Zeke.Tooltips
{
    public class Tooltip : Singleton<Tooltip>
    {
        [Header("Dependency")]
        [SerializeField] private GameObject tooltipObject;
        [SerializeField] private RectTransform tooltipRect;
        [SerializeField] private TMP_Text tooltipText;

        [Header("Tooltip")]
        [SerializeField] private Vector2 maxSize = new Vector2(400f, 400f);

        [Space]

        [SerializeField] private float showDelay = 1f;
        [SerializeField, Min(0.01f)] private float appearDuration = 0.14f;
        [SerializeField, Min(0.01f)] private float hideDuration = 0.08f;

        private const float CursorHeight = 28f;

        private State state = State.Hide;
        private float timer = 0f;
        private CanvasGroup group;
        private bool hiding;

        private enum State
        {
            Show,
            Hide,
        }

        public static void SetText(string text)
        {
            Instance.tooltipText.text = text;
            Vector2 sizeDelta = Vector2.zero;

            Vector2 textSize = Instance.tooltipText.GetPreferredValues(text, Instance.maxSize.x, Instance.maxSize.y);

            sizeDelta.x = Mathf.Clamp(textSize.x, 0f, Instance.maxSize.x);
            sizeDelta.y = Mathf.Clamp(textSize.y, 0f, Instance.maxSize.y);

            Instance.tooltipRect.sizeDelta = sizeDelta;
        }

        public static void Show(string text)
        {
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

        private void Awake()
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
            Vector2 pointer = ReadPointer();
            float height = tooltipRect.rect.height * tooltipRect.lossyScale.y;

            // Lower-left corner sits on the cursor, so the box grows up and to the right.
            bool fitsAbove = pointer.y + height <= Screen.height;
            tooltipRect.pivot = fitsAbove ? Vector2.zero : Vector2.up;

            Vector3 position = tooltipRect.position;
            position.x = pointer.x;
            position.y = fitsAbove ? pointer.y : pointer.y - CursorHeight;
            tooltipRect.position = position;

            ClampToScreen(tooltipRect);
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
                    Appear(true);
                }
            }
        }
    }
}