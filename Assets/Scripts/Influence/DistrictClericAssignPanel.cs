using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Asignación de clérigos del jugador a la zona seleccionada (flujo análogo a seeds).
/// Usa OnGUI para el Vertical Slice; se puede reemplazar por UI UGUI después.
/// </summary>
public class DistrictClericAssignPanel : MonoBehaviour
{
    [SerializeField] private bool visible = true;

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.cKey.wasPressedThisFrame) visible = !visible;
#else
        if (Input.GetKeyDown(KeyCode.C)) visible = !visible;
#endif
    }

    private void OnGUI()
    {
        if (!visible || InfluenceManager.IsNull) return;

        DistrictZone zone = DistrictSelectionController.SelectedZone;
        if (zone == null || !zone.IsPlayable) return;

        zone.EnsureInfluenceState();
        InfluenceManager manager = InfluenceManager.Get;
        ZoneInfluenceState state = zone.Influence;

        float width = 280f;
        float height = 120f;
        float x = (Screen.width - width) * 0.5f;
        float y = 340f;
        Rect rect = new Rect(x, y, width, height);
        OnGuiClickBlocker.RegisterGuiRect(rect);
        GUILayout.BeginArea(rect, GUI.skin.box);
        GUILayout.Label($"Clérigos → {zone.SectorName} (C)");
        GUILayout.Label($"Pool: {manager.GetClericPool(FactionId.Player)} | Zona: {state.GetClerics(FactionId.Player)}");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Asignar 1"))
        {
            manager.TryAssignClerics(zone, FactionId.Player, 1, out _);
        }

        if (GUILayout.Button("Retirar 1"))
        {
            manager.TryAssignClerics(zone, FactionId.Player, -1, out _);
        }
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }
}
