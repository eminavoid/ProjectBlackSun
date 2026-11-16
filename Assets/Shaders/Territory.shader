Shader "Custom/Territory"
{
    Properties
    {
        _Color("Owner Color", Color) = (1, 1, 1, 1)
        _Color2("Rival Color", Color) = (1, 1, 1, 1)
        _Mode("Mode", Float) = 0
        _Alpha("Alpha", Range(0, 1)) = 0.5
        _Lift("Lift", Float) = 0.004

        [Header(Zone)]
        _Center("Center (XZ)", Vector) = (0, 0, 0, 0)
        _Radius("Radius", Float) = 0.4
        _Seed("Seed", Float) = 0

        [Header(Paint)]
        _NoiseScale("Paint Noise Per Unit", Float) = 7
        _NoiseStrength("Paint Noise Strength", Range(0, 1)) = 0.35
        _NoiseSpeed("Paint Drift Speed", Float) = 0.05

        [Header(Held)]
        _PulseSpeed("Pulse Speed", Float) = 1.8
        _RingSpacing("Ring Spacing", Float) = 0.55
        _RingSpeed("Ring Speed", Float) = 0.22
        _RingWidth("Ring Width", Range(0.01, 0.5)) = 0.12
        _RingStrength("Ring Strength", Range(0, 1)) = 0.45

        [Header(Stripes)]
        _StripeScale("Stripes Per Unit", Float) = 12
        _StripeWidth("Stripe Width", Range(0.05, 0.95)) = 0.45
        _StripeSpeed("Stripe March Speed", Float) = 0.18
        _ContestSpeed("Contested March Speed", Float) = 0.7
        _SheenSpeed("Sheen Speed", Float) = 0.12
        _SheenStrength("Sheen Strength", Range(0, 1)) = 0.6

        [Header(Owner Change)]
        _Flash("Flash", Range(0, 1)) = 0
        _FlashT("Flash Progress", Range(0, 1)) = 0
        _FlashColor("Flash Color", Color) = (1, 1, 1, 1)
        _FlashAlpha("Flash Max Alpha", Range(0, 1)) = 0.65

        [Header(Readability)]
        _ShadowStrength("House Shadows On Tint", Range(0, 1)) = 0.75
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent-20"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Territory"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _Color2;
                half4 _FlashColor;
                half _FlashAlpha;
                half _ShadowStrength;
                float4 _Center;
                float _Mode;
                half _Alpha;
                float _Lift;
                float _Radius;
                float _Seed;
                float _NoiseScale;
                half _NoiseStrength;
                float _NoiseSpeed;
                float _PulseSpeed;
                float _RingSpacing;
                float _RingSpeed;
                half _RingWidth;
                half _RingStrength;
                float _StripeScale;
                half _StripeWidth;
                float _StripeSpeed;
                float _ContestSpeed;
                float _SheenSpeed;
                half _SheenStrength;
                half _Flash;
                half _FlashT;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                positionWS.y += _Lift;
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash(i);
                float b = Hash(i + float2(1, 0));
                float c = Hash(i + float2(0, 1));
                float d = Hash(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float Paint(float2 xz, float time)
            {
                float2 p = xz * _NoiseScale + _Seed * 17.0;
                float drift = time * _NoiseSpeed;
                float n = ValueNoise(p + float2(drift, -drift * 0.7)) * 0.65;
                n += ValueNoise(p * 2.3 - float2(drift * 1.3, drift)) * 0.35;
                return n;
            }

            half Stripe(float2 xz, float march)
            {
                float s = (xz.x + xz.y) * _StripeScale + march;
                float aa = max(fwidth(s), 1e-4);
                float d = abs(frac(s) - 0.5);
                float halfWidth = _StripeWidth * 0.5;
                return 1.0h - (half)smoothstep(halfWidth - aa, halfWidth + aa, d);
            }

            half Rings(float r, float time)
            {
                float phase = r / _RingSpacing - time * _RingSpeed + _Seed;
                float d = abs(frac(phase) - 0.5) * 2.0;
                half ring = (half)smoothstep(1.0 - _RingWidth * 2.0, 1.0, 1.0 - d);
                return ring * (half)saturate(1.15 - r);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float time = _Time.y;
                float2 xz = input.positionWS.xz;
                float r = length(xz - _Center.xy) / max(_Radius, 1e-3);
                half paint = (half)Paint(xz, time);
                half paintMod = lerp(1.0h, 0.7h + paint * 0.6h, _NoiseStrength);

                half3 color = _Color.rgb;
                half alpha = _Alpha * paintMod;

                if (_Mode > 0.5 && _Mode < 1.5)
                {
                    alpha *= 0.86h + 0.14h * (half)sin(time * _PulseSpeed + _Seed * 6.28);
                    half ring = Rings(r, time) * _RingStrength;
                    color = lerp(color, saturate(color * 1.6h + 0.25h), ring);
                    alpha = saturate(alpha + ring * 0.35h);
                }
                else if (_Mode > 1.5 && _Mode < 2.5)
                {
                    half stripe = Stripe(xz, time * _StripeSpeed);
                    stripe *= lerp(1.0h, 0.65h + paint * 0.5h, _NoiseStrength);
                    color = lerp(color, color * 0.2h, stripe);
                    alpha *= lerp(0.45h, 1.0h, stripe);

                    float sweep = frac((xz.x - xz.y) / max(_Radius, 1e-3) * 0.25 - time * _SheenSpeed + _Seed);
                    half sheen = (half)smoothstep(0.08, 0.0, abs(sweep - 0.5)) * _SheenStrength * (1.0h - stripe);
                    color = lerp(color, saturate(color * 1.5h + 0.3h), sheen);
                    alpha = saturate(alpha + sheen * 0.3h);
                }
                else if (_Mode > 2.5 && _Mode < 3.5)
                {
                    half stripe = Stripe(xz, time * _ContestSpeed);
                    color = lerp(_Color.rgb, _Color2.rgb, stripe);
                    alpha *= 0.82h + 0.18h * (half)sin(time * 7.0 + _Seed * 6.28 + r * 4.0);
                }
                else if (_Mode > 3.5)
                {
                    half border = (half)smoothstep(0.55, 0.95, r);
                    half beat = 0.55h + 0.45h * (half)sin(time * 3.0 + _Seed * 6.28);
                    color = saturate(_Color.rgb * 1.25h + 0.1h);
                    alpha = _Alpha * (0.18h + border * beat);
                }

                half wave = 0.0h;
                if (_Flash > 0.001h)
                {
                    float front = _FlashT * 1.6;
                    wave = (half)exp(-pow((r - front) / 0.18, 2.0)) * (1.0h - _FlashT);
                }

                half flare = saturate(_Flash * 0.55h + wave);
                color = lerp(color, _FlashColor.rgb, flare);
                alpha = saturate(max(alpha, flare * _FlashAlpha));

                half shadow = MainLightRealtimeShadow(TransformWorldToShadowCoord(input.positionWS));
                color *= lerp(1.0h, shadow, _ShadowStrength);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
