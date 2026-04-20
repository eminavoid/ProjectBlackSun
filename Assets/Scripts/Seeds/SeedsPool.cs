using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Seeds Pool", menuName = "Seeds/New Seeds Pool", order = 1)]
public class SeedsPool : ScriptableObject
{
    [field: SerializeField] public List<Seed> EvilSeeds; //fuck you unity for not making hashSet serializable
}