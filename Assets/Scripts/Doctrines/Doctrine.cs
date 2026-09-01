using UnityEngine;
using System;
using static PlayerStats;
using System.Collections.Generic;

[CreateAssetMenu(fileName="Doctrine", menuName="new Doctrine", order = 1)]
public class Doctrine : ScriptableObject
{
    [field: SerializeField] public string Name { get; private set; }
    [field: SerializeField, TextArea(10, 10)] public string Description { get; private set; }

    [field: Space]

    [SerializeField] private List<StatUpdate> statUpdates;
    [SerializeField] private List<DoctrineBehaviour> behaviours;

    public void OnTickStart() => behaviours.ForEach(behaviour => behaviour.OnTickStart());
    public void OnTickEnd() => behaviours.ForEach(behaviour => behaviour.OnTickEnd());

    public List<StatUpdate> StatUpdates => statUpdates;

    [Serializable]
    public struct StatUpdate
    {
        public PlayerStat playerStat;
        public int changeAmount;
    }
}