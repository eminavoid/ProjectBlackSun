using UnityEngine;

public class RotateOverTime : MonoBehaviour
{
    [SerializeField] private Vector3 degreesPerSecond = new Vector3(0f, 2.5f, 0f);

    private void Update() => transform.Rotate(degreesPerSecond * Time.deltaTime, Space.World);
}
