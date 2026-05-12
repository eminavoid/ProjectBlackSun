using UnityEngine;
using System;
using static PlayerStats;
using System.Collections.Generic;

[CreateAssetMenu(fileName="Doctrine", menuName="new Doctrine", order = 1)]
public class Doctrine : ScriptableObject
{
    [field: SerializeField] public string Name { get; private set; }
    [field: SerializeField] public string Description { get; private set; }

    [field: Space]

    [SerializeField] private List<StatUpdate> statUpdates;

    public List<StatUpdate> StatUpdates => statUpdates;

    [Serializable]
    public struct StatUpdate
    {
        public PlayerStat playerStat;
        public int changeAmount;
    }
}