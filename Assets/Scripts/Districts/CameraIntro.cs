using DG.Tweening;
using UnityEngine;

[RequireComponent(typeof(MapCameraController))]
public class CameraIntro : MonoBehaviour
{
    [SerializeField] private Vector3 offset = new Vector3(0f, 2.6f, -1.8f);
    [SerializeField, Min(0f)] private float delay = 0.25f;
    [SerializeField, Min(0.01f)] private float duration = 2.8f;

    private void Start()
    {
        var controller = GetComponent<MapCameraController>();
        Vector3 rest = transform.position;
        controller.enabled = false;
        transform.position = rest + offset;
        transform.DOMove(rest, duration)
            .SetDelay(delay)
            .SetEase(Ease.InOutCubic)
            .SetLink(gameObject)
            .OnComplete(() => controller.enabled = true);
    }
}
