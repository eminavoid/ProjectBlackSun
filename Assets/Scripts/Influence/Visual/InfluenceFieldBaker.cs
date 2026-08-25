using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hornea el campo de influencia del mapa en texturas globales que consume Custom/InfluenceOverlay.
/// Trabaja en world-space XZ, así el patrón es continuo entre cuadras vecinas.
/// </summary>
public class InfluenceFieldBaker
{
    private const string FieldProperty = "_InfluenceField";
    private const string AuxProperty = "_InfluenceFieldAux";
    private const string BoundsProperty = "_InfluenceFieldBounds";

    private const float MinWeight = 1e-4f;

    private readonly int resolution;
    private readonly Color[] accum;
    private readonly Color[] accumAux;
    private readonly Color[] blurTemp;
    private readonly Color[] target;
    private readonly Color[] targetAux;
    private readonly Color[] previous;
    private readonly Color[] previousAux;
    private readonly Color[] displayed;
    private readonly Color[] displayedAux;
    private readonly float[] weights = new float[FactionIdUtil.All.Length];

    private Texture2D fieldTexture;
    private Texture2D auxTexture;

    private Vector2 fieldOrigin;
    private float fieldSize = 1f;
    private bool boundsValid;
    private bool hasBaked;

    /// <summary>True cuando el encuadre cambió y no se puede interpolar contra el horneado anterior.</summary>
    public bool LayoutChanged { get; private set; }

    public InfluenceFieldBaker(int resolution)
    {
        this.resolution = Mathf.Clamp(resolution, 32, 512);
        int count = this.resolution * this.resolution;

        accum = new Color[count];
        accumAux = new Color[count];
        blurTemp = new Color[count];
        target = new Color[count];
        targetAux = new Color[count];
        previous = new Color[count];
        previousAux = new Color[count];
        displayed = new Color[count];
        displayedAux = new Color[count];
    }

    public void Bake(IReadOnlyList<DistrictZone> zones, ZoneAdjacencyGraph adjacency, Settings settings)
    {
        System.Array.Copy(displayed, previous, displayed.Length);
        System.Array.Copy(displayedAux, previousAux, displayedAux.Length);

        LayoutChanged = !UpdateBounds(zones) || !hasBaked;

        System.Array.Clear(accum, 0, accum.Length);
        System.Array.Clear(accumAux, 0, accumAux.Length);

        if (boundsValid && zones != null)
        {
            SplatZones(zones, settings);
            SplatBridges(zones, adjacency, settings);
        }

        for (int i = 0; i < settings.BlurPasses; i++)
        {
            Blur(accum, settings.BlurRadius);
            Blur(accumAux, settings.BlurRadius);
        }

        Normalize(accum, target);
        Normalize(accumAux, targetAux);

        hasBaked = true;
    }

    /// <summary>Sube el campo interpolando entre el horneado anterior y el actual.</summary>
    public void Publish(float blend)
    {
        EnsureTextures();

        blend = LayoutChanged ? 1f : Mathf.Clamp01(blend);

        if (blend >= 1f)
        {
            System.Array.Copy(target, displayed, target.Length);
            System.Array.Copy(targetAux, displayedAux, targetAux.Length);
        }
        else
        {
            for (int i = 0; i < displayed.Length; i++)
            {
                displayed[i] = Color.Lerp(previous[i], target[i], blend);
                displayedAux[i] = Color.Lerp(previousAux[i], targetAux[i], blend);
            }
        }

        fieldTexture.SetPixels(displayed);
        fieldTexture.Apply(false, false);
        auxTexture.SetPixels(displayedAux);
        auxTexture.Apply(false, false);

        Shader.SetGlobalTexture(FieldProperty, fieldTexture);
        Shader.SetGlobalTexture(AuxProperty, auxTexture);
        Shader.SetGlobalVector(BoundsProperty, new Vector4(fieldOrigin.x, fieldOrigin.y, fieldSize, resolution));
    }

    public void Release()
    {
        if (fieldTexture != null) Object.Destroy(fieldTexture);
        if (auxTexture != null) Object.Destroy(auxTexture);
        fieldTexture = null;
        auxTexture = null;
    }

    private void EnsureTextures()
    {
        if (fieldTexture == null)
        {
            fieldTexture = CreateTexture("InfluenceField");
        }

        if (auxTexture == null)
        {
            auxTexture = CreateTexture("InfluenceFieldAux");
        }
    }

