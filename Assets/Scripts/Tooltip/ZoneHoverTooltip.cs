using UnityEngine;

namespace Zeke.Tooltips
{
    /// <summary>
    /// Hover de un nodo del mapa, con el mismo Tooltip que el resto de la UI.
    /// </summary>
    public class ZoneHoverTooltip : MonoBehaviour
    {
        private static ZoneHoverTooltip current;

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
            if (!Tooltip.IsNull) Tooltip.Hide();
        }

        private void OnDisable()
        {
            if (current != this) return;
            current = null;
            if (!Tooltip.IsNull) Tooltip.Hide();
        }

        private void Show()
        {
            if (Tooltip.IsNull || zone == null || !zone.IsPlayable) return;
            Tooltip.Show(BuildText(zone));
        }

        private static string BuildText(DistrictZone zone)
        {
            zone.EnsureInfluenceState();
            ZoneInfluenceState state = zone.Influence;

            string owner = "Nadie";
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
