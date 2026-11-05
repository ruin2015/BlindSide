// Plain additive colour for the intro hologram's lines, frames, beams and marker rings (vertex colour × _Color × _Alpha).
Shader "Blindside/HoloLine"
{
    Properties
    {
        _Color ("Colour", Color) = (0.7, 0.92, 1, 1)
        _Alpha ("Alpha", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" }
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
                half4 _Color;
                float _Alpha;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.color = i.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                return half4(i.color.rgb * i.color.a * _Color.rgb * _Alpha, 1.0);
            }
            ENDHLSL
        }
    }
}
