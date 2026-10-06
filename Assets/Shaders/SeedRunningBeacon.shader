Shader "Custom/SeedRunningBeacon"
{
    // Marca que se dibuja encima del overlay: acá corre una seed.
    // El tinte es la facción. El jugador suma una cruz blanca.
    Properties
    {
        [HDR] _ShieldColor("Faction Color", Color) = (1.2, 0.72, 0.2, 1)
        [HDR] _RimColor("Rim Color", Color) = (1, 0.9, 0.6, 1)
        _ShieldIntensity("Intensity", Range(0, 4)) = 2.2
        _ShellDistance("Shell Distance", Range(0, 0.2)) = 0.02
        _Lift("World Lift", Range(0, 80)) = 0
        _PulseSpeed("Pulse Speed", Range(0, 8)) = 2.2
        _IsPlayer("Is Player", Range(0, 1)) = 0
        _HasFaction("Has Faction", Range(0, 1)) = 0
        _Selected("Selected", Range(0, 1)) = 0
        _RunProgress("Run Progress", Range(0, 1)) = 0
        _ZoneCenter("Zone Center", Vector) = (0, 0, 0, 0)
        _ZoneExtent("Zone Extent", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+80"
            "IgnoreProjector" = "True"
            "UniversalMaterialType" = "Unlit"
        }

        Pass
        {
            Name "SeedRunningBeacon"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off
            Offset -2, -2

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShieldColor;
                half4 _RimColor;
                half _ShieldIntensity;
                half _ShellDistance;
                half _Lift;
                half _PulseSpeed;
                half _IsPlayer;
                half _HasFaction;
                half _Selected;
                half _RunProgress;
                float4 _ZoneCenter;
                half _ZoneExtent;
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
                positionWS.y += _Lift;

                output.positionCS = TransformWorldToHClip(positionWS);
                #if UNITY_REVERSED_Z
                output.positionCS.z = min(output.positionCS.z + 2.0e-4 * output.positionCS.w, output.positionCS.w);
                #else
                output.positionCS.z = max(output.positionCS.z - 2.0e-4 * output.positionCS.w, -output.positionCS.w);
                #endif

                output.positionWS = positionWS;
                output.normalWS = normalWS;
                output.viewDirWS = viewDirWS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float extent = max(_ZoneExtent, 0.001);
                float2 local = (input.positionWS.xz - _ZoneCenter.xz) / extent;
                float radius = length(local);
                float around = frac(atan2(local.y, local.x) * 0.15915494 + 0.5);

                half3 raw = max(_ShieldColor.rgb, 0.0h);
                half peak = max(raw.r, max(raw.g, raw.b));
                half3 tint = peak > 0.001h ? raw / peak : half3(1.0h, 0.85h, 0.4h);

                float beat = 0.5 + 0.5 * sin(_Time.y * _PulseSpeed);
                if (_IsPlayer > 0.5h)
                {
                    beat = max(beat, 0.5 + 0.5 * sin(_Time.y * _PulseSpeed * 2.0 + 0.6));
                }

                half pulse = (half)lerp(0.72, 1.0, beat);

                float core = pow(saturate(1.0 - radius / 0.16), 1.5);
                float ringDist = abs(radius - 0.7);
                float ring = saturate(1.0 - smoothstep(0.02, 0.075, ringDist));
                float ticks = frac(around * 11.0 - _Time.y * max(_PulseSpeed, 0.2) * 0.42);
                float dash = smoothstep(0.0, 0.06, ticks) * smoothstep(0.34, 0.1, ticks);
                float ringGlow = ring * (0.28 + dash * 1.35);

                float wedge = saturate(1.0 - smoothstep(0.01, 0.11, ringDist));
                wedge *= step(around, saturate(_RunProgress));

                float mark = 0.0;
                if (_IsPlayer > 0.5h)
                {
                    float armX = (1.0 - smoothstep(0.0, 0.06, abs(local.x))) * step(abs(local.y), 0.68);
                    float armY = (1.0 - smoothstep(0.0, 0.06, abs(local.y))) * step(abs(local.x), 0.68);
                    mark = max(armX, armY);
                }
                else if (_HasFaction < 0.5h)
                {
                    float box = max(abs(local.x), abs(local.y));
                    mark = saturate(1.0 - smoothstep(0.018, 0.06, abs(box - 0.62)));
                }

                float sweep = frac(input.positionWS.y / extent - _Time.y * 0.32);
                float band = pow(saturate(1.0 - abs(sweep - 0.32) * 9.0), 2.0);

                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);
                float fresnel = pow(1.0 - saturate(abs(dot(normalWS, viewDirWS))), 2.4);

                float energy = core * 1.45 + ringGlow * 1.25 + wedge * 1.6 + mark * 1.35 + band * 0.75 + fresnel * fresnel;
                if (_Selected > 0.5h)
                {
                    energy += fresnel * (0.35 + 0.65 * frac(_Time.y * 1.7));
                }

                clip(energy - 0.06);

                half3 rgb = tint * (half)energy * _ShieldIntensity * pulse;
                rgb += _RimColor.rgb * (half)(wedge + fresnel * 0.35) * 0.45h;
                if (_IsPlayer > 0.5h)
                {
                    rgb += half3(1.0h, 0.94h, 1.0h) * (half)mark * 0.9h * pulse;
                }

                half outPeak = max(rgb.r, max(rgb.g, rgb.b));
                if (outPeak > 1.7h) rgb *= 1.7h / outPeak;

                return half4(rgb, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
