using UnityEngine;
using UnityEngine.Events;

public class DebugCodexLoader : MonoBehaviour
{
    [SerializeField] private UnityEvent unityEvent;
    [SerializeField] private float delay = 7f;

    private float timer = 0f;

    private void Update()
    {
        timer += Time.deltaTime;

        if (timer > delay)
        {
            unityEvent?.Invoke();
            enabled = false;
        }
    }
}