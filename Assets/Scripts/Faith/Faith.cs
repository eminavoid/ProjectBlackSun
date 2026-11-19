using UnityEngine;

[CreateAssetMenu(fileName = "Faith", menuName = "Faith/New Faith", order = 2)]
public class Faith : ScriptableObject
{
    [SerializeField, TextArea(3,3)] private string description;

    [Space]

    [Tooltip("When the flock / zeal ratio is below this, this faith type will not be selected unless this one is the minimum across all faith types.")]
    [SerializeField] private float minZealRatio;

    [Space]

    [SerializeReference, SerializeReferenceDropdown] private IFaithConsequence consequence;

    public string Name => name;
    public string Description => description;

    public float MinZealRatio => minZealRatio;

    public IFaithConsequence Consequence => consequence;
}