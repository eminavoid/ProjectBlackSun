Shader "Custom/InfluenceOverlay"
{
    Properties
    {
        // Los rangos son amplios porque el mapa está escalado ~100x y el código
        // deriva estos valores del tamaño real de una cuadra.
        _Lift("World Lift", Range(0, 200)) = 1.6
        _GlobalAlpha("Global Alpha", Range(0, 1)) = 1
        _Intensity("Intensity", Range(0, 4)) = 1.75
        _MinFill("Min Fill", Range(0, 0.5)) = 0.01
        _FillPower("Fill Power", Range(0.2, 4)) = 0.4

        [HDR] _RimColor("Rim Color", Color) = (1.1, 1.1, 1.4, 1)
        _RimPower("Rim Power", Range(0.4, 8)) = 1.8
        _RimStrength("Rim Strength", Range(0, 3)) = 1.1

        [HDR] _FrontierColor("Frontier Color", Color) = (1.6, 1.5, 1.2, 1)
        _FrontierStrength("Frontier Strength", Range(0, 8)) = 2.6
        _FrontierWidth("Frontier Width", Range(0.5, 8)) = 2.2

        _PatternScale("Pattern Scale", Range(0.001, 20)) = 0.35
        _PatternStrength("Pattern Strength", Range(0, 1)) = 0.45
        _FlowSpeed("Flow Speed", Range(0, 4)) = 0.35
        _ScanSpeed("Scan Speed", Range(0, 6)) = 0.9
        _SmokeStrength("Smoke Strength", Range(0, 1)) = 0.72
        _SmokeScale("Smoke Scale", Range(0.001, 20)) = 0.12
        _SmokeSpeed("Smoke Speed", Range(0, 2)) = 0.55
        _ContestedStrength("Contested Strength", Range(0, 2)) = 0.9
        _BreathAmp("Breath Amplitude", Range(0, 20)) = 0.25
        _BreathSpeed("Breath Speed", Range(0, 8)) = 1.4
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "InfluenceOverlay"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Campo global horneado por InfluenceFieldBaker (world-space XZ).
            TEXTURE2D(_InfluenceField);
            SAMPLER(sampler_InfluenceField);
            TEXTURE2D(_InfluenceFieldAux);
            SAMPLER(sampler_InfluenceFieldAux);
            float4 _InfluenceFieldBounds; // xy = origen XZ, z = tamaño del cuadrado, w = resolución

            CBUFFER_START(UnityPerMaterial)
                half _Lift;
                half _GlobalAlpha;
                half _Intensity;
                half _MinFill;
                half _FillPower;
                half4 _RimColor;
                half _RimPower;
                half _RimStrength;
                half4 _FrontierColor;
                half _FrontierStrength;
                half _FrontierWidth;
                half _PatternScale;
                half _PatternStrength;
                half _FlowSpeed;
                half _ScanSpeed;
                half _SmokeStrength;
                half _SmokeScale;
                half _SmokeSpeed;
                half _ContestedStrength;
                half _BreathAmp;
                half _BreathSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
            };

            float2 FieldUV(float2 worldXZ)
            {
                return (worldXZ - _InfluenceFieldBounds.xy) / max(_InfluenceFieldBounds.z, 0.0001);
            }

            half4 SampleField(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_InfluenceField, sampler_InfluenceField, uv);
            }

            float HexDistance(float2 p)
            {
                p = abs(p);
                return max(p.x * 0.8660254 + p.y * 0.5, p.y);
            }

            // Celdas hexagonales en coordenadas de mundo: continuas entre cuadras vecinas.
            float HexCells(float2 p)
            {
                const float2 skew = float2(1.0, 1.7320508);
                float2 a = frac(p / skew) * skew - skew * 0.5;
                float2 b = frac((p + skew * 0.5) / skew) * skew - skew * 0.5;
                float2 closest = dot(a, a) < dot(b, b) ? a : b;
                float edge = 0.5 - HexDistance(closest);
                return 1.0 - smoothstep(0.0, 0.14, edge);
            }

            float Hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
                float a = Hash(i);
                float b = Hash(i + float2(1.0, 0.0));
                float c = Hash(i + float2(0.0, 1.0));
                float d = Hash(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float Fbm(float2 p)
            {
                float n = ValueNoise(p);
                n += ValueNoise(p * 2.03 + 17.2) * 0.5;
                n += ValueNoise(p * 4.07 - 8.4) * 0.25;
                return n * 0.5714;
            }

            float SmokeField(float2 worldXZ, float time)
            {
                float scale = max(_SmokeScale, 0.0001);
                float speed = max(_SmokeSpeed, 0.0);
                float2 q = worldXZ * scale;

                // Warp de dominio: las nubes se deforman en vez de deslizar rígidas.
                float2 warp = float2(
                    ValueNoise(q * 0.85 + float2(time * speed * 0.08, time * speed * 0.05)),
                    ValueNoise(q * 0.85 + float2(31.2, 17.8) - float2(time * speed * 0.06, time * speed * 0.09)));
                warp = (warp * 2.0 - 1.0) * 0.9;

                float2 r = q + warp;

                float clouds = Fbm(r + float2(time * speed * 0.11, -time * speed * 0.06));
                float layer2 = Fbm(r * 1.35 - float2(time * speed * 0.07, time * speed * 0.1) + 9.1);
                float ridges = 1.0 - abs(layer2 * 2.0 - 1.0);

                float pulse = 0.9 + 0.1 * sin(time * speed * 1.35 + clouds * 6.2831);
                return saturate((clouds * 0.58 + ridges * 0.42) * pulse);
            }

            float GrainField(float2 worldXZ, float time)
            {
                float scale = max(_SmokeScale, 0.0001);
                float speed = max(_SmokeSpeed, 0.0);
                float2 p = worldXZ * scale * 6.5;

                float n1 = ValueNoise(p + float2(time * speed * 0.42, -time * speed * 0.31));
                float n2 = ValueNoise(p * 2.25 + float2(-time * speed * 0.58, time * speed * 0.47));

                float sparkle = sin((n1 * 10.0 + n2 * 7.0 + time * speed * 2.8) * 6.2831853) * 0.5 + 0.5;
                sparkle = sparkle * sparkle * sparkle;
                return saturate(n1 * 0.48 + n2 * 0.32 + sparkle * 0.2);
            }

            // Evita el blowout a blanco sin re-saturar colores apagados (el azul del jugador).
            half3 FactionChroma(half3 c)
            {
                c = max(c, 0.0h);
                half peak = max(c.r, max(c.g, c.b));
                return peak > 1.0h ? c / peak : c;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                float3 normalWS = NormalizeNormalPerVertex(normalInputs.normalWS);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);

                // Elevación en world-space: inmune al escalado de los meshes del mapa.
                float breath = sin(_Time.y * _BreathSpeed + positionWS.x * 0.05 + positionWS.z * 0.05);
                positionWS.y += _Lift + breath * _BreathAmp;

                output.positionWS = positionWS;
                output.normalWS = normalWS;
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(positionWS);
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = FieldUV(input.positionWS.xz);
                half4 field = SampleField(uv);

                half coverage = smoothstep(_MinFill, 0.12h, saturate(field.a));
                half fill = pow(saturate(field.a), _FillPower);
                clip(coverage - 0.001h);

                half4 aux = SAMPLE_TEXTURE2D(_InfluenceFieldAux, sampler_InfluenceFieldAux, uv);
                half dominance = saturate(aux.r);
                half contested = saturate(1.0h - dominance) * _ContestedStrength;

                // Frontera: derivada del campo en 4 taps. Salta donde cambia el dominante o el distrito.
                float texel = _FrontierWidth / max(_InfluenceFieldBounds.w, 1.0);
                half4 left = SampleField(uv - float2(texel, 0));
                half4 right = SampleField(uv + float2(texel, 0));
                half4 down = SampleField(uv - float2(0, texel));
                half4 up = SampleField(uv + float2(0, texel));

                half3 gradX = abs(right.rgb - left.rgb);
                half3 gradZ = abs(up.rgb - down.rgb);
                half colorEdge = saturate((gradX.r + gradX.g + gradX.b + gradZ.r + gradZ.g + gradZ.b) * 1.6h);
                half fillEdge = saturate((abs(right.a - left.a) + abs(up.a - down.a)) * 2.2h);
                half frontier = saturate(max(colorEdge, fillEdge)) * _FrontierStrength;

                float time = _Time.y;
                float2 worldPattern = input.positionWS.xz * _PatternScale;
                float2 swirl = float2(
                    sin(time * _FlowSpeed * 0.35 + input.positionWS.z * _PatternScale * 0.55),
                    cos(time * _FlowSpeed * 0.28 + input.positionWS.x * _PatternScale * 0.55)) * 0.18;
                worldPattern += float2(time * _FlowSpeed * 0.12, -time * _FlowSpeed * 0.08) + swirl;

                half hex = HexCells(worldPattern) * _PatternStrength;

                float scanPhase = frac((input.positionWS.x + input.positionWS.z) * _PatternScale * 0.25 - time * _ScanSpeed * 0.12);
                half scan = pow(saturate(1.0h - abs(scanPhase - 0.5h) * 2.0h), 6.0h);

                half smoke = SmokeField(input.positionWS.xz, time);
                half grain = GrainField(input.positionWS.xz, time);

                half interference = contested * grain * 0.55h;

                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);
                half fresnel = pow(1.0h - saturate(abs(dot(normalWS, viewDirWS))), _RimPower);

                half3 chroma = FactionChroma(field.rgb);
                half energy = saturate(_Intensity * 0.25h);
                half pattern = 0.62h + hex * 0.2h + scan * 0.12h + fill * 0.08h;

                // El ruido texturiza; el piso alto conserva el fluor.
                half smokeMix = saturate(_SmokeStrength);
                half textureBreak = smoke * 0.62h + grain * 0.38h;
                half fluor = lerp(0.72h, 1.0h, saturate(energy * pattern));
                half value = fluor * lerp(1.0h, 0.62h + textureBreak * 0.5h, smokeMix);

                half3 baseRgb = chroma * value;
                baseRgb += chroma * frontier * 0.28h;
                baseRgb += chroma * fresnel * _RimStrength * 0.22h;
                baseRgb += chroma * interference * 0.45h;
                baseRgb += chroma * grain * smokeMix * 0.16h;

                half peak = max(baseRgb.r, max(baseRgb.g, baseRgb.b));
                if (peak > 1.0h) baseRgb /= peak;

                half alpha = coverage * lerp(0.52h, 0.88h, energy)
                    * (0.8h + hex * 0.08h + scan * 0.05h + frontier * 0.08h + fresnel * 0.1h);
                alpha *= lerp(1.0h, 0.42h + smoke * 0.52h + grain * 0.18h, smokeMix);
                alpha = saturate(alpha + interference * 0.2h) * _GlobalAlpha;

                return half4(baseRgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
