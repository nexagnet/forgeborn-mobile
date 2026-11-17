// Additive unlit particle shader for grinding / hammer sparks (vertex colour drives colour and fade). Cheap on purpose.
Shader "ForgeBorn/Spark"
{
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.color = i.color;
                o.uv = i.uv;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 d = i.uv - 0.5;
                half falloff = saturate(1.0 - dot(d, d) * 4.0);
                return half4(i.color.rgb * 2.0h, i.color.a * falloff);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
