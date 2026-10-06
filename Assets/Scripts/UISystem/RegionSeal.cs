using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class RegionSeal : MonoBehaviour
{
    [Serializable]
    private struct Emblem
    {
        public Region region;
        [Tooltip("Same canvas as the template (172x177): the whole seal with its emblem stamped, or just the emblem.")]
        public Sprite sprite;
    }

    [SerializeField] private List<Emblem> emblems = new List<Emblem>();

    [Header("Placeholder (regions without a drawn emblem)")]
    [SerializeField] private ResourceIcons icons;
    [SerializeField] private Color iconTint = new Color(0.32f, 0.04f, 0.05f, 0.85f);
    [SerializeField, Range(0.1f, 1f)] private float iconScale = 0.5f;
    [Tooltip("Optional light edge that makes the tinted icon look pressed into the wax. Only used by the placeholder.")]
    [SerializeField] private Shadow pressedEdge;

    private Image image;

    public void Show(Region region)
    {
        if (image == null) image = GetComponent<Image>();

        Sprite sprite = DrawnEmblem(region);
        bool placeholder = false;
        if (sprite == null && icons != null && region.TryGetResource(out Resource resource))
        {
            sprite = icons.Get(resource);
            placeholder = sprite != null;
        }

        image.sprite = sprite;
        image.enabled = sprite != null;
        image.color = placeholder ? iconTint : Color.white;
        image.preserveAspect = placeholder;
        transform.localScale = Vector3.one * (placeholder ? iconScale : 1f);
        if (pressedEdge != null) pressedEdge.enabled = placeholder;
    }

    private Sprite DrawnEmblem(Region region)
    {
        for (int i = 0; i < emblems.Count; i++)
        {
            if (emblems[i].region == region) return emblems[i].sprite;
        }
        return null;
    }
}
