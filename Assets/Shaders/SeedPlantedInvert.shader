Shader "Custom/SeedPlantedInvert"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [HDR] _ShieldColor("Shield Color", Color) = (0.25, 0.85, 1.4, 1)
        [HDR] _RimColor("Rim Color", Color) = (0.6, 1.5, 2.0, 1)
        _ShieldIntensity("Shield Intensity", Range(0, 4)) = 1.8
        _ShellDistance("Shell Distance", Range(0, 0.1)) = 0.01
        _NoiseScale("Noise Scale", Range(1, 40)) = 12
        _NoiseSpeed("Noise Speed", Range(0, 6)) = 1.4
        _NoiseStrength("Noise Strength", Range(0, 2)) = 1.1
        _HexScale("Hex Scale", Range(1, 80)) = 28
        _HexStrength("Hex Strength", Range(0, 1)) = 0.55
        _RimPower("Rim Power", Range(0.4, 8)) = 1.8
        _PulseSpeed("Pulse Speed", Range(0, 8)) = 2.0
        _BaseDim("Base Dim", Range(0, 1)) = 0.12
        _DistortAmount("View Distort", Range(0, 0.2)) = 0.035
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForceShield"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _ShieldColor;
                half4 _RimColor;
                half _ShieldIntensity;
                half _ShellDistance;
                half _NoiseScale;
                half _NoiseSpeed;
                half _NoiseStrength;
                half _HexScale;
                half _HexStrength;
                half _RimPower;
                half _PulseSpeed;
                half _BaseDim;
                half _DistortAmount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 viewDirWS : TEXCOORD3;
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 19.19);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float Fbm(float2 uv)
            {
                float value = 0.0;
                float amp = 0.5;
                float2 p = uv;
                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    value += ValueNoise(p) * amp;
                    p = p * 2.15 + float2(17.1, 9.7);
                    amp *= 0.5;
                }
                return value;
            }

            float HexGrid(float2 uv)
            {
                const float2 s = float2(1.0, 1.7320508);
                float2 c1 = (frac(uv / s) - 0.5) * s;
                float2 c2 = (frac(uv / s + 0.5) - 0.5) * s;
                float2 g = dot(c1, c1) < dot(c2, c2) ? c1 : c2;
                float d = max(abs(g.x) * 0.866025 + abs(g.y) * 0.5, abs(g.y));
                return saturate(1.0 - smoothstep(0.42, 0.5, d));
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                float3 normalWS = NormalizeNormalPerVertex(normalInputs.normalWS);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(positionWS);

                // Always lift toward the camera-facing side so the shield never sinks under the mesh in Game View.
                float facing = dot(normalWS, viewDirWS);
                float3 shellDir = facing >= 0.0 ? normalWS : -normalWS;
                positionWS += shellDir * _ShellDistance;

                output.positionCS = TransformWorldToHClip(positionWS);

                // Extra depth pull toward the near plane (handles reversed-Z and Game camera precision).
                #if UNITY_REVERSED_Z
                output.positionCS.z = min(output.positionCS.z + 1.0e-4 * output.positionCS.w, output.positionCS.w);
                #else
                output.positionCS.z = max(output.positionCS.z - 1.0e-4 * output.positionCS.w, -output.positionCS.w);
                #endif

                output.positionWS = positionWS;
                output.normalWS = normalWS;
                output.viewDirWS = viewDirWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                baseColor.rgb *= (1.0h - _BaseDim);

                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);
                float ndotv = saturate(abs(dot(normalWS, viewDirWS)));
                float fresnel = pow(1.0 - ndotv, _RimPower);

                float time = _Time.y * _NoiseSpeed;

                // Parallax-ish UV shift for depth feeling separate from the base sample.
                float2 shellUv = input.uv + viewDirWS.xz * (_ShellDistance * 4.0 + _DistortAmount);

                float2 noiseUv = shellUv * _NoiseScale;
                noiseUv += float2(time * 0.35, -time * 0.22);
                noiseUv += viewDirWS.xz * (_DistortAmount * 8.0);

                float noiseA = Fbm(noiseUv);
                float noiseB = Fbm(noiseUv * 1.7 + float2(-time * 0.4, time * 0.18));
                float movingNoise = saturate(noiseA * 0.65 + noiseB * 0.55);
                movingNoise = saturate(pow(movingNoise, 1.35) * _NoiseStrength);

                float2 hexUv = shellUv * _HexScale;
                hexUv += float2(time * 0.12, time * 0.08);
                hexUv += (movingNoise - 0.5) * 0.35;
                float hex = HexGrid(hexUv);
                float hexPulse = 0.55 + 0.45 * sin(_Time.y * _PulseSpeed + movingNoise * 6.2831);
                float hexEnergy = hex * _HexStrength * hexPulse;

                float sweep = sin((shellUv.x + shellUv.y) * 18.0 - time * 3.5 + movingNoise * 4.0);
                sweep = pow(saturate(sweep * 0.5 + 0.5), 6.0);

                float pulse = 0.75 + 0.25 * sin(_Time.y * _PulseSpeed);
                float fill = movingNoise * 0.45 + hexEnergy * 0.7 + sweep * 0.5;
                float shieldMask = saturate(fresnel * 1.35 + fill * (0.45 + fresnel * 0.85));
                shieldMask *= pulse;

                half3 shieldRgb = _ShieldColor.rgb * (movingNoise * 0.55 + hexEnergy + sweep * 0.8);
                shieldRgb += _RimColor.rgb * fresnel * (1.1 + movingNoise * 0.6);
                shieldRgb *= _ShieldIntensity * shieldMask;

                // Composite in one Forward pass so Game View never draws the shield under the base.
                half3 finalRgb = baseColor.rgb + shieldRgb;
                return half4(finalRgb, baseColor.a);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
