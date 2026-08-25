using UnityEngine;

public class LoseWealthDebug : MonoBehaviour
{
    [SerializeField] private PlayerResources resources;
    [SerializeField] private KeyCode key;

    private void Update()
    {
        if (Input.GetKeyDown(key))
        {
            resources.AddResource(Resource.Wealth, -10000);
        }
    }
}
