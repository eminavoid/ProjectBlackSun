using UnityEngine.InputSystem;
using UnityEngine;
using System;
using TMPro;

namespace Zeke.Tooltips
{
    [RequireComponent(typeof(TMP_Text))]
    public class HoverLinkHandlerTMPro : MonoBehaviour, ILinkHoverEvents
    {
        public Action<LinkHoverEventInfo> OnHoverEnterLink { get; set; }
        public Action<LinkHoverEventInfo> OnHoverStayLink { get; set; }
        public Action<LinkHoverEventInfo> OnHoverExitLink { get; set; }

        private RectTransform rectTransform;
        private TMP_Text tmpText;
        private Canvas canvas;
        private Camera camera;

        private LinkHoverEventInfo linkHoverEventInfo;
        private int currentLinkIndex = -1;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            tmpText = GetComponent<TMP_Text>();
            canvas = GetComponentInParent<Canvas>();
            camera = GetCamera(canvas);
        }

        private Camera GetCamera(Canvas canvas)
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
            return canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
        }

        private void Update()
        {
            Vector2 mousePosition = Mouse.current.position.ReadValue();

            if (!IsInsideRect(rectTransform, mousePosition))
            {
                if (currentLinkIndex != -1)
                {
                    OnHoverExitLink?.Invoke(linkHoverEventInfo);
                    currentLinkIndex = -1;
                }

                return;
            }

            if (TryGetIntersectingLink(tmpText, mousePosition, out int linkIndex))
            {  
                TMP_LinkInfo linkInfo = tmpText.textInfo.linkInfo[linkIndex];
                linkHoverEventInfo = new LinkHoverEventInfo(linkInfo.GetLinkID(), mousePosition);

                if (currentLinkIndex != -1 && currentLinkIndex != linkIndex)
                {
                    OnHoverExitLink?.Invoke(linkHoverEventInfo);
                }

                if (currentLinkIndex != linkIndex)
                {
                    OnHoverEnterLink?.Invoke(linkHoverEventInfo);
                    currentLinkIndex = linkIndex;
                }

                OnHoverStayLink?.Invoke(linkHoverEventInfo);
            }
            else if (currentLinkIndex != -1)
            {
                OnHoverExitLink?.Invoke(linkHoverEventInfo);
                currentLinkIndex = linkIndex;
            }
        }

        private bool IsInsideRect(RectTransform rect, Vector2 mousePosition)
        {
            return TMP_TextUtilities.IsIntersectingRectTransform(rect, mousePosition, camera);
        }

        private bool TryGetIntersectingLink(TMP_Text text, Vector2 mousePosition, out int linkIndex)
        {
            linkIndex = TMP_TextUtilities.FindIntersectingLink(text, mousePosition, camera);

            return linkIndex >= 0;
        }
    }
}