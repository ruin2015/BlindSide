// Blindside/UI/Segmented — a segmented bar drawn in one pass from vertex data, so every bar shares ONE material (batches, no
// per-bar instances). SegmentedBar writes the parameters into the vertices:
//   color (vertex)  fill colour (the Graphic's colour; a CanvasGroup's alpha lands here)
//   uv0   (u 0..1 along the bar, width in canvas units, fill colour alpha as authored, empty colour alpha)
//   uv1   (fill ratio, ghost ratio, segment width, gap)
//   uv2   ghost colour RGBA
//   normal  empty colour RGB
// Pattern (CSS: repeating-linear-gradient(90deg, c 0 6px, transparent 6px 8px)): segments of `segment` then `gap` from the left;
// x <= fill → fill colour, x <= ghost → ghost colour, else the empty colour. UI stencil and RectMask2D clipping are supported.
Shader "Blindside/UI/Segmented"
{
    Properties
    {
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
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }

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
                float4 uv1 : TEXCOORD1;
                float4 uv2 : TEXCOORD2;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 uv2 : TEXCOORD2;
                float3 emptyRgb : TEXCOORD3;
                float4 world : TEXCOORD4;
            };

            float4 _ClipRect;

            v2f vert(appdata v)
            {
                v2f o;
                o.world = v.vertex;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv0 = v.uv0;
                o.uv1 = v.uv1;
                o.uv2 = v.uv2;
                o.emptyRgb = v.normal;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float width = i.uv0.y;
                float x = i.uv0.x * width;
                // n = floor((width + gap) / (segment + gap)) whole cells; the width left over widens every segment a little so the
                // last cell ends exactly on the bar's right end (no sliver). Fill and ghost are read on this same grid.
                float gap = i.uv1.w;
                float n = max(floor((width + gap) / max(i.uv1.z + gap, 0.001) + 0.001), 1.0);
                float segment = (width - (n - 1.0) * gap) / n;
                float period = max(segment + gap, 0.001);

                float cell = min(floor(x / period), n - 1.0);
                float local = x - cell * period;
                float aa = max(fwidth(x), 0.0001);
                // inside a segment, with a one-pixel soft edge; the bar's own ends stay square
                float inSeg = saturate((segment - local) / aa + 0.5) * saturate(local / aa + 0.5);

                float groupAlpha = i.color.a / max(i.uv0.z, 0.001);
                float p = x / max(width, 0.001);
                fixed4 fillCol = i.color;
                fixed4 ghostCol = fixed4(i.uv2.rgb, i.uv2.a * groupAlpha);
                fixed4 emptyCol = fixed4(i.emptyRgb, i.uv0.w * groupAlpha);

                fixed4 col = p <= i.uv1.x ? fillCol : (p <= i.uv1.y ? ghostCol : emptyCol);
                col.a *= inSeg;

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(i.world.xy, _ClipRect);
                #endif
                return col;
            }
            ENDCG
        }
    }
}
