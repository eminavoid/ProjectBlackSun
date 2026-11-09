Shader "Custom/TutorialMark"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 0.84, 0.42, 1)
        _Size ("Size", Vector) = (200, 80, 0, 0)
        _Core ("Core Width", Float) = 4
        _Glow ("Glow Width", Float) = 28
        _Corner ("Corner Length", Float) = 42

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _Size;
            float _Core;
            float _Glow;
            float _Corner;
            float4 _ClipRect;

            v2f vert(appdata v)
            {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 size = max(_Size.xy, float2(1, 1));
                float nearX = min(i.uv.x, 1 - i.uv.x) * size.x;
                float nearY = min(i.uv.y, 1 - i.uv.y) * size.y;
                float dist = min(nearX, nearY);

                float pulse = 0.72 + 0.28 * sin(_Time.y * 3.4);
                float core = 1 - smoothstep(_Core * 0.2, _Core, dist);
                float glow = (1 - smoothstep(_Core, _Glow, dist)) * pulse;
                float corner = saturate(1 - nearX / _Corner) * saturate(1 - nearY / _Corner);
                float bracket = corner * (1 - smoothstep(_Core * 2.2, _Core * 3.4, dist));
                float wash = (1 - smoothstep(_Glow, min(size.x, size.y) * 0.45, dist)) * 0.16 * pulse;

                float alpha = saturate(max(core, glow * 0.9) + bracket * 0.85 + wash);
                float3 hot = float3(1, 0.97, 0.82);
                float3 col = lerp(i.color.rgb, hot, saturate(core + bracket));
                alpha *= i.color.a;
                alpha *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                return float4(col, alpha);
            }
            ENDCG
        }
    }
}
