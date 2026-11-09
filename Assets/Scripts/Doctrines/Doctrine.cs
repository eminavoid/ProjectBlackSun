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

    public void OnTickStart()
    {
        if (behaviours == null) return;
        behaviours.ForEach(behaviour => behaviour.OnTickStart());
    }

    public void OnTickEnd()
    {
        if (behaviours == null) return;
        behaviours.ForEach(behaviour => behaviour.OnTickEnd());
    }

    public List<StatUpdate> StatUpdates => statUpdates;

    public static Doctrine CreateRuntime(string doctrineName, string doctrineDescription, PlayerStat stat, int amount)
    {
        Doctrine doctrine = CreateInstance<Doctrine>();
        doctrine.Name = doctrineName;
        doctrine.Description = doctrineDescription;
        doctrine.statUpdates = new List<StatUpdate>
        {
            new StatUpdate { playerStat = stat, changeAmount = amount }
        };
        doctrine.behaviours = new List<DoctrineBehaviour>();
        doctrine.name = doctrineName;
        return doctrine;
    }

    [Serializable]
    public struct StatUpdate
    {
        public PlayerStat playerStat;
        public int changeAmount;
    }
}