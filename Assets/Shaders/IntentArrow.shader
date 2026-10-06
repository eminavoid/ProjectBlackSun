Shader "Custom/IntentArrow"
{
    Properties
    {
        [HDR] _Tint("Tint", Color) = (1, 1, 1, 1)
        _Alpha("Alpha", Range(0, 1)) = 1
        _Intensity("Intensity", Range(0, 4)) = 2.2
        _ChevronScale("Chevron Scale", Range(1, 60)) = 9
        _ChevronSharp("Chevron Sharpness", Range(1, 16)) = 6
        _ChevronSkew("Chevron Skew", Range(0, 2)) = 0.85
        _ScrollSpeed("Scroll Speed", Range(0, 6)) = 1.7
        _EdgeFade("Edge Fade", Range(0, 1)) = 0.42
        _TailFade("Tail Fade", Range(0, 0.6)) = 0.12
        _CoreGlow("Core Glow", Range(0, 1)) = 0.55
        _PulseSpeed("Pulse Speed", Range(0, 12)) = 3.4
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+50"
            "IgnoreProjector" = "True"
            "UniversalMaterialType" = "Unlit"
        }

        Pass
        {
            Name "IntentArrow"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma target 4.5
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
                float2 uv2 : TEXCOORD1;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
                half4 color : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.uv2 = input.uv2;
                output.color = input.color;
                return output;
            }

            half Arrowheads(float2 uv)
            {
                half across = abs(uv.y - 0.5h) * 2.0h;
                float x = frac(uv.x * _ChevronScale - _Time.y * _ScrollSpeed);
                float skew = saturate(_ChevronSkew * 0.5h);
                float reach = lerp(0.48, 0.7, skew);
                float halfWidth = lerp(0.98, 0.03, saturate(pow(saturate(x / reach), lerp(0.7, 1.45, skew))));
                half glyph = saturate(((half)halfWidth - across) * max(_ChevronSharp, 1.0h));
                half tip = saturate(1.0h - abs(x - reach * 0.92) * 8.0h) * step(across, 0.4h);
                return saturate(glyph + tip * 0.7h);
            }

            half4 frag(Varyings input) : SV_Target
            {
                half across = abs(input.uv.y - 0.5h) * 2.0h;
                half body = 1.0h - smoothstep(_EdgeFade, 1.0h, across);
                half tail = smoothstep(0.0h, _TailFade, input.uv.x);
                half head = smoothstep(0.78h, 0.92h, input.uv.x);
                half spine = pow(saturate(1.0h - across), 2.0h) * _CoreGlow;
                half pulse = 0.82h + 0.18h * sin(_Time.y * _PulseSpeed);

                half mask;
                bool seedBeacon = input.uv2.x > 0.5;
                bool ring = input.uv2.y > 0.5;

                if (seedBeacon && ring)
                {
                    float ticks = frac(input.uv.x * 14.0 - _Time.y * _ScrollSpeed);
                    half marks = smoothstep(0.0h, 0.04h, (half)ticks) * smoothstep(0.22h, 0.06h, (half)ticks);
                    half band = 1.0h - smoothstep(0.15h, 0.95h, across);
                    half flash = 0.55h + 0.45h * sin(_Time.y * _PulseSpeed);
                    mask = band * saturate(0.35h + marks) * flash;
                }
                else if (seedBeacon)
                {
                    float dash = frac(input.uv.x * 6.0 - _Time.y * _ScrollSpeed * 1.35);
                    half packets = pow(saturate(1.0h - abs((half)dash - 0.22h) * 4.5h), 1.6h);
                    half column = pow(saturate(1.0h - across), 1.4h);
                    half tip = smoothstep(0.8h, 1.0h, input.uv.x);
                    mask = body * saturate(packets * 1.15h + column * 0.4h + tip) * pulse;
                }
                else
                {
                    half chevron = Arrowheads(input.uv);
                    half shaft = step(input.uv.x, 0.8h);
                    mask = body * tail * saturate(chevron * shaft * 1.25h + spine * shaft + head) * pulse;
                }

                half3 rgb = _Tint.rgb * input.color.rgb * _Intensity;
                half alpha = mask * _Alpha * input.color.a;
                return half4(rgb * alpha, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
