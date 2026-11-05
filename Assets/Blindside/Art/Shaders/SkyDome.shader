// The sky, drawn on the inside of a big sphere that follows the camera (SkyDome.cs). Per pixel from the view direction:
// a zenith → horizon gradient with a hazy band at the horizon, the ground colour below it, the sun (direction set by
// SkyDome from the scene's directional light) with a soft glow, and a drifting procedural cloud layer.
// Drawn first (Background queue) just in front of the far plane: everything else draws over it, and its depth keeps
// URP's own skybox (drawn after the opaques wherever the depth is still clear) from painting over it.
Shader "Blindside/SkyDome"
{
    Properties
    {
        _ZenithColor ("Zenith", Color) = (0.17, 0.38, 0.74, 1)
        _HorizonColor ("Horizon", Color) = (0.70, 0.81, 0.92, 1)
        _GroundColor ("Below the horizon", Color) = (0.27, 0.29, 0.25, 1)
        _SunColor ("Sun", Color) = (1, 0.93, 0.80, 1)
        _SunDir ("Direction to the sun", Vector) = (0.3, 0.6, 0.4, 0)
        _SunSize ("Sun size", Range(0.0002, 0.02)) = 0.0012
        _CloudCover ("Cloud cover", Range(0, 1)) = 0.5
        _CloudColor ("Clouds", Color) = (1, 1, 1, 1)
        _CloudScale ("Cloud scale", Float) = 1.4
        _CloudSpeed ("Cloud drift", Float) = 0.006
    }
    SubShader
    {
        Tags { "RenderType" = "Background" "Queue" = "Background" "RenderPipeline" = "UniversalPipeline" "PreviewType" = "Skybox" }
        Cull Front
        ZWrite On

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor, _HorizonColor, _GroundColor, _SunColor, _CloudColor;
                float4 _SunDir;
                float _SunSize, _CloudCover, _CloudScale, _CloudSpeed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                // on the far plane: behind everything, whatever the dome's radius
                #if UNITY_REVERSED_Z
                    o.positionCS.z = 1.0e-5 * o.positionCS.w;
                #else
                    o.positionCS.z = 0.99999 * o.positionCS.w;
                #endif
                return o;
            }

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float Noise(float3 x)
            {
                float3 i = floor(x), f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(Hash(i), Hash(i + float3(1, 0, 0)), f.x),
                                 lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x),
                                 lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            float Fbm(float3 p)
            {
                float v = 0.0, a = 0.5;
                for (int k = 0; k < 5; k++) { v += a * Noise(p); p *= 2.03; a *= 0.5; }
                return v;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.positionWS - _WorldSpaceCameraPos);
                float h = d.y;

                // sky gradient, a hazy band along the horizon, the ground below
                half3 sky = lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(saturate(h), 0.45));
                sky = lerp(sky, _HorizonColor.rgb * 1.04, exp(-abs(h) * 16.0) * 0.55);
                half3 col = h >= 0.0 ? sky : lerp(_HorizonColor.rgb * 0.82, _GroundColor.rgb, saturate(-h * 7.0));

                // the sun: a disc, a tight glow and a wide one
                float3 s = normalize(_SunDir.xyz);
                float sd = dot(d, s);
                float above = step(-0.02, h);
                col += _SunColor.rgb * above * (smoothstep(1.0 - _SunSize, 1.0 - _SunSize * 0.5, sd) * 2.5
                                               + pow(saturate(sd), 90.0) * 0.4 + pow(saturate(sd), 6.0) * 0.12);

                // clouds: a layer seen through the sky, thinning into the haze near the horizon
                if (h > 0.0)
                {
                    float2 uv = d.xz / (h + 0.12) * _CloudScale;
                    float t = _Time.y * _CloudSpeed;
                    float n = Fbm(float3(uv + float2(t, t * 0.6), t * 0.4));
                    float c = smoothstep(1.0 - _CloudCover, 1.0 - _CloudCover + 0.22, n) * smoothstep(0.0, 0.14, h);
                    float lit = 0.8 + 0.2 * saturate(sd);
                    half3 cloud = _CloudColor.rgb * lerp(0.72, 1.0, saturate((n - 0.4) * 2.0)) * lit;
                    col = lerp(col, cloud, c * 0.92);
                }
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
