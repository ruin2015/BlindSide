// Blindside/UI/KitDeco — the kit's drawn layers (KitShaderGraphic and its subclasses) in one pass. Like UISegmented / UIScreenFX every
// parameter travels in the mesh vertices, so ONE shared material serves every stripe, sweep, shadow and bracket (they batch):
//   uv0     (x, y local position from the rect's bottom-left — canvas units, mode, the graphic's alpha as authored)
//   uv1     mode parameters (below)
//   uv2     colour A, sRGB values RGBA
//   uv3     colour B, sRGB values RGBA
//   tangent (u, y in the unsheared shape — for the shadow, sigma, unused)
//   normal  the colour the layer sits on, sRGB values (for the alpha match, see Out)
//   color   the Graphic's colour; only its alpha is used: a CanvasGroup's alpha lands there (÷ uv0.w = the group's share)
// Modes:
//   0 flat     colour A (CornerBrackets)
//   1 hazard   135° stripes (CSS repeating-linear-gradient(135deg, A 0 w, B w P)): uv1 = (stripe A width, period, offset, -) along the
//              gradient; the offset scrolls the pattern to the right (one period per cycle)
//   2 sweep    the hover light (CSS linear-gradient(105deg, transparent 30%, A 50%, transparent 70%) moved by translateX):
//              uv1 = (x shift, box width, box height, -)
//   3 shadow   box-shadow as a blurred parallelogram: uv1 = (u0, y0, u1, y1) the unsheared rect, tangent.z = sigma (CSS blur ÷ 2)
// Colours are mixed in sRGB values like the browser. A Linear project blends in linear light, where a .55 white over paper or a .5
// black over the dark floor comes out far brighter / darker than the mockup; Out converts to the linear colour with the alpha that
// gives the same brightness over the given background as sRGB blending does (the UIScreenFX method, per pixel).
Shader "Blindside/UI/KitDeco"
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
                float4 uv3 : TEXCOORD3;
                float4 tangent : TANGENT;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 colA : TEXCOORD2;
                float4 colB : TEXCOORD3;
                float4 shape : TEXCOORD4;
                float3 under : TEXCOORD5;
                float4 world : TEXCOORD6;
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
                o.colA = v.uv2;
                o.colB = v.uv3;
                o.shape = v.tangent;
                o.under = v.normal;
                return o;
            }

            // erf approximation (Abramowitz–Stegun 7.1.27), for the gaussian box shadow
            float4 Erf(float4 x)
            {
                float4 s = sign(x), a = abs(x);
                float4 d = 1.0 + (0.278393 + (0.230389 + 0.078108 * (a * a)) * a) * a;
                d *= d;
                return s - s / (d * d);
            }

            // sRGB colour c with alpha a over background b (sRGB) → the output that blends to the same brightness in this colour space
            float4 Out(float3 c, float a, float3 b)
            {
                #ifdef UNITY_COLORSPACE_GAMMA
                return float4(c, a);
                #else
                const float3 lumW = float3(0.2126, 0.7152, 0.0722);
                float lc = dot(c, lumW), lb = dot(b, lumW);
                float linC = GammaToLinearSpaceExact(lc), linB = GammaToLinearSpaceExact(lb);
                float linT = GammaToLinearSpaceExact(lerp(lb, lc, a));
                float alpha = abs(linC - linB) > 0.0001 ? saturate((linT - linB) / (linC - linB)) : a;
                return float4(GammaToLinearSpace(c), alpha);
                #endif
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float mode = i.uv0.z;
                float2 p = i.uv0.xy;
                float3 rgb = i.colA.rgb;
                float a = i.colA.a;

                if (mode > 0.5 && mode < 1.5)
                {
                    // hazard: g runs along 135° (towards the bottom-right on screen; y is up here), the pattern starts with A
                    float period = max(i.uv1.y, 0.001), wA = i.uv1.x;
                    float g = (p.x - p.y) * 0.70710678 - i.uv1.z;
                    float f = g - floor(g / period) * period;
                    float aa = max(fwidth(g), 0.0001);
                    float inside = max(min(f, wA - f), min(f - period, period + wA - f));
                    float m = saturate(inside / aa + 0.5);
                    rgb = lerp(i.colB.rgb, i.colA.rgb, m);
                    a = lerp(i.colB.a, i.colA.a, m);
                }
                else if (mode > 1.5 && mode < 2.5)
                {
                    // sweep: the gradient line of a 105° gradient over the box, the box moved right by uv1.x
                    float w = i.uv1.y, h = i.uv1.z;
                    float2 d = p - float2(i.uv1.x + w * 0.5, h * 0.5);
                    const float2 dir = float2(0.96592583, -0.25881905);
                    float len = max(w * 0.96592583 + h * 0.25881905, 0.001);
                    float u = dot(d, dir) / len + 0.5;
                    a *= saturate(1.0 - abs(u - 0.5) / 0.2);
                }
                else if (mode > 2.5)
                {
                    // shadow: a gaussian-blurred rect in the unsheared shape space
                    float sigma = max(i.shape.z, 0.001);
                    float4 q = float4(i.shape.xy - i.uv1.xy, i.shape.xy - i.uv1.zw);
                    float4 integral = 0.5 + 0.5 * Erf(q * (0.70710678 / sigma));
                    a *= saturate((integral.z - integral.x) * (integral.w - integral.y));
                }

                float4 col = Out(rgb, a, i.under);
                col.a *= saturate(i.color.a / max(i.uv0.w, 0.001));

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(i.world.xy, _ClipRect);
                #endif
                return col;
            }
            ENDCG
        }
    }
}
