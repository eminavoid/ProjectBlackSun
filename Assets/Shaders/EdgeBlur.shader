Shader "Hidden/FaithEclipse/EdgeBlur"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "EdgeBlur"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _EdgeBlurRadius;
            float _EdgeBlurSharpArea;
            float _EdgeBlurFalloff;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 sharp = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float edge = smoothstep(_EdgeBlurSharpArea, _EdgeBlurSharpArea + _EdgeBlurFalloff, length((uv - 0.5) * 2.0));
                if (edge <= 0.001) return sharp;

                float2 reach = float2(_ScreenParams.y / _ScreenParams.x, 1.0) * _EdgeBlurRadius * edge;
                half4 sum = sharp;
                const int taps = 20;
                [unroll]
                for (int i = 1; i <= taps; i++)
                {
                    float spread = sqrt(i / (float)taps);
                    float angle = i * 2.39996323;
                    sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(cos(angle), sin(angle)) * spread * reach);
                }

                return sum / (taps + 1);
            }
            ENDHLSL
        }
    }
}
