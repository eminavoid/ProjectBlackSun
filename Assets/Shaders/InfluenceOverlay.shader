Shader "Custom/InfluenceOverlay"
{
    Properties
    {
        // Los rangos son amplios porque el mapa está escalado ~100x y el código
        // deriva estos valores del tamaño real de una cuadra.
        _Lift("World Lift", Range(0, 200)) = 1.6
        _GlobalAlpha("Global Alpha", Range(0, 1)) = 1
        _Intensity("Intensity", Range(0, 4)) = 1.35
        _MinFill("Min Fill", Range(0, 0.5)) = 0.02
        _FillPower("Fill Power", Range(0.2, 4)) = 0.75

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
                return frac(sin(dot(p, float2(41.13, 289.7))) * 43758.5453);
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

                half fill = pow(saturate(field.a), _FillPower);
                clip(fill - _MinFill);

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
                worldPattern += float2(time * _FlowSpeed * 0.35, time * _FlowSpeed * 0.2);

                half hex = HexCells(worldPattern) * _PatternStrength;

                // Barrido diagonal, también en mundo, para que cruce las cuadras sin cortes.
                float scanPhase = frac((input.positionWS.x + input.positionWS.z) * _PatternScale * 0.25 - time * _ScanSpeed * 0.12);
                half scan = pow(saturate(1.0h - abs(scanPhase - 0.5h) * 2.0h), 6.0h);

                // Disputa: interferencia granulada que rompe el color uniforme.
                half noise = Hash(floor(worldPattern * 6.0) + floor(time * 9.0));
                half interference = contested * noise * 0.6h;

                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);
                half fresnel = pow(1.0h - saturate(abs(dot(normalWS, viewDirWS))), _RimPower);

                half3 baseRgb = field.rgb * _Intensity * (0.55h + hex * 0.9h + scan * 0.7h);
                baseRgb += _FrontierColor.rgb * frontier;
                baseRgb += _RimColor.rgb * fresnel * _RimStrength * fill;
                baseRgb += field.rgb * interference * 1.4h;

                half alpha = fill * (0.42h + hex * 0.45h + scan * 0.35h + frontier * 0.5h + fresnel * 0.55h);
                alpha = saturate(alpha + interference * 0.3h) * _GlobalAlpha;

                return half4(baseRgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
