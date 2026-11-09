Shader "Custom/DistrictExplain"
{
    Properties
    {
        _Color("District Color", Color) = (1, 1, 1, 1)
        _Strength("Strength", Range(0, 1)) = 0.62
        _PulseSpeed("Pulse Speed", Range(0, 4)) = 1.6
        _Lift("World Lift", Range(0, 2)) = 0.06
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "DistrictExplain"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Offset -2, -2

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Strength;
                half _PulseSpeed;
                half _Lift;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz) + normalWS * _Lift;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = normalWS;
                output.viewDirWS = GetWorldSpaceViewDir(positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normalWS);
                float3 view = normalize(input.viewDirWS);
                half fresnel = pow(1.0 - saturate(dot(normal, view)), 1.6);
                half pulse = 0.72 + 0.28 * sin(_Time.y * _PulseSpeed);
                half alpha = saturate(_Strength * (0.55 + 0.45 * fresnel) * pulse);
                return half4(_Color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
