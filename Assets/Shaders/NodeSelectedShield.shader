Shader "Custom/NodeSelectedShield"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [HDR] _ShieldColor("Selection Color", Color) = (2.4, 0.28, 2.8, 1)
        [HDR] _RimColor("Edge Color", Color) = (2.8, 1.85, 0.4, 1)
        _TintStrength("Tint Strength", Range(0, 1)) = 0.68
        _ShieldIntensity("Edge Intensity", Range(0, 6)) = 2.6
        _RimPower("Rim Power", Range(0.4, 8)) = 1.15
        _EdgeStrength("Crease Strength", Range(0, 3)) = 1.45
        _ScanSpeed("Scan Speed", Range(0, 2)) = 0.28
        _ScanPixels("Scan Width (px)", Range(1, 24)) = 9
        _PulseSpeed("Pulse Speed", Range(0, 8)) = 2.1
        [HideInInspector] _ZoneCenter("Zone Center", Vector) = (0, 0, 0, 0)
        [HideInInspector] _ZoneExtents("Zone Extents", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Unlit"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "SelectedForceShield"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _ShieldColor;
                half4 _RimColor;
                half _TintStrength;
                half _ShieldIntensity;
                half _RimPower;
                half _EdgeStrength;
                half _ScanSpeed;
                half _ScanPixels;
                half _PulseSpeed;
                float4 _ZoneCenter;
                float4 _ZoneExtents;
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
            };

            // Vertices stay on the authored mesh. Extruding along normals splits every hard
            // corner of a house, and a fixed world offset changes size with zoom.
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half3 SelectionHue(half3 color)
            {
                half peak = max(color.r, max(color.g, color.b));
                return peak > 0.001h ? color / peak : color;
            }

            // Hard edges stay about a pixel wide at any zoom: fwidth measures the normal
            // change between neighboring pixels, not a world or UV distance.
            float Crease(float3 normalWS)
            {
                return smoothstep(0.18, 0.7, length(fwidth(normalWS)));
            }

            // One bar crossing the node. Width is measured in pixels, so close-up and
            // city zoom show the same stroke. Screen Y is the fallback before bounds exist.
            float ScanBar(float2 positionWSXZ, float4 positionCS, float phaseOffset)
            {
                float3 right = mul((float3x3)unity_CameraToWorld, float3(1, 0, 0));
                float2 axis = right.xz;
                float axisLen = length(axis);
                axis = axisLen > 1e-4 ? axis / axisLen : float2(1, 0);
                float reach = _ZoneExtents.x * abs(axis.x) + _ZoneExtents.y * abs(axis.y);

                float along01 = positionCS.y / max(_ScreenParams.y, 1.0);
                if (reach > 1e-4)
                {
                    float along = dot(positionWSXZ - _ZoneCenter.xz, axis);
                    along01 = saturate(along / (reach * 2.0) + 0.5);
                }

                float fw = max(fwidth(along01), 1e-5);
                float phase = frac(_Time.y * _ScanSpeed + phaseOffset);
                float dist = abs(along01 - phase);
                dist = min(dist, 1.0 - dist);
                float distPx = dist / fw;
                float width = max(_ScanPixels, 1.0);
                float core = 1.0 - smoothstep(width * 0.2, width, distPx);
                float shoulder = 1.0 - smoothstep(width, width * 5.5, distPx);
                return saturate(core + shoulder * 0.55);
            }

            half3 Shade(float3 positionWS, float3 normalWS, float4 positionCS, half3 albedo)
            {
                InputData inputData = (InputData)0;
                inputData.positionWS = positionWS;
                inputData.positionCS = positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(positionWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                half3 gi = SampleSHPixel(SampleSHVertex(normalWS), normalWS);
                Light mainLight = GetMainLight(inputData.shadowCoord);
                half3 diffuse = gi + LightingLambert(
                    mainLight.color * (mainLight.shadowAttenuation * mainLight.distanceAttenuation),
                    mainLight.direction,
                    normalWS);

                #if defined(_ADDITIONAL_LIGHTS)
                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light light = GetAdditionalLight(lightIndex, inputData.positionWS);
                    diffuse += LightingLambert(
                        light.color * (light.shadowAttenuation * light.distanceAttenuation),
                        light.direction,
                        normalWS);
                LIGHT_LOOP_END
                #endif

                half3 lit = albedo * diffuse;

                #if defined(_SCREEN_SPACE_OCCLUSION)
                AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(inputData.normalizedScreenSpaceUV);
                lit *= ao.directAmbientOcclusion;
                #endif

                return lit;
            }

            void frag(
                Varyings input
                , out half4 outColor : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                half3 lit = Shade(input.positionWS, normalWS, input.positionCS, baseSample.rgb);
                half3 hue = SelectionHue(_ShieldColor.rgb);
                float breath = 0.62 + 0.38 * sin(_Time.y * _PulseSpeed);
                half3 emissive = hue * (0.28h + 0.34h * (half)breath);
                half3 tinted = lerp(lit, lit * hue * 1.25h + emissive, _TintStrength);

                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _RimPower);
                float crease = saturate(Crease(normalWS) * _EdgeStrength);
                float scan = ScanBar(input.positionWS.xz, input.positionCS, 0.0);
                float scanBack = ScanBar(input.positionWS.xz, input.positionCS, 0.5);

                half3 glow = _ShieldColor.rgb * (0.42h + (half)fresnel * 0.9h) * (half)breath;
                glow += _RimColor.rgb * ((half)fresnel * 1.7h + (half)crease * 1.55h);
                glow += _RimColor.rgb * ((half)scan * 2.1h + (half)scanBack * 0.85h);
                glow *= _ShieldIntensity;

                half3 finalRgb = tinted + glow;
                half peak = max(finalRgb.r, max(finalRgb.g, finalRgb.b));
                if (peak > 4.2h) finalRgb *= 4.2h / peak;
                outColor = half4(finalRgb, baseSample.a);

                #ifdef _WRITE_RENDERING_LAYERS
                outRenderingLayers = EncodeMeshRenderingLayer();
                #endif
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_TARGET
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode" = "DepthNormalsOnly" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthNormalsVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            void DepthNormalsFragment(
                Varyings input
                , out half4 outNormalWS : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                #if defined(_GBUFFER_NORMALS_OCT)
                    float3 normalWS = normalize(input.normalWS);
                    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                    float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
                    half3 packedNormalWS = half3(PackFloat2To888(remappedOctNormalWS));
                    outNormalWS = half4(packedNormalWS, 0.0);
                #else
                    outNormalWS = half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
                #endif

                #ifdef _WRITE_RENDERING_LAYERS
                    outRenderingLayers = EncodeMeshRenderingLayer();
                #endif
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
