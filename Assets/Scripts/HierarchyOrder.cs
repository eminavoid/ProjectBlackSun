using UnityEngine;

public class HierarchyOrder : MonoBehaviour
{
    [SerializeField] private int order;

    private void Awake()
    {
        transform.SetSiblingIndex(order);
    }
}