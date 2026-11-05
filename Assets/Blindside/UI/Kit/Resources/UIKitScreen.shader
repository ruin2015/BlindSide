// Blindside/UI/KitScreen — a flat colour composited with CSS mix-blend-mode: screen (result = 1 − (1 − under)(1 − colour·opacity)), for
// KitTint with `screen` on (the roster card's hit flash). Fixed-function blend One / OneMinusSrcColor gives exactly that; the vertex
// layout is the KitDeco one (uv2 = colour sRGB RGBA, uv0.w = the alpha as authored, vertex alpha = with the CanvasGroup).
// In a Linear project the colour is converted so that over black it gives the mockup's sRGB value (over light colours the result is
// within a few % of the browser's). Shared material, no per-instance state.
Shader "Blindside/UI/KitScreen"
{
    Properties
    {
        [PerRendererData] _MainTex ("Unused", 2D) = "white" {}
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [HideInInspector] _ClipRect ("Clip Rect", Vector) = (-32767, -32767, 32767, 32767)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" }

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
        Blend One OneMinusSrcColor
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float4 uv0 : TEXCOORD0;
                float4 uv2 : TEXCOORD2;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float4 uv0 : TEXCOORD0;
                float4 colA : TEXCOORD1;
                float4 world : TEXCOORD2;
            };

            float4 _ClipRect;

            v2f vert(appdata v)
            {
                v2f o;
                o.world = v.vertex;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv0 = v.uv0;
                o.colA = v.uv2;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float opacity = i.colA.a * saturate(i.color.a / max(i.uv0.w, 0.001));
                float3 k = saturate(i.colA.rgb * opacity);
                #ifndef UNITY_COLORSPACE_GAMMA
                k = GammaToLinearSpace(k);
                #endif
                #ifdef UNITY_UI_CLIP_RECT
                k *= UnityGet2DClipping(i.world.xy, _ClipRect);
                #endif
                return fixed4(k, 0);
            }
            ENDCG
        }
    }
}