    private Texture2D CreateTexture(string name)
    {
        Texture2D tex = new Texture2D(resolution, resolution, TextureFormat.RGBAHalf, false, true)
        {
            name = name,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        return tex;
    }

    /// <summary>Encuadre cuadrado que cubre todas las cuadras; devuelve false si cambió respecto al anterior.</summary>
    private bool UpdateBounds(IReadOnlyList<DistrictZone> zones)
    {
        Vector2 previousOrigin = fieldOrigin;
        float previousSize = fieldSize;
        bool hadBounds = boundsValid;

        boundsValid = false;
        if (zones == null || zones.Count == 0) return false;

        float minX = float.MaxValue;
        float minZ = float.MaxValue;
        float maxX = float.MinValue;
        float maxZ = float.MinValue;

        for (int i = 0; i < zones.Count; i++)
        {
            DistrictZone zone = zones[i];
            if (zone == null || !zone.IsPlayable) continue;

            Bounds bounds = zone.GetWorldBounds();
            if (bounds.min.x < minX) minX = bounds.min.x;
            if (bounds.min.z < minZ) minZ = bounds.min.z;
            if (bounds.max.x > maxX) maxX = bounds.max.x;
            if (bounds.max.z > maxZ) maxZ = bounds.max.z;
        }

        if (minX > maxX) return false;

        // Cuadrado con margen: el blur necesita espacio para desbordar sin recortarse.
        float span = Mathf.Max(maxX - minX, maxZ - minZ);
        float padding = Mathf.Max(span * 0.08f, 1f);
        span += padding * 2f;

        float centerX = (minX + maxX) * 0.5f;
        float centerZ = (minZ + maxZ) * 0.5f;

        fieldSize = Mathf.Max(span, 0.01f);
        fieldOrigin = new Vector2(centerX - fieldSize * 0.5f, centerZ - fieldSize * 0.5f);
        boundsValid = true;

        return hadBounds
            && Mathf.Approximately(previousSize, fieldSize)
            && (previousOrigin - fieldOrigin).sqrMagnitude < 1e-6f;
    }

    private void SplatZones(IReadOnlyList<DistrictZone> zones, Settings settings)
    {
        for (int i = 0; i < zones.Count; i++)
        {
            DistrictZone zone = zones[i];
            if (zone == null || !zone.IsPlayable) continue;

            ZoneInfluenceState state = zone.Influence;
            if (state == null) continue;

            float totalWeight = AccumulateWeights(state, settings.ClericWeight);
            if (totalWeight <= 0f) continue;

            Bounds bounds = zone.GetWorldBounds();
            float radius = Mathf.Max(bounds.extents.x, bounds.extents.z) * settings.SplatRadiusScale;

            // Piso de presencia: un clérigo recién asignado ya tiene que verse aunque
            // todavía no haya generado influencia.
            float strength = Mathf.Max(
                Mathf.Clamp01(totalWeight / state.Cap),
                settings.MinPresence);

            Splat(
                bounds.center,
                radius,
                BlendedColor(totalWeight),
                strength,
                Dominance(totalWeight),
                DistrictKey(zone.District));
        }
    }

    private void SplatBridges(
        IReadOnlyList<DistrictZone> zones,
        ZoneAdjacencyGraph adjacency,
        Settings settings)
    {
        if (adjacency == null) return;

        for (int i = 0; i < zones.Count; i++)
        {
            DistrictZone zone = zones[i];
            if (zone == null || !zone.IsPlayable || zone.Influence == null) continue;

            FactionId? controller = zone.Influence.Controller;
            if (!controller.HasValue) continue;

            IReadOnlyList<DistrictZone> neighbors = adjacency.GetNeighbors(zone);
            for (int n = 0; n < neighbors.Count; n++)
            {
                DistrictZone neighbor = neighbors[n];
                if (neighbor == null || neighbor.Influence == null) continue;

                // Cada par se procesa una sola vez.
                if (neighbor.GetInstanceID() <= zone.GetInstanceID()) continue;
                if (neighbor.District != zone.District) continue;
                if (neighbor.Influence.Controller != controller) continue;

                Bounds a = zone.GetWorldBounds();
                Bounds b = neighbor.GetWorldBounds();

                float radius = Mathf.Min(
                    Mathf.Max(a.extents.x, a.extents.z),
                    Mathf.Max(b.extents.x, b.extents.z)) * settings.BridgeRadiusScale;

                float strength = Mathf.Min(
                    Mathf.Clamp01(zone.Influence.TotalInfluence / (float)zone.Influence.Cap),
                    Mathf.Clamp01(neighbor.Influence.TotalInfluence / (float)neighbor.Influence.Cap));

                Splat(
                    (a.center + b.center) * 0.5f,
                    radius,
                    FactionPalette.For(controller.Value),
                    strength,
                    1f,
                    DistrictKey(zone.District));
            }
        }
    }

    private void Splat(Vector3 worldCenter, float radius, Color color, float strength, float dominance, float districtKey)
    {
        if (radius <= 0f || strength <= 0f) return;

        float texelsPerUnit = resolution / fieldSize;
        float centerU = (worldCenter.x - fieldOrigin.x) * texelsPerUnit;
        float centerV = (worldCenter.z - fieldOrigin.y) * texelsPerUnit;
        float radiusTexels = radius * texelsPerUnit;
        if (radiusTexels < 0.5f) radiusTexels = 0.5f;

        int minX = Mathf.Max(0, Mathf.FloorToInt(centerU - radiusTexels));
        int maxX = Mathf.Min(resolution - 1, Mathf.CeilToInt(centerU + radiusTexels));
        int minY = Mathf.Max(0, Mathf.FloorToInt(centerV - radiusTexels));
        int maxY = Mathf.Min(resolution - 1, Mathf.CeilToInt(centerV + radiusTexels));

        float radiusSqr = radiusTexels * radiusTexels;

        for (int y = minY; y <= maxY; y++)
        {
            float dy = y + 0.5f - centerV;
            int row = y * resolution;

            for (int x = minX; x <= maxX; x++)
            {
                float dx = x + 0.5f - centerU;
                float distSqr = dx * dx + dy * dy;
                if (distSqr > radiusSqr) continue;

                float falloff = 1f - Mathf.Sqrt(distSqr / radiusSqr);
                float weight = falloff * falloff * strength;
                if (weight <= 0f) continue;

                int index = row + x;

                // Premultiplicado: el blur mezcla sin halos y se normaliza al final.
                accum[index].r += color.r * weight;
                accum[index].g += color.g * weight;
                accum[index].b += color.b * weight;
                accum[index].a += weight;

                accumAux[index].r += dominance * weight;
                accumAux[index].g += districtKey * weight;
                accumAux[index].a += weight;
            }
        }
    }

    private void Blur(Color[] buffer, int radius)
    {
        if (radius <= 0) return;

        float norm = 1f / (radius * 2f + 1f);

        for (int y = 0; y < resolution; y++)
        {
            int row = y * resolution;
            for (int x = 0; x < resolution; x++)
            {
                Color sum = default;
                for (int k = -radius; k <= radius; k++)
                {
                    int sx = Mathf.Clamp(x + k, 0, resolution - 1);
                    sum += buffer[row + sx];
                }

                blurTemp[row + x] = sum * norm;
            }
        }

        for (int x = 0; x < resolution; x++)
        {
            for (int y = 0; y < resolution; y++)
            {
                Color sum = default;
                for (int k = -radius; k <= radius; k++)
                {
                    int sy = Mathf.Clamp(y + k, 0, resolution - 1);
                    sum += blurTemp[sy * resolution + x];
                }

                buffer[y * resolution + x] = sum * norm;
            }
        }
    }

    private static void Normalize(Color[] source, Color[] destination)
    {
        for (int i = 0; i < source.Length; i++)
        {
            float weight = source[i].a;
            if (weight <= MinWeight)
            {
                destination[i] = Color.clear;
                continue;
            }

            float inv = 1f / weight;
            destination[i] = new Color(
                source[i].r * inv,
                source[i].g * inv,
                source[i].b * inv,
                Mathf.Clamp01(weight));
        }
    }

    /// <summary>
    /// Peso por secta en la zona: influencia ya generada más los clérigos estacionados,
    /// para que una asignación se vea antes de que produzca. Devuelve el total.
    /// </summary>
    private float AccumulateWeights(ZoneInfluenceState state, float clericWeight)
    {
        float total = 0f;

        for (int i = 0; i < FactionIdUtil.All.Length; i++)
        {
            FactionId faction = FactionIdUtil.All[i];
            float weight = state.GetShare(faction) + state.GetClerics(faction) * clericWeight;
            weights[i] = weight;
            total += weight;
        }

        return total;
    }

    private Color BlendedColor(float totalWeight)
    {
        Color blended = Color.black;

        for (int i = 0; i < weights.Length; i++)
        {
            if (weights[i] <= 0f) continue;

            float ratio = weights[i] / totalWeight;
            Color color = FactionPalette.For(FactionIdUtil.All[i]);
            blended.r += color.r * ratio;
            blended.g += color.g * ratio;
            blended.b += color.b * ratio;
        }

        blended.a = 1f;
        return blended;
    }

    /// <summary>1 cuando una sola secta ocupa la zona, baja al repartirse (disputa).</summary>
    private float Dominance(float totalWeight)
    {
        float best = 0f;

        for (int i = 0; i < weights.Length; i++)
        {
            if (weights[i] > best) best = weights[i];
        }

        return Mathf.Clamp01(best / totalWeight);
    }

    private static float DistrictKey(Districts district)
    {
        return ((int)district + 1) / 8f;
    }

    public struct Settings
    {
        public float SplatRadiusScale;
        public float BridgeRadiusScale;
        public int BlurPasses;
        public int BlurRadius;
        public float ClericWeight;
        public float MinPresence;
    }
}
