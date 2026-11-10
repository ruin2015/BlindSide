// Raindrops on the camera's lens (MissionAtmosphere): a quad just in front of the camera, drawn after everything opaque. Inside a
// drop the scene (the camera's opaque texture) is seen through it — shifted the other way, like a little lens, a touch brighter, with
// a darker rim; outside the drops the quad is clear. Two layers of drops on a grid (hash per cell): each fades in, sits, fades out
// on its own clock, and some of them run down the glass as they go. _Amount 0..1 fades the whole effect.
Shader "Blindside/LensRain"
{
    Properties
    {
        _Amount ("Amount", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+400" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "LensRain"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            ZTest Always
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float _Amount;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 screen : TEXCOORD0; float2 uv : TEXCOORD1; };

            Varyings vert(Attributes a)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(a.positionOS.xyz);
                o.screen = ComputeScreenPos(o.positionCS);
                o.uv = a.uv;
                return o;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // one layer: (direction from the drop's centre / radius, mask)
            float3 Drops(float2 uv, float t, float cells, float speed)
            {
                const float2 aspect = float2(1.7778, 1.0);
                float2 g = uv * aspect * cells;
                float2 id = floor(g);
                float2 st = frac(g) - .5;
                float n = Hash(id);
                float life = frac(t * speed + n);
                float2 c = float2(Hash(id + 3.1) - .5, Hash(id + 7.7) - .5) * .55;
                // about one in four runs down the glass in the second half of its life
                c.y += .45 - step(.75, n) * smoothstep(.35, 1., life) * .95 - .45;
                float r = .05 + .09 * Hash(id + 1.3);
                float2 d = st - c;
                d.y *= 1. + .35 * step(.75, n) * smoothstep(.35, 1., life);   // a running drop stretches a little
                float dist = length(d);
                float present = step(.86, Hash(id + 9.1));
                float fade = smoothstep(0., .12, life) * smoothstep(1., .8, life);
                float m = smoothstep(r, r * .72, dist) * fade * present;
                return float3(d / max(r, 1e-3), m);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 suv = i.screen.xy / i.screen.w;
                float t = _Time.y;
                float3 a = Drops(i.uv, t, 6., .04);
                float3 b = Drops(i.uv * 1.6 + .37, t, 10., .06);
                // only round the edges of the glass: nothing in the middle, more toward the rim
                float2 e = abs(i.uv - .5) * 2.;
                float edge = smoothstep(.62, .95, max(e.x, e.y));
                float m = saturate(max(a.z, b.z)) * _Amount * edge * .8;
                if (m <= .001) return half4(0, 0, 0, 0);
                float2 dir = a.z > b.z ? a.xy : b.xy;
                // the lens: the scene seen through the drop, from the other side of it (and a little zoomed)
                float2 off = -dir * .022;
                half3 col = SampleSceneColor(saturate(suv + off)).rgb;
                float rim = smoothstep(.55, 1., length(dir));
                col = col * (1.03 - .1 * rim);
                // a highlight on the upper side of the drop
                col += .05 * smoothstep(.3, .08, length(dir - float2(-.3, .4)));
                return half4(col, m * (.55 + .45 * rim));   // clear in the middle of a drop, its edge reads a little more
            }
            ENDHLSL
        }
    }
}
