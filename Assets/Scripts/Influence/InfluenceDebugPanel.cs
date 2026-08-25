using System.Text;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// UI/debug OnGUI: asignación de clérigos del jugador + score Faith Eclipse.
/// </summary>
public class InfluenceDebugPanel : MonoBehaviour
{
    [SerializeField] private bool showPanel = true;
    [SerializeField] private bool showFaithEclipse = true;

    private string statusMessage = string.Empty;

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.iKey.wasPressedThisFrame) showPanel = !showPanel;
#else
        if (Input.GetKeyDown(KeyCode.I)) showPanel = !showPanel;
#endif
    }

    private void OnGUI()
    {
        if (!showPanel) return;

        GUIStyle box = new GUIStyle(GUI.skin.box)
        {
            fontSize = 14,
            alignment = TextAnchor.UpperLeft,
            wordWrap = true
        };
        box.normal.textColor = Color.white;

        float width = 420f;
        float height = showFaithEclipse ? 260f : 200f;
        // Centered so side UIs (resources / doctrines) stay clickable.
        float x = (Screen.width - width) * 0.5f;
        float y = 72f;
        Rect rect = new Rect(x, y, width, height);
        OnGuiClickBlocker.RegisterGuiRect(rect);
        GUILayout.BeginArea(rect, box);

        GUILayout.Label("Influencia (I para ocultar)");

        if (InfluenceManager.IsNull)
        {
            GUILayout.Label("InfluenceManager no disponible.");
            GUILayout.EndArea();
            return;
        }

        InfluenceManager manager = InfluenceManager.Get;
        int pool = manager.GetClericPool(FactionId.Player);
        GUILayout.Label($"Clérigos disponibles: {pool}");

        DistrictZone zone = DistrictSelectionController.SelectedZone;
        if (zone == null || !zone.IsPlayable)
        {
            GUILayout.Label("Seleccioná una zona del mapa.");
        }
        else
        {
            zone.EnsureInfluenceState();
            ZoneInfluenceState state = zone.Influence;
            GUILayout.Label($"{zone.SectorName} ({zone.District})");
            GUILayout.Label(state.FormatDebugLine());
            GUILayout.Label($"Tus clérigos aquí: {state.GetClerics(FactionId.Player)} | share: {state.GetShare(FactionId.Player)} ({state.GetSharePercent(FactionId.Player):0}%)");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+1 Clérigo")) Assign(zone, 1);
            if (GUILayout.Button("+5")) Assign(zone, 5);
            if (GUILayout.Button("-1")) Assign(zone, -1);
            if (GUILayout.Button("-5")) Assign(zone, -5);
            GUILayout.EndHorizontal();

            GUILayout.Label(BuildPresenceLine(state));
        }

        if (!string.IsNullOrEmpty(statusMessage))
        {
            GUILayout.Label(statusMessage);
        }

        if (showFaithEclipse)
        {
            GUILayout.Space(6f);
            GUILayout.Label(BuildFaithEclipseBlock(manager));
        }

        if (!FanaticDefensePending.Enabled)
        {
            GUILayout.Label("(Fanáticos: pendiente GDD)");
        }

        GUILayout.EndArea();
    }

    private void Assign(DistrictZone zone, int delta)
    {
        if (InfluenceManager.Get.TryAssignClerics(zone, FactionId.Player, delta, out string error))
        {
            statusMessage = delta > 0 ? $"Asignados +{delta}." : $"Retirados {-delta}.";
        }
        else
        {
            statusMessage = error;
        }
    }

    private static string BuildPresenceLine(ZoneInfluenceState state)
    {
        StringBuilder sb = new StringBuilder("Presencia: ");
        bool any = false;
        foreach (FactionId faction in FactionIdUtil.All)
        {
            int share = state.GetShare(faction);
            int clerics = state.GetClerics(faction);
            if (share <= 0 && clerics <= 0) continue;
            if (any) sb.Append(" | ");
            sb.Append($"{FactionIdUtil.ShortLabel(faction)} s{share}/c{clerics}");
            any = true;
        }

        if (!any) sb.Append("(vacía)");
        return sb.ToString();
    }

    private static string BuildFaithEclipseBlock(InfluenceManager manager)
    {
        StringBuilder sb = new StringBuilder("Faith Eclipse (zonas Controlled):\n");
        foreach (FactionId faction in FactionIdUtil.All)
        {
            sb.AppendLine($"  {FactionIdUtil.DisplayName(faction)}: {manager.CountControlledZones(faction)}");
        }

        FactionId? leader = manager.GetFaithEclipseLeader(out int leading);
        if (leader.HasValue)
        {
            sb.Append($"Líder: {FactionIdUtil.DisplayName(leader.Value)} ({leading})");
        }
        else
        {
            sb.Append("Líder: (empate / nadie)");
        }

        return sb.ToString();
    }
}
