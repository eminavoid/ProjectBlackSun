Shader "Universal Render Pipeline/Custom/LitBandDitherFade"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1,1,1,1)
        _BaseMap ("Albedo Texture", 2D) = "white" {}
        
        [Header(Near Fade)]
        _NearFadeStart ("Near Fade Start (Solid)", Float) = 3.0
        _NearFadeEnd ("Near Fade End (Invisible)", Float) = 1.0
        
        [Header(Far Fade)]
        _FarFadeStart ("Far Fade Start (Solid)", Float) = 15.0
        _FarFadeEnd ("Far Fade End (Invisible)", Float) = 20.0
    }

    SubShader
    {
        Tags 
        { 
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // URP shadow keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float2 uv           : TEXCOORD2;
                float4 screenPos    : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseMap_ST;
                float _NearFadeStart;
                float _NearFadeEnd;
                float _FarFadeStart;
                float _FarFadeEnd;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            // 4x4 Bayer Dither Matrix for screen-door transparency
            float Bayer4(int2 uv)
            {
                const float matrix4x4[16] = {
                    0.0625, 0.5625, 0.1875, 0.6875,
                    0.8125, 0.3125, 0.9375, 0.4375,
                    0.2500, 0.7500, 0.1250, 0.6250,
                    1.0000, 0.5000, 0.8750, 0.3750
                };
                int x = uv.x & 3;
                int y = uv.y & 3;
                return matrix4x4[x + y * 4];
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.screenPos = ComputeScreenPos(output.positionCS);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 1. Calculate world space distance from camera
                float dist = distance(input.positionWS, GetCameraPositionWS());

                // 2. Calculate near fade (0 when >= _NearFadeStart, 1 when <= _NearFadeEnd)
                float nearFade = saturate((_NearFadeStart - dist) / max(0.0001, _NearFadeStart - _NearFadeEnd));

                // 3. Calculate far fade (0 when <= _FarFadeStart, 1 when >= _FarFadeEnd)
                float farFade = saturate((dist - _FarFadeStart) / max(0.0001, _FarFadeEnd - _FarFadeStart));

                // 4. Combine both: 0 in the visible sweet spot, ramping to 1 at either extreme
                float fadeAmount = max(nearFade, farFade);

                // 5. Compute screen pixel coordinates and discard pixels
                int2 pixelCoord = int2((input.screenPos.xy / input.screenPos.w) * _ScreenParams.xy);
                float ditherThreshold = Bayer4(pixelCoord);

                clip(ditherThreshold - fadeAmount - 0.001);

                // 6. Sample texture and color
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

                // 7. Simple URP lighting calculation
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 normalWS = normalize(input.normalWS);
                half NdotL = saturate(dot(normalWS, mainLight.direction));
                half3 lighting = mainLight.color * (NdotL * mainLight.shadowAttenuation);
                
                // Add soft ambient term
                lighting += half3(0.15, 0.15, 0.15);

                half3 finalColor = albedo.rgb * lighting;

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }

        // Shadow Caster Pass so objects cast proper shadows while solid
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseMap_ST;
                float _NearFadeStart;
                float _NearFadeEnd;
                float _FarFadeStart;
                float _FarFadeEnd;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float dist = distance(input.positionWS, GetCameraPositionWS());
                float nearFade = saturate((_NearFadeStart - dist) / max(0.0001, _NearFadeStart - _NearFadeEnd));
                float farFade = saturate((dist - _FarFadeStart) / max(0.0001, _FarFadeEnd - _FarFadeStart));
                clip(0.5 - max(nearFade, farFade));
                return 0;
            }
            ENDHLSL
        }
    }
}