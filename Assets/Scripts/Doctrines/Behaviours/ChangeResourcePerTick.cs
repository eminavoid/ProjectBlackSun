using UnityEngine;

[CreateAssetMenu(fileName = "Doctrine Behaviour", menuName = "Doctrine Behaviours/Gain Stat Per Tick Behaviour", order = 1)]
public class ChangeResourcePerTick : DoctrineBehaviour
{
    [SerializeField] private PlayerResources resources;

    [SerializeField] private int amount;
    [SerializeField] private Resource resource;

    public override void OnTickStart()
    {
        resources.AddResource(resource, amount);
    }
}