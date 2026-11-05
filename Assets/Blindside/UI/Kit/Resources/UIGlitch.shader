// Blindside/UI/Glitch — the card-glitch look (ui_spec.md "카드 글리치 등장") in one UI pass. Like UISegmented, every parameter
// travels in the mesh vertices, so all glitch images share ONE material and batch (the texture is the canvas renderer's):
//   uv0     (sprite u, sprite v, rect width, rect height) — width / height in canvas units, to turn pixel offsets into uv
//   uv1     (reveal 0..1, brightness, hue shift degrees, rgb split px: red −, blue +)
//   uv2     (slice offset 1, 2, 3 in px — 0 = that strip is off, sweep 0..1 — <0 = off)
//   uv3     the sprite's uv rectangle (xMin, yMin, xMax, yMax): samples never leave it, so an atlas neighbour can't show
//   normal  (x 0..1, y 0..1 across the rect, luminance of the background under a translucent picture — sRGB, < 0 = no match)
//           (the half-texel inset in v comes from _MainTex_TexelSize)
//   tangent (red flash 0..1, saturation, body offset px, half-texel inset in u)
//   color   tint / CanvasGroup alpha as for any UI graphic
// Strips (percent of the height from the top): 12–30, 44–60, 70–88. A strip that is on draws the image shifted by its offset
// over the body; where the shifted image has nothing (it left the sprite rectangle) the body shows. Stencil masks and RectMask2D clip.
Shader "Blindside/UI/Glitch"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
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
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 uv2 : TEXCOORD2;
                float4 uv3 : TEXCOORD3;
                float3 nrm : TEXCOORD4;
                float4 tan : TEXCOORD5;
                float4 world : TEXCOORD6;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _ClipRect;

            v2f vert(appdata v)
            {
                v2f o;
                o.world = v.vertex;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv0 = v.uv0; o.uv1 = v.uv1; o.uv2 = v.uv2; o.uv3 = v.uv3;
                o.nrm = v.normal; o.tan = v.tangent;
                return o;
            }

            // the SVG feColorMatrix hue rotation (what CSS hue-rotate() does)
            float3 HueRotate(float3 c, float degrees)
            {
                float a = radians(degrees), cs = cos(a), sn = sin(a);
                return float3(
                    dot(c, float3(0.213 + cs * 0.787 - sn * 0.213, 0.715 - cs * 0.715 - sn * 0.715, 0.072 - cs * 0.072 + sn * 0.928)),
                    dot(c, float3(0.213 - cs * 0.213 + sn * 0.143, 0.715 + cs * 0.285 + sn * 0.140, 0.072 - cs * 0.072 - sn * 0.283)),
                    dot(c, float3(0.213 - cs * 0.213 - sn * 0.787, 0.715 - cs * 0.715 + sn * 0.715, 0.072 + cs * 0.928 + sn * 0.072)));
            }

            // sample inside the sprite's uv rectangle; `inside` is 0 where the requested uv lies outside it
            float4 SampleClamped(float2 uv, float4 rect, float2 inset, out float inside)
            {
                inside = (uv.x >= rect.x && uv.x <= rect.z && uv.y >= rect.y && uv.y <= rect.w) ? 1.0 : 0.0;
                uv = clamp(uv, rect.xy + inset, rect.zw - inset);
                return tex2D(_MainTex, uv);
            }

            // one strip: [lo, hi] of the height from the top; offset 0 = off
            void Strip(float lo, float hi, float offset, float fromTop, float2 uv, float2 perPx, float4 rect, float2 inset, inout float3 rgb, inout float alpha)
            {
                float on = step(lo, fromTop) * step(fromTop, hi) * step(0.001, abs(offset));
                float inside;
                float4 s = SampleClamped(uv - float2(offset, 0.0) * perPx, rect, inset, inside);
                float a = s.a * inside * on;
                rgb = lerp(rgb, s.rgb, a);
                alpha = max(alpha, a);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 size = max(i.uv0.zw, 0.001);
                float4 rect = i.uv3;
                float2 inset = float2(i.tan.w, i.tan.w > 0.0 ? 0.5 * _MainTex_TexelSize.y : 0.0);
                float2 perPx = (rect.zw - rect.xy) / size;       // uv per canvas unit
                float2 p = i.nrm.xy;
                float fromTop = 1.0 - p.y;

                float reveal = i.uv1.x, bright = i.uv1.y, hue = i.uv1.z, split = i.uv1.w;
                float flash = i.tan.x, sat = i.tan.y, body = i.tan.z;

                // wipe from the right: the mockup's clip-path edge runs straight down, from the right side to beyond the left (−20%)
                float edge = lerp(1.0, -0.2, reveal);
                float visible = step(edge, p.x);

                // body: shifted by `body`, red copy left / blue copy right by `split`
                float2 uvBody = i.uv0.xy - float2(body, 0.0) * perPx;
                float inR, inG, inB;
                float4 sR = SampleClamped(uvBody + float2(split, 0.0) * perPx, rect, inset, inR);
                float4 sG = SampleClamped(uvBody, rect, inset, inG);
                float4 sB = SampleClamped(uvBody - float2(split, 0.0) * perPx, rect, inset, inB);
                float3 rgb = float3(sR.r, sG.g, sB.b);
                float alpha = max(sR.a * inR, max(sG.a * inG, sB.a * inB));

                // three horizontal strips, each the image shifted sideways over the body
                Strip(0.12, 0.30, i.uv2.x, fromTop, i.uv0.xy, perPx, rect, inset, rgb, alpha);
                Strip(0.44, 0.60, i.uv2.y, fromTop, i.uv0.xy, perPx, rect, inset, rgb, alpha);
                Strip(0.70, 0.88, i.uv2.z, fromTop, i.uv0.xy, perPx, rect, inset, rgb, alpha);

                // colour, in sRGB values like CSS filters (a Linear project samples linear colour): brightness → hue → saturation,
                // clamped after each step as the browser does
                #ifndef UNITY_COLORSPACE_GAMMA
                rgb = LinearToGammaSpace(rgb);
                #endif
                rgb = saturate(rgb * bright);
                rgb = saturate(HueRotate(rgb, hue));
                float lum = dot(rgb, float3(0.2126, 0.7152, 0.0722));
                rgb = saturate(lerp(float3(lum, lum, lum), rgb, sat));

                // light sweep: a level band 21% of the height (80 of 374 in the mockup), its top running from −27% to 110% of the
                // height; transparent → white .75 → transparent, mix-blend-mode overlay
                if (i.uv2.w >= 0.0)
                {
                    float centre = lerp(-0.267, 1.1, i.uv2.w) + 0.107;
                    float a = saturate(1.0 - abs(fromTop - centre) / 0.107) * 0.75 * step(0.001, alpha);
                    rgb = lerp(rgb, min(rgb * 2.0, 1.0), a);
                }
                #ifndef UNITY_COLORSPACE_GAMMA
                rgb = GammaToLinearSpace(rgb);
                #endif

                // red flash (hit)
                #ifdef UNITY_COLORSPACE_GAMMA
                float3 red = float3(0.90, 0.21, 0.30);
                #else
                float3 red = GammaToLinearSpace(float3(0.90, 0.21, 0.30));
                #endif
                rgb = lerp(rgb, red, flash * 0.7);

                fixed4 col = fixed4(rgb, alpha * visible) * i.color;

                // translucent picture (opacity) over a known background: the alpha that gives the brightness sRGB blending gives
                // over that background's luminance (normal.z, sRGB; < 0 = off) — KitDeco's Out(), per pixel
                #ifndef UNITY_COLORSPACE_GAMMA
                if (i.nrm.z >= 0.0 && col.a > 0.0 && col.a < 1.0)
                {
                    const float3 lumW = float3(0.2126, 0.7152, 0.0722);
                    float lb = i.nrm.z, lc = dot(LinearToGammaSpace(col.rgb), lumW);
                    float linB = GammaToLinearSpaceExact(lb), linC = GammaToLinearSpaceExact(lc);
                    float linT = GammaToLinearSpaceExact(lerp(lb, lc, col.a));
                    col.a = abs(linC - linB) > 0.0001 ? saturate((linT - linB) / (linC - linB)) : col.a;
                }
                #endif

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(i.world.xy, _ClipRect);
                #endif
                return col;
            }
            ENDCG
        }
    }
}
