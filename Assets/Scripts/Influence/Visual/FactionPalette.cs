using UnityEngine;

/// <summary>
/// Paleta fija de influencia (hex). El overlay usa For(); las flechas usan Glow() HDR.
/// </summary>
public static class FactionPalette
{
    private const float GlowValue = 1.55f;

    private static readonly Color PlayerRgb = Hex("#d000ff");
    private static readonly Color Rival1Rgb = Hex("#ff0000");
    private static readonly Color Rival2Rgb = Hex("#00f2ff");
    private static readonly Color Rival3Rgb = Hex("#00ff00");
    private static readonly Color Rival4Rgb = Hex("#eeff00");

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
            case FactionId.Player: return PlayerRgb;
            case FactionId.Rival1: return Neon(Rival1Rgb);
            case FactionId.Rival2: return Neon(Rival2Rgb);
            case FactionId.Rival3: return Neon(Rival3Rgb);
            case FactionId.Rival4: return Neon(Rival4Rgb);
            default: return Color.white;
        }
    }

    public static Color Glow(FactionId? faction)
    {
        return faction.HasValue ? Glow(faction.Value) : Color.white;
    }

    /// <summary>Tinta oscura sobre el overlay fluor: matiz opuesto, brillo bajo.</summary>
    public static Color ContrastOn(Color surface)
    {
        Color ground = LabelOf(surface);
        Color.RGBToHSV(ground, out float hue, out float sat, out _);

        if (sat < 0.08f)
        {
            return new Color(0.07f, 0.07f, 0.08f, 1f);
        }

        return Color.HSVToRGB(Mathf.Repeat(hue + 0.5f, 1f), 0.85f, 0.13f);
    }

    private static Color Neon(Color rgb)
    {
        return new Color(rgb.r * GlowValue, rgb.g * GlowValue, rgb.b * GlowValue, 1f);
    }

    private static Color Hex(string html)
    {
        if (!ColorUtility.TryParseHtmlString(html, out Color color))
        {
            return Color.magenta;
        }

        color.a = 1f;
        return color;
    }

    private static Color LabelOf(Color hdr)
    {
        return new Color(Mathf.Clamp01(hdr.r), Mathf.Clamp01(hdr.g), Mathf.Clamp01(hdr.b), 1f);
    }
}
