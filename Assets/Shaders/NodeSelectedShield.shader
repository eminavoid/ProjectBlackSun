Shader "Custom/NodeSelectedShield"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [HDR] _ShieldColor("Shield Color", Color) = (1.4, 0.15, 1.55, 1)
        [HDR] _RimColor("Rim Color", Color) = (1.8, 1.2, 0.2, 1)
        [HDR] _ScanColor("Scan Color", Color) = (0.4, 2.2, 0.55, 1)
        _ShieldIntensity("Shield Intensity", Range(0, 4)) = 2.2
        _ShellDistance("Shell Distance", Range(0, 0.1)) = 0.012
        _ScanSpeed("Scan Speed", Range(0, 10)) = 3.2
        _ScanWidth("Scan Width", Range(0.01, 0.4)) = 0.08
        _GridScale("Grid Scale", Range(1, 80)) = 22
        _GridStrength("Grid Strength", Range(0, 1)) = 0.7
        _RimPower("Rim Power", Range(0.4, 8)) = 1.35
        _PulseSpeed("Pulse Speed", Range(0, 12)) = 5.5
        _FlickerSpeed("Flicker Speed", Range(0, 40)) = 18
        _BaseDim("Base Dim", Range(0, 1)) = 0.2
        _FluorBoost("Fluor Boost", Range(0, 2)) = 0.85
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
            Name "SelectedForceShield"
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
                half4 _ScanColor;
                half _ShieldIntensity;
                half _ShellDistance;
                half _ScanSpeed;
                half _ScanWidth;
                half _GridScale;
                half _GridStrength;
                half _RimPower;
                half _PulseSpeed;
                half _FlickerSpeed;
                half _BaseDim;
                half _FluorBoost;
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
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
            };

            // Diamond / targeting lattice — different from planted hex cells.
            float DiamondGrid(float2 uv)
            {
                float2 p = abs(frac(uv) - 0.5);
                float d = abs(p.x + p.y - 0.5);
                float lineA = 1.0 - smoothstep(0.0, 0.045, d);
                float2 q = abs(frac(uv + 0.5) - 0.5);
                float d2 = abs(q.x - q.y);
                float lineB = 1.0 - smoothstep(0.0, 0.03, d2);
                return saturate(max(lineA, lineB * 0.85));
            }

            float Crosshair(float2 uv)
            {
                float2 c = uv - 0.5;
                float arms = max(
                    1.0 - smoothstep(0.0, 0.012, abs(c.x)) * step(abs(c.y), 0.28),
                    1.0 - smoothstep(0.0, 0.012, abs(c.y)) * step(abs(c.x), 0.28));
                float ring = abs(length(c) - 0.32);
                ring = 1.0 - smoothstep(0.0, 0.02, ring);
                float ring2 = abs(length(c) - 0.18);
                ring2 = 1.0 - smoothstep(0.0, 0.015, ring2);
                return saturate(arms * 0.55 + ring + ring2 * 0.7);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                float3 normalWS = NormalizeNormalPerVertex(normalInputs.normalWS);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(positionWS);

                float facing = dot(normalWS, viewDirWS);
                float3 shellDir = facing >= 0.0 ? normalWS : -normalWS;
                positionWS += shellDir * _ShellDistance;

                output.positionCS = TransformWorldToHClip(positionWS);
                #if UNITY_REVERSED_Z
                output.positionCS.z = min(output.positionCS.z + 1.0e-4 * output.positionCS.w, output.positionCS.w);
                #else
                output.positionCS.z = max(output.positionCS.z - 1.0e-4 * output.positionCS.w, -output.positionCS.w);
                #endif

                output.normalWS = normalWS;
                output.viewDirWS = viewDirWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

                // Fluorescent lift of the underlying color toward hot magenta.
                half3 fluorTint = lerp(baseColor.rgb, baseColor.rgb * _ShieldColor.rgb, _FluorBoost);
                fluorTint *= (1.0h - _BaseDim);

                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);
                float ndotv = saturate(abs(dot(normalWS, viewDirWS)));
                float fresnel = pow(1.0 - ndotv, _RimPower);

                float time = _Time.y;

                // Horizontal holographic scan band (unique vs planted noise wash).
                float scanPos = frac(input.uv.y * 0.85 - time * _ScanSpeed * 0.15);
                float scan = 1.0 - smoothstep(0.0, _ScanWidth, abs(scanPos - 0.5) * 2.0);
                scan = pow(scan, 1.5);

                // Secondary vertical strobe lines.
                float strobe = sin(input.uv.x * 70.0 + time * _FlickerSpeed);
                strobe = pow(saturate(strobe * 0.5 + 0.5), 8.0);

                float2 gridUv = input.uv * _GridScale;
                gridUv += viewDirWS.xz * 0.35;
                float grid = DiamondGrid(gridUv) * _GridStrength;

                float reticle = Crosshair(input.uv + viewDirWS.xz * 0.02);

                float pulse = 0.65 + 0.35 * sin(time * _PulseSpeed);
                float flicker = 0.85 + 0.15 * sin(time * _FlickerSpeed * 0.7);

                float shieldMask = saturate(
                    fresnel * 1.5
                    + scan * 0.95
                    + grid * 0.75
                    + strobe * 0.35
                    + reticle * 0.9);
                shieldMask *= pulse * flicker;

                half3 shieldRgb = _ShieldColor.rgb * (grid * 0.8 + scan * 0.55 + strobe * 0.4);
                shieldRgb += _ScanColor.rgb * (scan * 1.2 + strobe * 0.5);
                shieldRgb += _RimColor.rgb * (fresnel * 1.3 + reticle * 0.85);
                shieldRgb *= _ShieldIntensity * shieldMask;

                half3 finalRgb = fluorTint + shieldRgb;
                return half4(finalRgb, baseColor.a);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
