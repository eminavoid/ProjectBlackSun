using UnityEngine;

public static class MapViewLod
{
    public const float DetailMaxHeight = 1.5f;

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

    public static bool IsClose => CameraHeight <= DetailMaxHeight;
}
