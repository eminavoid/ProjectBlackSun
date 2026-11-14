using System.Collections.Generic;
using UnityEngine;

public class CameraLOD : MonoBehaviour
{
    [Header("Building Visibility")]
    [SerializeField] private float maxBuldingY = 10f;
    [SerializeField] private float minBuildingY = 1f;

    [Space]

    [SerializeField] private List<MeshRenderer> buildings;

    private bool buildingsVisible = false;

    private void Awake()
    {
        buildingsVisible = true;
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
        new List<MeshRenderer>(buildings).ForEach(renderer => renderer.enabled = false);
    }

    private void EnableBuildings()
    {
        buildingsVisible = true;
        new List<MeshRenderer>(buildings).ForEach(renderer => renderer.enabled = true);
    }
}
