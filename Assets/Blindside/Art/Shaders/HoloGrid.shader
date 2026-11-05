// The intro hologram's ground (IntroHologram): a world-space grid, fine lines every _Minor metres and brighter ones every
// _Major, fading out with distance from _Center; a ring pulse travels out from _Center every _PulsePeriod seconds; _Reveal (m)
// draws the grid outward from _Center (with a bright front). Additive, unlit.
Shader "Blindside/HoloGrid"
{
    Properties
    {
        _Color ("Line colour", Color) = (0.2, 0.6, 1, 1)
        _MajorColor ("Major line colour", Color) = (0.55, 0.85, 1, 1)
        _Minor ("Minor spacing (m)", Float) = 1
        _Major ("Major spacing (m)", Float) = 10
        _Center ("Centre (xz)", Vector) = (0, 0, 0, 0)
        _Fade ("Fade radius (m)", Float) = 180
        _Reveal ("Reveal radius (m)", Float) = 10000
        _PulsePeriod ("Pulse period (s)", Float) = 2.4
        _PulseColor ("Pulse colour", Color) = (0.4, 0.9, 1, 1)
        _Alpha ("Alpha", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color, _MajorColor, _PulseColor;
                float4 _Center;
                float _Minor, _Major, _Fade, _Reveal, _PulsePeriod, _Alpha;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            // 1 on a line every `spacing` metres, `px` pixels wide, anti-aliased
            float GridLine(float2 p, float spacing, float px)
            {
                float2 g = p / spacing;
                float2 w = fwidth(g);
                float2 a = abs(frac(g - 0.5) - 0.5) / max(w, 1e-5);
                return 1.0 - saturate(min(a.x, a.y) / px);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 p = i.positionWS.xz;
                float r = distance(p, _Center.xy);
                clip(_Reveal - r);
                float fade = saturate(1.0 - r / _Fade);
                fade *= fade;
                float minor = GridLine(p, _Minor, 1.0) * 0.35;
                float major = GridLine(p, _Major, 1.4);
                float front = saturate(1.0 - abs(_Reveal - r) * 0.25);
                float ph = frac(_Time.y / _PulsePeriod);
                float pulse = saturate(1.0 - abs(r - ph * _Fade * 0.6) * 0.5) * (1.0 - ph);
                half3 col = _Color.rgb * minor + _MajorColor.rgb * major + _PulseColor.rgb * pulse * 0.35 + _MajorColor.rgb * front;
                col += _Color.rgb * 0.035;   // a faint glow under it all
                return half4(col * fade * _Alpha, 1.0);
            }
            ENDHLSL
        }
    }
}
