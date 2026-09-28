Shader "Hidden/UIBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        // Separable gaussian (9 taps). _Direction is the per-tap UV step.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float2 _Direction;

            fixed4 frag(v2f_img i) : SV_Target
            {
                static const float w[5] = { 0.227027, 0.1945946, 0.1216216, 0.054054, 0.016216 };
                fixed4 c = tex2D(_MainTex, i.uv) * w[0];
                for (int k = 1; k < 5; k++)
                {
                    float2 o = _Direction * k;
                    c += tex2D(_MainTex, i.uv + o) * w[k];
                    c += tex2D(_MainTex, i.uv - o) * w[k];
                }
                // The screen capture doesn't come with a reliable alpha; a partly transparent result
                // lets the sharp UI underneath show through instead of hiding it.
                return fixed4(c.rgb, 1);
            }
            ENDCG
        }

        // Pass 1: copies the screen capture into the (sRGB) working texture. The capture holds
        // already gamma-encoded colors; without decoding them here they get encoded twice and the
        // result looks washed out.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            fixed4 frag(v2f_img i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                #ifdef UNITY_COLORSPACE_GAMMA
                return fixed4(c.rgb, 1);
                #else
                return fixed4(GammaToLinearSpace(c.rgb), 1);
                #endif
            }
            ENDCG
        }
    }
}
