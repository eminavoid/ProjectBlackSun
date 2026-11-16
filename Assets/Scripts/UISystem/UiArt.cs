using System.Collections.Generic;
using UnityEngine;

public static class UiArt
{
    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    public static Sprite Find(string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName)) return null;
        if (cache.TryGetValue(spriteName, out Sprite cached) && cached != null) return cached;

        Sprite[] sprites = Resources.FindObjectsOfTypeAll<Sprite>();
        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite sprite = sprites[i];
            if (sprite == null) continue;
            if (cache.TryGetValue(sprite.name, out Sprite known) && known != null) continue;
            cache[sprite.name] = sprite;
        }

        return cache.TryGetValue(spriteName, out cached) ? cached : null;
    }
}
