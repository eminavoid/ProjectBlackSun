using UnityEngine;

public class CameraLOD : MonoBehaviour
{
    [Header("Building Visibility")]
    [SerializeField] private float maxBuldingY = 10f;
    [SerializeField] private float minBuildingY = 1f;

    [Space]

    [SerializeField] private GameObject buildings;

    private bool buildingsVisible = false;

    private void Awake()
    {
        buildingsVisible = buildings.activeSelf;
    }

    private void Update()
    {
        if (transform.position.y > maxBuldingY || transform.position.y < minBuildingY)
        {
            if (buildingsVisible)
            {
                DisableBuildings();
            }
        }
        else
        {
            if (!buildingsVisible)
            {
                EnableBuildings();
            }
        }
    }

    private void DisableBuildings()
    {
        buildingsVisible = false;
        buildings.SetActive(false);
    }

    private void EnableBuildings()
    {
        buildingsVisible = true;
        buildings.SetActive(true);
    }
}
