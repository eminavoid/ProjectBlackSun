Shader "Custom/IntentArrow"
{
    Properties
    {
        [HDR] _Tint("Tint", Color) = (1, 1, 1, 1)
        _Alpha("Alpha", Range(0, 1)) = 1
        _Intensity("Intensity", Range(0, 4)) = 1.6
        _ChevronScale("Chevron Scale", Range(1, 60)) = 14
        _ChevronSharp("Chevron Sharpness", Range(1, 16)) = 4
        _ChevronSkew("Chevron Skew", Range(0, 2)) = 0.55
        _ScrollSpeed("Scroll Speed", Range(0, 6)) = 1.1
        _EdgeFade("Edge Fade", Range(0, 1)) = 0.55
        _TailFade("Tail Fade", Range(0, 0.6)) = 0.18
        _CoreGlow("Core Glow", Range(0, 1)) = 0.3
        _PulseSpeed("Pulse Speed", Range(0, 12)) = 3.2
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
            Name "IntentArrow"
            Tags { "LightMode" = "UniversalForward" }

            // Aditivo con color premultiplicado por alpha en el fragment.
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Alpha;
                half _Intensity;
                half _ChevronScale;
                half _ChevronSharp;
                half _ChevronSkew;
                half _ScrollSpeed;
                half _EdgeFade;
                half _TailFade;
                half _CoreGlow;
                half _PulseSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // u = avance a lo largo del recorrido, v = ancho de la cinta.
                half across = abs(input.uv.y - 0.5h) * 2.0h;
                half body = 1.0h - smoothstep(_EdgeFade, 1.0h, across);

                // El sesgo por ancho convierte las bandas en galones que apuntan al destino.
                float phase = input.uv.x * _ChevronScale - _Time.y * _ScrollSpeed + across * _ChevronSkew;
                half chevron = pow(saturate(sin(phase * 6.2831853) * 0.5h + 0.5h), _ChevronSharp);

                half tail = smoothstep(0.0h, _TailFade, input.uv.x);
                half core = _CoreGlow * (1.0h - across);
                half pulse = 0.78h + 0.22h * sin(_Time.y * _PulseSpeed);

                half mask = body * tail * saturate(chevron + core) * pulse;

                half3 rgb = _Tint.rgb * input.color.rgb * _Intensity;
                half alpha = mask * _Alpha * input.color.a;

                return half4(rgb * alpha, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
