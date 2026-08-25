using UnityEngine;

/// <summary>
/// Corte por altura de cámara: de cerca overlay (sin flechas); de lejos overlay + flechas.
/// El texto de stats vive en la zona seleccionada, no en este LOD.
/// </summary>
public static class MapViewLod
{
    public const float DefaultDetailMaxHeight = 3f;

    public static float CameraHeight
    {
        get
        {
            if (MapCameraController.Instance != null)
            {
                return MapCameraController.Instance.transform.position.y;
            }

            Camera cam = Camera.main;
            return cam != null ? cam.transform.position.y : 10f;
        }
    }

    public static bool IsClose(InfluenceOverlaySettings settings)
    {
        float cutoff = settings != null ? settings.detailMaxHeight : DefaultDetailMaxHeight;
        return CameraHeight <= cutoff;
    }

    public static void Weights(InfluenceOverlaySettings settings, out float arrows, out float detail)
    {
        bool close = IsClose(settings);
        detail = close ? 1f : 0f;
        arrows = close ? 0f : 1f;
    }
}
