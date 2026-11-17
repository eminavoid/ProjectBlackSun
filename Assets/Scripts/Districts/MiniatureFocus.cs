using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Volume))]
public class MiniatureFocus : MonoBehaviour
{
    [Tooltip("Height of the map surface the camera focuses on.")]
    [SerializeField] private float groundHeight = 0.22f;
    [SerializeField, Min(1f)] private float focalLength = 150f;
    [Tooltip("Aperture when the focus is at the reference distance. Closer views stop down so the blur stays about the same on screen.")]
    [SerializeField, Min(1f)] private float referenceAperture = 2.4f;
    [SerializeField, Min(0.1f)] private float referenceDistance = 5f;
    [Tooltip("How much sharper the view gets as you zoom in past the reference distance. 1 keeps the blur the same at every zoom.")]
    [SerializeField, Min(1f)] private float zoomSharpening = 1.5f;

    private DepthOfField depthOfField;
    private Camera mapCamera;

    private void Start()
    {
        Volume volume = GetComponent<Volume>();
        if (!volume.profile.TryGet(out depthOfField))
        {
            enabled = false;
            return;
        }

        depthOfField.mode.Override(DepthOfFieldMode.Bokeh);
        depthOfField.focalLength.Override(focalLength);
        depthOfField.focusDistance.Override(referenceDistance);
        depthOfField.aperture.Override(referenceAperture);
    }

    private void LateUpdate()
    {
        if (mapCamera == null) mapCamera = Camera.main;
        if (mapCamera == null) return;

        Ray ray = mapCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (ray.direction.y >= -0.01f) return;

        float distance = (groundHeight - ray.origin.y) / ray.direction.y;
        if (distance <= 0f) return;

        depthOfField.focusDistance.value = distance;
        depthOfField.aperture.value = Mathf.Clamp(referenceAperture * Mathf.Pow(referenceDistance / distance, zoomSharpening), 1f, 32f);
    }
}
