using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Saved touches, keyed by zone object name. DistrictZone is added at runtime, so the links cannot live only on that component.
/// </summary>
[CreateAssetMenu(fileName = "Zone Travel Catalog", menuName = "Influence/Zone Travel Catalog", order = 3)]
public class ZoneTravelCatalog : ScriptableObject
{
    [Serializable]
    public class Record
    {
        public string fromZone;
        public string toZone;
        public float gap;
        public TravelEdgeKind kind;
    }

    [SerializeField] private List<Record> links = new List<Record>();

    public IReadOnlyList<Record> Links => links;

    public void SetLinks(List<Record> records)
    {
        links = records ?? new List<Record>();
    }
}
