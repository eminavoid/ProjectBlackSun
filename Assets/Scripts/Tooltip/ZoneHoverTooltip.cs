using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Zeke.Tooltips
{
    /// <summary>
    /// Hover de un nodo del mapa, con el mismo Tooltip que el resto de la UI.
    /// </summary>
    public class ZoneHoverTooltip : MonoBehaviour
    {
        private static readonly List<RaycastResult> uiHits = new List<RaycastResult>(16);

        private static ZoneHoverTooltip current;
        private static bool ownsTooltip;

        private DistrictZone zone;

        private void Awake()
        {
            zone = GetComponent<DistrictZone>();
        }

        private void OnMouseEnter()
        {
            current = this;
            Show();
        }

        private void OnMouseOver()
        {
            if (current != this) current = this;
            Show();
        }

        private void OnMouseExit()
        {
            if (current != this) return;
            current = null;
            DismissOwned();
        }

        private void OnDisable()
        {
            if (current != this) return;
            current = null;
            DismissOwned();
        }

        public static void DismissOwned()
        {
            if (!ownsTooltip) return;
            ownsTooltip = false;
            if (!Tooltip.IsNull) Tooltip.Hide();
        }

        private void Show()
        {
            if (Tooltip.IsNull || zone == null || !zone.IsPlayable) return;

            if (NodeTooltipMenuBlock.IsAnyMenuOpen || IsPointerOverBlockingUi())
            {
                if (current == this) DismissOwned();
                return;
            }

            Tooltip.Show(BuildText(zone));
            ownsTooltip = true;
        }

        private static bool IsPointerOverBlockingUi()
        {
            Vector2 screenPosition = ReadPointer();
            if (EventSystem.current == null) return false;

            PointerEventData pointerData = new PointerEventData(EventSystem.current) { position = screenPosition };
            uiHits.Clear();
            EventSystem.current.RaycastAll(pointerData, uiHits);

            for (int i = 0; i < uiHits.Count; i++)
            {
                GameObject hit = uiHits[i].gameObject;
                if (hit == null || Tooltip.IsPartOfTooltip(hit)) continue;
                if (!hit.TryGetComponent(out Graphic graphic) || !graphic.raycastTarget) continue;
                return true;
            }

            return false;
        }

        private static Vector2 ReadPointer()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null) return Mouse.current.position.ReadValue();
            if (Pointer.current != null) return Pointer.current.position.ReadValue();
#endif
            return Input.mousePosition;
        }

        private static string BuildText(DistrictZone zone)
        {
            zone.EnsureInfluenceState();
            ZoneInfluenceState state = zone.Influence;

            string owner = "Unclaimed";
            if (state != null && state.Status == ZoneControlStatus.Controlled && state.Controller.HasValue)
            {
                owner = FactionIdUtil.DisplayName(state.Controller.Value);
            }

            return owner + "\n" + FormatGeneration(zone, state);
        }

        private static string FormatGeneration(DistrictZone zone, ZoneInfluenceState state)
        {
            if (zone == null || InfluenceManager.IsNull) return "—";

            DistrictProductionConfig config = InfluenceManager.Get.ProductionConfig;
            if (config == null || !config.TryGetEntry(zone.District, out DistrictProductionConfig.ProductionEntry entry))
            {
                return "—";
            }

            if (entry.isImperial) return "—";

            if (entry.usesPerZoneAmount)
            {
                int amount = zone.ProductionAmount;
                if (state != null && state.Status == ZoneControlStatus.Controlled && state.Controller.HasValue && state.TotalInfluence > 0)
                {
                    amount = DistrictProductionConfig.FloorShare(
                        zone.ProductionAmount,
                        state.GetShare(state.Controller.Value),
                        state.TotalInfluence);
                }

                return amount + " " + entry.primaryResource;
            }

            int primary = entry.primaryAmountPerZone;
            int secondary = entry.secondaryAmountPerZone;
            if (state != null && state.Status == ZoneControlStatus.Controlled && state.Controller.HasValue)
            {
                FactionId? districtOwner = InfluenceManager.Get.GetDistrictController(zone.District);
                if (districtOwner.HasValue)
                {
                    float multiplier = config.DistrictControlProductionMultiplier;
                    primary = Mathf.FloorToInt(primary * multiplier);
                    secondary = Mathf.FloorToInt(secondary * multiplier);
                }
            }

            string line = primary + " " + entry.primaryResource;
            if (secondary > 0) line += " + " + secondary + " " + entry.secondaryResource;
            return line;
        }
    }
}
