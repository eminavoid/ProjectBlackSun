using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Resource Icons", menuName = "Resources/Resource Icons", order = 1)]
public class ResourceIcons : ScriptableObject
{
    [Serializable]
    private struct Entry
    {
        public Resource resource;
        public Sprite icon;
    }

    [SerializeField] private List<Entry> icons = new List<Entry>();

    public Sprite Get(Resource resource)
    {
        for (int i = 0; i < icons.Count; i++)
        {
            if (icons[i].resource == resource) return icons[i].icon;
        }

        return null;
    }
}
