using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// OnGUI no pasa por EventSystem; registra rects para bloquear clicks de mapa.
/// Update corre antes que OnGUI, así que se usan los rects del frame anterior.
/// </summary>
public static class OnGuiClickBlocker
{
    private static readonly List<Rect> screenRects = new List<Rect>(8);
    private static int guiFrame = -1;

    /// <summary>Registra un rect en coordenadas GUI (origen arriba-izquierda), como GUILayout.BeginArea.</summary>
    public static void RegisterGuiRect(Rect guiRect)
    {
        if (guiFrame != Time.frameCount)
        {
            screenRects.Clear();
            guiFrame = Time.frameCount;
        }

        // Convert GUI (top-left) → screen (bottom-left) for Input System mouse positions.
        Rect screenRect = new Rect(
            guiRect.x,
            Screen.height - guiRect.y - guiRect.height,
            guiRect.width,
            guiRect.height);
        screenRects.Add(screenRect);
    }

    public static bool IsPointerOverBlockedArea(Vector2 screenPosition)
    {
        // OnGUI of frame N hasn't run during Update of frame N; accept previous frame.
        if (screenRects.Count == 0) return false;
        if (Time.frameCount - guiFrame > 1) return false;

        for (int i = 0; i < screenRects.Count; i++)
        {
            if (screenRects[i].Contains(screenPosition)) return true;
        }

        return false;
    }
}
