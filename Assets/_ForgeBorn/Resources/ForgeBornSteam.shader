// Soft alpha-blended steam puff for quenching (vertex colour carries tint and fade). Not additive: steam must occlude, not glow.
Shader "ForgeBorn/Steam"
{
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+1" }
        Blend SrcAlpha OneMinusSrcAlpha
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
                float r2 = dot(d, d) * 4.0;
                // Soft blobby edge with a little cheap noise so puffs do not read as perfect discs.
                float n = frac(sin(dot(floor(i.uv * 6.0), float2(12.9898, 78.233))) * 43758.5453);
                half falloff = saturate(1.0 - r2 * (0.9 + 0.25 * n));
                falloff *= falloff;
                return half4(i.color.rgb, i.color.a * falloff);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
