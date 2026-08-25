using UnityEngine;

/// <summary>
/// Colores de secta: pentágono en el círculo de color (72°) para máxima separación,
/// saturación y brillo al máximo para lectura fluor.
/// </summary>
public static class FactionPalette
{
    // Player + 4 rivales = 5 vértices. 360 / 5 = 72°.
    private const float HuePlayer = 72f;   // oro / amarillo láser
    private const float HueRival1 = 0f;    // rojo
    private const float HueRival2 = 216f;  // cian / azul
    private const float HueRival3 = 144f;  // verde
    private const float HueRival4 = 288f;  // violeta / magenta

    /// <summary>Valor HDR del tinte en overlay y flechas: >1 = glow fluor.</summary>
    private const float GlowValue = 1.55f;

    /// <summary>Corrupción (seeds): naranja, a mitad de camino entre rojo y oro para no chocar con una secta.</summary>
    public static readonly Color Corruption = Neon(36f);
    public static readonly Color CorruptionLabel = LabelOf(Corruption);

    public static Color For(FactionId faction)
    {
        return LabelOf(Glow(faction));
    }

    public static Color For(FactionId? faction)
    {
        return faction.HasValue ? For(faction.Value) : Color.white;
    }

    /// <summary>Tinte HDR para el campo de influencia y las flechas (el shader es aditivo / emissive).</summary>
    public static Color Glow(FactionId faction)
    {
        switch (faction)
        {
            case FactionId.Player: return Neon(HuePlayer);
            case FactionId.Rival1: return Neon(HueRival1);
            case FactionId.Rival2: return Neon(HueRival2);
            case FactionId.Rival3: return Neon(HueRival3);
            case FactionId.Rival4: return Neon(HueRival4);
            default: return Color.white;
        }
    }

    public static Color Glow(FactionId? faction)
    {
        return faction.HasValue ? Glow(faction.Value) : Color.white;
    }

    private static Color Neon(float hueDegrees)
    {
        return Color.HSVToRGB(hueDegrees / 360f, 1f, GlowValue, hdr: true);
    }

    private static Color LabelOf(Color hdr)
    {
        return new Color(Mathf.Clamp01(hdr.r), Mathf.Clamp01(hdr.g), Mathf.Clamp01(hdr.b), 1f);
    }
}
