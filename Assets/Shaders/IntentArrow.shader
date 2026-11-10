Shader "Custom/IntentArrow"
{
    Properties
    {
        [HDR] _Tint("Tint", Color) = (1, 1, 1, 1)
        _Alpha("Alpha", Range(0, 1)) = 1
        _Intensity("Intensity", Range(0, 4)) = 2.2
        _ChevronScale("Chevron Scale", Range(0.5, 12)) = 2.4
        _ChevronSharp("Chevron Sharpness", Range(0.4, 6)) = 1.35
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

            // Soft repeating cue. Fades out when a repeat is smaller than a few pixels,
            // so zoom never turns it into a stack of aliased strokes.
            half FlowMarks(float2 uv, half across)
            {
                float cell = uv.x * _ChevronScale - _Time.y * _ScrollSpeed;
                float fwCell = max(fwidth(cell), 1e-4);
                half lod = (half)(1.0 - smoothstep(0.22, 0.55, fwCell));

                float local = frac(cell);
                float skew = saturate(_ChevronSkew * 0.5);
                float ramp = saturate(local / lerp(0.72, 0.5, skew));
                float halfWidth = lerp(0.28, 0.92, pow(ramp, lerp(1.1, 0.75, skew)));
                float sdf = halfWidth - (float)across;
                float aa = max(fwidth(across) * max(_ChevronSharp, 0.4), 1e-3);
                half glyph = (half)smoothstep(-aa, aa, sdf);
                half seam = (half)(smoothstep(0.0, 0.16, local) * smoothstep(0.0, 0.2, 1.0 - local));
                return glyph * seam * lod;
            }

            half Body(half across)
            {
                float aa = max(fwidth(across), 1e-3);
                half edge = (half)(1.0 - smoothstep(1.0 - aa * 2.2, 1.0, (float)across));
                half falloff = (half)(1.0 - smoothstep(_EdgeFade, 1.0, (float)across));
                return edge * falloff;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half across = abs(input.uv.y - 0.5h) * 2.0h;
                half body = Body(across);
                half tail = smoothstep(0.0h, max(_TailFade, 0.02h), input.uv.x);
                half head = smoothstep(0.62h, 0.98h, input.uv.x);
                half spine = pow(saturate(1.0h - across), 1.6h) * _CoreGlow;
                half pulse = 0.84h + 0.16h * sin(_Time.y * _PulseSpeed);

                half mask;
                bool seedBeacon = input.uv2.x > 0.5;
                bool ring = input.uv2.y > 0.5;

                if (seedBeacon && ring)
                {
                    float travel = frac(input.uv.x - _Time.y * _ScrollSpeed * 0.18);
                    float dist = min(travel, 1.0 - travel);
                    float aa = max(fwidth(input.uv.x), 1e-4);
                    half orbit = (half)(1.0 - smoothstep(0.06, 0.06 + aa * 8.0, dist));
                    half flash = 0.7h + 0.3h * sin(_Time.y * _PulseSpeed);
                    mask = body * (0.55h + orbit * 0.9h) * flash;
                }
                else if (seedBeacon)
                {
                    float travel = frac(input.uv.x * 2.0 - _Time.y * _ScrollSpeed * 0.85);
                    float dist = abs(travel - 0.5);
                    half packet = pow(saturate(1.0h - (half)dist * 2.4h), 2.0h);
                    half column = pow(saturate(1.0h - across), 1.35h);
                    half tip = smoothstep(0.72h, 1.0h, input.uv.x);
                    mask = body * saturate(packet * 0.85h + column * 0.5h + tip * 0.65h) * pulse;
                }
                else
                {
                    half marks = FlowMarks(input.uv, across);
                    half shaft = 1.0h - smoothstep(0.68h, 0.9h, input.uv.x);
                    mask = body * tail * saturate(marks * shaft + spine + head) * pulse;
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
