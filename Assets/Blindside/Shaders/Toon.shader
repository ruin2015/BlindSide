// BLINDSIDE's toon look (every lit material: characters, guns, the map, the town — ToonConverter swaps URP/Lit for it; code that builds
// a lit material at runtime takes it through ToonShader.Lit). The look of a painted anime RPG:
//   shading  two bands from the main light — a lit side and a shadow side split by a soft step (_ShadowThreshold ± _ShadowSoftness),
//            the shadow side the albedo tinted (_ShadowColor, a cool violet) rather than darkened toward black; inside the lit side a
//            gentle second tone toward the terminator (_MidTone). Received shadows join the shadow side. The sky (SH) fills in on top.
//   highlight a small hard-edged specular spot (size from _Smoothness, strength _SpecStrength) on the lit side, in the surface's colour;
//            none on matte surfaces (smoothness under ~.35: cloth, skin, hair), fading in up to .75
//   rim      a thin light edge on silhouettes (_RimColor × the light, toward the surface's colour, at .6 of _RimStrength), brighter on the lit side
//   other lights each adds its own banded light (the helicopter's searchlight, the operators' weapon lights)
//   outline  an inverted hull (pass "Outline", LightMode SRPDefaultUnlit): the back faces pushed out along the normal in screen space
//            — the same width in pixels however far the camera is — coloured from the surface's own colour, darkened (_OutlineColor)
//   fade     the same surface switches as URP/Lit (_Surface, _SrcBlend, _DstBlend, _ZWrite, _SURFACE_TYPE_TRANSPARENT), so the code that
//            fades a wall or a unit works on it unchanged; a faded surface draws no outline.
// The shadow / depth / depth-normals passes are URP/Lit's own (UsePass).
Shader "Blindside/Toon"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1
        _EmissionMap ("Emission Map", 2D) = "white" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        _Smoothness ("Smoothness (highlight size)", Range(0, 1)) = 0.35
        _Metallic ("Metallic", Range(0, 1)) = 0

        [Header(Toon)]
        _ShadowColor ("Shadow Tint", Color) = (0.72, 0.66, 0.84, 1)
        _ShadowThreshold ("Shadow Threshold", Range(-1, 1)) = 0.02
        _ShadowSoftness ("Shadow Softness", Range(0.001, 0.5)) = 0.05
        _MidTone ("Mid Tone", Range(0, 1)) = 0.35
        _AmbientStrength ("Ambient", Range(0, 2)) = 0.7
        _SpecStrength ("Highlight Strength", Range(0, 1)) = 0.22
        _TameShine ("Tame Baked Shine (light grey patches in the texture)", Range(0, 1)) = 0
        _RimColor ("Rim Color", Color) = (1, 0.97, 0.92, 1)
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.3
        _RimWidth ("Rim Width", Range(0, 1)) = 0.28

        [Header(Outline)]
        _OutlineColor ("Outline Tint", Color) = (0.32, 0.26, 0.3, 1)
        _OutlineWidth ("Outline Width (px)", Range(0, 6)) = 1.2
        _OutlineFadeNear ("Outline Fade Start (m)", Float) = 7
        _OutlineFadeFar ("Outline Fade End (m)", Float) = 26
        _OutlineFarWidth ("Outline Width Far (×)", Range(0, 1)) = 0.3

        // URP/Lit's surface switches (runtime fades set these)
        [HideInInspector] _Surface ("__surface", Float) = 0
        [HideInInspector] _Blend ("__blend", Float) = 0
        [HideInInspector] _AlphaClip ("__clip", Float) = 0
        [HideInInspector] _SrcBlend ("__src", Float) = 1
        [HideInInspector] _DstBlend ("__dst", Float) = 0
        [HideInInspector] _SrcBlendAlpha ("__srcA", Float) = 1
        [HideInInspector] _DstBlendAlpha ("__dstA", Float) = 0
        [HideInInspector] _ZWrite ("__zw", Float) = 1
        [HideInInspector] _Cull ("__cull", Float) = 2
        [HideInInspector] _QueueOffset ("Queue offset", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "UniversalMaterialType" = "Lit" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Cutoff;
            half _BumpScale;
            half4 _EmissionColor;
            half _Smoothness;
            half _Metallic;
            half4 _ShadowColor;
            half _ShadowThreshold;
            half _ShadowSoftness;
            half _MidTone;
            half _AmbientStrength;
            half _SpecStrength;
            half _TameShine;
            half4 _RimColor;
            half _RimStrength;
            half _RimWidth;
            half4 _OutlineColor;
            half _OutlineWidth;
            half _OutlineFadeNear;
            half _OutlineFadeFar;
            half _OutlineFarWidth;
            half _Surface;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend], [_SrcBlendAlpha] [_DstBlendAlpha]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT
            #pragma shader_feature_local_fragment _EMISSION
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 tangentWS : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(v.normalOS, v.tangentOS);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = n.normalWS;
                o.tangentWS = float4(n.tangentWS, v.tangentOS.w * GetOddNegativeScale());
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            // one light's banded contribution: x = the lit share (0 shadow side .. 1 lit side), with the light's attenuation
            half Band(half ndl, half atten)
            {
                return smoothstep(_ShadowThreshold - _ShadowSoftness, _ShadowThreshold + _ShadowSoftness, ndl) * atten;
            }

            // matte surfaces (cloth, skin, hair: smoothness under ~.35) take no spot at all — a white spot on dark cloth read as a gloss;
            // the spot fades in with the smoothness and stays small (power 2^5 at the least)
            half Highlight(float3 N, float3 L, float3 V)
            {
                float3 H = normalize(L + V);
                half power = exp2(_Smoothness * 7.0 + 5.0);
                half s = pow(saturate(dot(N, H)), power);
                return smoothstep(.45, .55, s) * _SpecStrength * smoothstep(.35, .75, _Smoothness);
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                #if defined(_ALPHATEST_ON)
                    clip(base.a - _Cutoff);
                #endif

                float3 N = normalize(i.normalWS);
                #if defined(_NORMALMAP)
                    half3 nTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv), _BumpScale);
                    float3 bit = i.tangentWS.w * cross(N, i.tangentWS.xyz);
                    N = normalize(mul(nTS, float3x3(i.tangentWS.xyz, bit, N)));
                #endif
                float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half3 albedo = base.rgb;
                // baked shine: the generated textures paint light grey glints over dark cloth; grey mid-to-light tones (not white, not
                // coloured) are pulled down toward the cloth by _TameShine
                {
                    half lum = dot(albedo, half3(.299, .587, .114));
                    half sat = max(albedo.r, max(albedo.g, albedo.b)) - min(albedo.r, min(albedo.g, albedo.b));
                    // (linear values: the glint facets are sRGB ~.3–.6 → .07–.32, the cloth under .25 → .05, white parts .85+ → .7+)
                    half grey = smoothstep(.045, .1, lum) * (1.0 - smoothstep(.36, .52, lum)) * (1.0 - saturate(sat * 4.0));
                    albedo *= 1.0 - .55 * _TameShine * grey;
                }

                // ---- the main light: two bands, a tinted shadow side ----
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light ml = GetMainLight(shadowCoord);
                half ndl = dot(N, ml.direction);
                half lit = Band(ndl, ml.shadowAttenuation);
                half3 shadowSide = albedo * _ShadowColor.rgb;
                half mid = lerp(1.0 - _MidTone * .22, 1.0, smoothstep(.15, .65, ndl));
                half3 col = lerp(shadowSide, albedo * mid, lit) * ml.color;
                half3 specTint = lerp(albedo, 1.0, .35);   // the spot takes the surface's colour (not a white sticker)
                col += Highlight(N, ml.direction, V) * lit * ml.color * specTint;

                // ---- the sky ----
                col += albedo * SampleSH(N) * _AmbientStrength * lerp(_ShadowColor.rgb, 1.0, .5);

                // ---- the other lights ----
                #if defined(_ADDITIONAL_LIGHTS)
                    InputData inputData = (InputData)0;
                    inputData.positionWS = i.positionWS;
                    inputData.normalWS = N;
                    inputData.viewDirectionWS = V;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                    uint count = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(count)
                        Light l = GetAdditionalLight(lightIndex, i.positionWS, half4(1, 1, 1, 1));
                        half b = Band(dot(N, l.direction), l.shadowAttenuation) * l.distanceAttenuation;
                        col += (albedo * b + Highlight(N, l.direction, V) * b * .5 * specTint) * l.color;
                    LIGHT_LOOP_END
                #endif

                // ---- the rim ----
                half fres = 1.0 - saturate(dot(N, V));
                // (softer than the material's value and tinted toward the surface: a thin edge, not a sheen)
                half rim = smoothstep(1.0 - _RimWidth, 1.0 - _RimWidth + .1, fres) * _RimStrength * .6 * (.35 + .65 * lit);
                col += rim * _RimColor.rgb * lerp(albedo, 1.0, .5) * ml.color;

                #if defined(_EMISSION)
                    col += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, i.uv).rgb * _EmissionColor.rgb;
                #endif

                col = MixFog(col, i.fogFactor);
                half alpha = 1.0;
                #if defined(_SURFACE_TYPE_TRANSPARENT)
                    alpha = base.a;
                #endif
                return half4(col, alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite [_ZWrite]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half fogFactor : TEXCOORD1;
                half far : TEXCOORD2;      // 0 near .. 1 far (the line thins and lightens)
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                float4 pos = TransformObjectToHClip(v.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(v.normalOS);
                float2 nCS = mul((float3x3)UNITY_MATRIX_VP, nWS).xy;
                float len = max(length(nCS), 1e-4);
                nCS /= len;
                // the push: _OutlineWidth pixels (×2 / screen size in clip units, times w to undo the perspective divide)
                float2 px = 2.0 / _ScreenParams.xy;
                // far from the camera the figure is small: a line of the same pixel width would cover it, so it thins (to
                // _OutlineFarWidth of itself) between _OutlineFadeNear and _OutlineFadeFar metres
                float dist = distance(TransformObjectToWorld(v.positionOS.xyz), GetCameraPositionWS());
                half far = smoothstep(_OutlineFadeNear, _OutlineFadeFar, dist);
                o.far = far;
                pos.xy += nCS * px * _OutlineWidth * lerp(1.0, _OutlineFarWidth, far) * pos.w;
                o.positionCS = pos;
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fogFactor = ComputeFogFactor(pos.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                clip(.5 - _Surface);             // a faded (transparent) surface draws no outline
                clip(_OutlineWidth - .01);
                half4 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                #if defined(_ALPHATEST_ON)
                    clip(base.a - _Cutoff);
                #endif
                // (and lightens toward the surface's own colour, so a far figure doesn't read as a black blot)
                half3 col = base.rgb * lerp(_OutlineColor.rgb, .7, i.far);
                col = MixFog(col, i.fogFactor);
                return half4(col, 1);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }

    FallBack "Universal Render Pipeline/Lit"
}
