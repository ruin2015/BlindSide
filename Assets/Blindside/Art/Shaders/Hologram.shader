// The intro's tactical hologram (IntroHologram): additive, unlit. A body tinted _Color at _Fill, the mesh's face edges drawn as
// thin lines of constant screen width (from the UVs: every face of the map's wall cubes is 0..1, so each box reads as a
// wireframe), a rim toward grazing angles, scan bands drifting up in world space and a light flicker. _Reveal builds the
// object up from the ground (0 = nothing, 1 = all of it, the build-up edge glowing). _Alpha fades the whole thing.
Shader "Blindside/Hologram"
{
    Properties
    {
        _Color ("Colour", Color) = (0.25, 0.75, 1, 1)
        _EdgeColor ("Edge colour", Color) = (0.75, 0.95, 1, 1)
        _Fill ("Body strength", Range(0, 1)) = 0.12
        _EdgeWidth ("Edge width (px)", Range(0.3, 6)) = 1.4
        _Rim ("Rim strength", Range(0, 2)) = 0.6
        _ScanDensity ("Scan bands per metre", Float) = 1.6
        _ScanSpeed ("Scan speed", Float) = 0.8
        _Reveal ("Reveal height (m, from y0)", Float) = 1000
        _RevealBase ("Reveal base y", Float) = 0
        _Alpha ("Alpha", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
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
                half4 _Color, _EdgeColor;
                float _Fill, _EdgeWidth, _Rim, _ScanDensity, _ScanSpeed, _Reveal, _RevealBase, _Alpha;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.uv = i.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // build-up from the ground: hidden above the reveal line, a bright band at it
                float above = i.positionWS.y - (_RevealBase + _Reveal);
                clip(-above);
                float front = saturate(1.0 - abs(above) * 4.0);

                // face edges at a constant screen width
                float2 d = min(i.uv, 1.0 - i.uv) / max(fwidth(i.uv), 1e-5);
                float edge = 1.0 - saturate(min(d.x, d.y) / _EdgeWidth);

                float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                float rim = pow(1.0 - saturate(abs(dot(normalize(i.normalWS), v))), 3.0) * _Rim;

                float scan = 0.65 + 0.35 * sin((i.positionWS.y * _ScanDensity - _Time.y * _ScanSpeed) * 6.2831853);
                float flicker = 0.92 + 0.08 * sin(_Time.y * 37.0 + i.positionWS.x * 0.7);

                half3 col = _Color.rgb * (_Fill * scan + rim) + _EdgeColor.rgb * edge + _EdgeColor.rgb * front * 1.5;
                return half4(col * flicker * _Alpha, 1.0);
            }
            ENDHLSL
        }
    }
}
