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

    // Dirección de expansión por cuadra: XZ, apunta hacia vecinos que no son de la misma secta.
    private readonly Dictionary<DistrictZone, Vector2> zoneFlow = new Dictionary<DistrictZone, Vector2>();
    private readonly Dictionary<DistrictZone, Vector2> zoneFlowNext = new Dictionary<DistrictZone, Vector2>();
    private readonly HashSet<DistrictZone> flowSources = new HashSet<DistrictZone>();

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
            ComputeExpansionFlow(zones, adjacency);
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
        Shader.SetGlobalVector(BoundsProperty, BoundsVector);
    }

    public void BindTo(Material material)
    {
        if (material == null || fieldTexture == null || auxTexture == null) return;

        material.SetTexture(FieldProperty, fieldTexture);
        material.SetTexture(AuxProperty, auxTexture);
        material.SetVector(BoundsProperty, BoundsVector);
    }

    public void BindTo(MaterialPropertyBlock block)
    {
        if (block == null || fieldTexture == null || auxTexture == null) return;

        block.SetTexture(FieldProperty, fieldTexture);
        block.SetTexture(AuxProperty, auxTexture);
        block.SetVector(BoundsProperty, BoundsVector);
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

    private Vector4 BoundsVector => new Vector4(fieldOrigin.x, fieldOrigin.y, fieldSize, resolution);

    private Texture2D CreateTexture(string name)
    {
        TextureFormat format = TextureFormat.RGBA32;
        if (SystemInfo.SupportsTextureFormat(TextureFormat.RGBAHalf))
        {
            format = TextureFormat.RGBAHalf;
        }
        else if (SystemInfo.SupportsTextureFormat(TextureFormat.RGBAFloat))
        {
            format = TextureFormat.RGBAFloat;
        }

        Texture2D tex = new Texture2D(resolution, resolution, format, false, true)
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
            float radius = Mathf.Sqrt(bounds.extents.x * bounds.extents.x + bounds.extents.z * bounds.extents.z)
                * settings.SplatRadiusScale;

            // Piso de presencia: un clérigo recién asignado ya tiene que verse aunque
            // todavía no haya generado influencia.
            float strength = Mathf.Max(
                Mathf.Clamp01(totalWeight / state.Cap),
                settings.MinPresence);

            Vector2 flow = Vector2.zero;
            zoneFlow.TryGetValue(zone, out flow);

            Splat(
                bounds.center,
                radius,
                BlendedColor(totalWeight),
                strength,
                Dominance(totalWeight),
                flow);
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
                if (neighbor.GetEntityId() <= zone.GetEntityId()) continue;
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

                zoneFlow.TryGetValue(zone, out Vector2 flowA);
                zoneFlow.TryGetValue(neighbor, out Vector2 flowB);

                Splat(
                    (a.center + b.center) * 0.5f,
                    radius,
                    FactionPalette.For(controller.Value),
                    strength,
                    1f,
                    ClampFlow(flowA + flowB, 1f));
            }
        }
    }

    private void Splat(Vector3 worldCenter, float radius, Color color, float strength, float dominance, Vector2 flow)
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

                float t = Mathf.Sqrt(distSqr / radiusSqr);
                // Meseta: el peso se mantiene alto hasta el borde y recién ahí cae.
                float weight = strength * (1f - Mathf.SmoothStep(0.82f, 1f, t));
                if (weight <= 0.001f) continue;

                int index = row + x;

                // Premultiplicado: el blur mezcla sin halos y se normaliza al final.
                // Aux: R dominancia, G/B dirección de expansión en XZ (con signo), A peso.
                accum[index].r += color.r * weight;
                accum[index].g += color.g * weight;
                accum[index].b += color.b * weight;
                accum[index].a += weight;

                accumAux[index].r += dominance * weight;
                accumAux[index].g += flow.x * weight;
                accumAux[index].b += flow.y * weight;
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

    /// <summary>
    /// Empuje hacia afuera: cada cuadra apunta a vecinos que no controla su secta.
    /// El interior hereda esa dirección para que el bloque entero se lea como un avance.
    /// </summary>
    private void ComputeExpansionFlow(IReadOnlyList<DistrictZone> zones, ZoneAdjacencyGraph adjacency)
    {
        zoneFlow.Clear();
        flowSources.Clear();
        if (zones == null) return;

        for (int i = 0; i < zones.Count; i++)
        {
            DistrictZone zone = zones[i];
            if (zone == null || !zone.IsPlayable || zone.Influence == null) continue;

            Vector2 push = DirectPush(zone, adjacency);
            zoneFlow[zone] = push;
            if (push.sqrMagnitude > 0.2f) flowSources.Add(zone);
        }

        const int iterations = 8;
        for (int iter = 0; iter < iterations; iter++)
        {
            zoneFlowNext.Clear();

            foreach (KeyValuePair<DistrictZone, Vector2> pair in zoneFlow)
            {
                DistrictZone zone = pair.Key;
                Vector2 current = pair.Value;
                if (flowSources.Contains(zone) || adjacency == null)
                {
                    zoneFlowNext[zone] = current;
                    continue;
                }

                FactionId? owner = ActingFaction(zone.Influence);
                Vector2 accumulated = Vector2.zero;
                int contributors = 0;

                IReadOnlyList<DistrictZone> neighbors = adjacency.GetNeighbors(zone);
                for (int n = 0; n < neighbors.Count; n++)
                {
                    DistrictZone neighbor = neighbors[n];
                    if (neighbor == null || neighbor.Influence == null) continue;
                    if (ActingFaction(neighbor.Influence) != owner) continue;
                    if (!zoneFlow.TryGetValue(neighbor, out Vector2 neighborFlow)) continue;
                    if (neighborFlow.sqrMagnitude < 1e-6f) continue;

                    accumulated += neighborFlow;
                    contributors++;
                }

                zoneFlowNext[zone] = contributors > 0
                    ? ClampFlow(accumulated / contributors, 0.72f)
                    : current;
            }

            zoneFlow.Clear();
            foreach (KeyValuePair<DistrictZone, Vector2> pair in zoneFlowNext)
            {
                zoneFlow[pair.Key] = pair.Value;
            }
        }
    }

    private static Vector2 DirectPush(DistrictZone zone, ZoneAdjacencyGraph adjacency)
    {
        if (adjacency == null || zone == null || zone.Influence == null) return Vector2.zero;

        FactionId? owner = ActingFaction(zone.Influence);
        if (!owner.HasValue) return Vector2.zero;

        Vector2 push = Vector2.zero;
        Vector3 origin = zone.GetWorldBounds().center;
        IReadOnlyList<DistrictZone> neighbors = adjacency.GetNeighbors(zone);

        for (int i = 0; i < neighbors.Count; i++)
        {
            DistrictZone neighbor = neighbors[i];
            if (neighbor == null || !neighbor.IsPlayable) continue;

            FactionId? other = neighbor.Influence != null ? ActingFaction(neighbor.Influence) : null;
            if (other.HasValue && other.Value == owner.Value) continue;

            Vector3 delta = neighbor.GetWorldBounds().center - origin;
            Vector2 direction = new Vector2(delta.x, delta.z);
            if (direction.sqrMagnitude < 1e-6f) continue;

            push += direction.normalized;
        }

        return ClampFlow(push, 1f);
    }

    private static FactionId? ActingFaction(ZoneInfluenceState state)
    {
        if (state == null) return null;
        if (state.Controller.HasValue) return state.Controller;

        FactionId? best = null;
        float bestWeight = 0f;

        for (int i = 0; i < FactionIdUtil.All.Length; i++)
        {
            FactionId faction = FactionIdUtil.All[i];
            float weight = state.GetShare(faction) + state.GetClerics(faction);
            if (weight <= bestWeight) continue;
            bestWeight = weight;
            best = faction;
        }

        return best;
    }

    private static Vector2 ClampFlow(Vector2 flow, float maxLength)
    {
        float sqr = flow.sqrMagnitude;
        float cap = maxLength * maxLength;
        if (sqr <= cap || sqr < 1e-8f) return flow;
        return flow * (maxLength / Mathf.Sqrt(sqr));
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
