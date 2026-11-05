// Blindside/UI/ScreenFX — the full-screen overlay (ui_spec.md "화면 전체 효과") in one pass, one quad, one draw call. Like UIGlitch and
// UISegmented, every parameter travels in the mesh vertices, so the material is shared and never changed:
//   uv0     (x 0..1, y 0..1 across the rect, rect width, rect height) — canvas units
//   uv1     (scanline alpha, scanline period px, scanline thickness px, noise alpha)
//   uv2     (light band centre from the top, band half height — canvas units, band alpha, burst alpha)
//   uv3     (noise offset x, noise offset y — texels, burst seed, noise cell px)
//   tangent (band / burst blue rgb as sRGB values, unused)
//   color   white; its alpha scales the whole overlay
// _MainTex is the small repeating noise texture (point filtered, repeat); only its offset moves (12fps), nothing is re-uploaded.
// Scanlines and noise are laid on whole screen pixels (SV_POSITION), so they stay crisp at any canvas scale: the caller rounds
// "3 canvas units" to whole pixels with the canvas scale factor.
// Layers, back to front: noise (grey), scanlines (white), light band (blue, transparent → alpha → transparent over its height), glitch
// burst (five horizontal white / blue strips, shuffled by the seed). Composited premultiplied in sRGB values, output straight alpha
// (in a Linear project converted at the end, see frag).
Shader "Blindside/UI/ScreenFX"
{
    Properties
    {
        [PerRendererData] _MainTex ("Noise", 2D) = "gray" {}
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
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 uv2 : TEXCOORD2;
                float4 uv3 : TEXCOORD3;
                float3 blue : TEXCOORD4;
                float4 world : TEXCOORD5;
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
                o.blue = v.tangent.xyz;
                return o;
            }

            float Hash(float x) { return frac(sin(x * 12.9898 + 78.233) * 43758.5453); }

            // premultiplied "over": a layer of colour c and alpha a on top of acc
            void Over(inout float4 acc, float3 c, float a)
            {
                acc.rgb = c * a + acc.rgb * (1.0 - a);
                acc.a = a + acc.a * (1.0 - a);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 px = floor(i.pos.xy);                 // whole screen pixels
                float2 size = i.uv0.zw;
                float x = i.uv0.x * size.x, fromTop = (1.0 - i.uv0.y) * size.y;
                float4 acc = 0;

                // noise: one texel per `cell` pixels, the texture offset jumps at 12fps
                float noiseA = i.uv1.w;
                if (noiseA > 0.0)
                {
                    float cell = max(i.uv3.w, 1.0);
                    float2 uv = (floor(px / cell) + i.uv3.xy + 0.5) * _MainTex_TexelSize.xy;
                    float n = tex2D(_MainTex, uv).r;
                    Over(acc, float3(n, n, n), noiseA);
                }

                // scanlines: a `thickness`-pixel white line every `period` pixels
                float scanA = i.uv1.x;
                if (scanA > 0.0)
                {
                    float period = max(i.uv1.y, 2.0);
                    float on = step(fmod(px.y, period), i.uv1.z - 0.5);
                    Over(acc, float3(1, 1, 1), scanA * on);
                }

                // light band: transparent → blue alpha → transparent across its height
                float bandA = i.uv2.z;
                if (bandA > 0.0)
                {
                    float d = abs(fromTop - i.uv2.x) / max(i.uv2.y, 0.001);
                    Over(acc, i.blue, bandA * saturate(1.0 - d));
                }

                // glitch burst: five strips at seeded heights, each covering a seeded run of the width, white and blue in turn
                float burstA = i.uv2.w;
                if (burstA > 0.0)
                {
                    float seed = i.uv3.z;
                    [unroll] for (int k = 0; k < 5; k++)
                    {
                        float s = seed * 7.31 + k * 1.93;
                        float top = Hash(s) * size.y, h = lerp(3.0, 26.0, Hash(s + 0.11));
                        float x0 = Hash(s + 0.23) * size.x * 0.6, x1 = x0 + lerp(0.25, 0.9, Hash(s + 0.37)) * size.x;
                        float inside = step(top, fromTop) * step(fromTop, top + h) * step(x0, x) * step(x, x1);
                        float3 c = (k == 1 || k == 3) ? i.blue : float3(1, 1, 1);
                        Over(acc, c, burstA * lerp(0.6, 1.0, Hash(s + 0.51)) * inside);
                    }
                }

                float3 c = acc.a > 0.0001 ? acc.rgb / acc.a : float3(0, 0, 0);
                float alpha = acc.a;
                #ifndef UNITY_COLORSPACE_GAMMA
                // the layers are mixed in sRGB values like the browser; a Linear project blends in linear light, where a .03 white
                // would brighten dark UI ~6× more than in the mockup. Output the linear colour with the alpha that gives the same
                // brightness change over the UI's dark ink (#14161A, sRGB ≈ .085) as sRGB blending does.
                float lumC = dot(c, float3(0.2126, 0.7152, 0.0722));
                const float lumB = 0.085;
                float linB = GammaToLinearSpaceExact(lumB), linC = GammaToLinearSpaceExact(lumC);
                float linT = GammaToLinearSpaceExact(lerp(lumB, lumC, alpha));
                alpha = abs(linC - linB) > 0.0001 ? saturate((linT - linB) / (linC - linB)) : alpha;
                c = GammaToLinearSpace(c);
                #endif
                fixed4 col = fixed4(c, alpha * i.color.a);

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(i.world.xy, _ClipRect);
                #endif
                return col;
            }
            ENDCG
        }
    }
}
