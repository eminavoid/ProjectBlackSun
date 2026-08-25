using UnityEngine;

/// <summary>
/// Colores de secta compartidos por todos los visuales (marcadores, overlay, flechas).
/// </summary>
public static class FactionPalette
{
    /// <summary>Corrupción sin secta: seeds del antagonista global.</summary>
    public static readonly Color Corruption = new Color(0.75f, 0.25f, 0.08f);

    public static Color For(FactionId faction)
    {
        switch (faction)
        {
            case FactionId.Player: return new Color(0.95f, 0.85f, 0.2f);
            case FactionId.Rival1: return new Color(0.9f, 0.3f, 0.3f);
            case FactionId.Rival2: return new Color(0.3f, 0.6f, 0.95f);
            case FactionId.Rival3: return new Color(0.4f, 0.9f, 0.4f);
            case FactionId.Rival4: return new Color(0.85f, 0.4f, 0.9f);
            default: return Color.white;
        }
    }

    public static Color For(FactionId? faction)
    {
        return faction.HasValue ? For(faction.Value) : Color.white;
    }
}
