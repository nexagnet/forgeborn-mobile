// Workshop backdrop: dark vertical gradient, warm furnace glow from the lower left, soft vignette. Unlit, one pass.
Shader "ForgeBorn/Backdrop"
{
    Properties
    {
        _Top ("Top colour", Color) = (0.055, 0.058, 0.075, 1)
        _Bottom ("Bottom colour", Color) = (0.13, 0.085, 0.06, 1)
        _Glow ("Furnace glow", Color) = (1.0, 0.42, 0.1, 1)
        _GlowAmount ("Glow amount", Range(0, 2)) = 0.55
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Top;
                half4 _Bottom;
                half4 _Glow;
                half _GlowAmount;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half3 col = lerp(_Bottom.rgb, _Top.rgb, smoothstep(0.0, 1.0, i.uv.y));
                float2 d = (i.uv - float2(0.05, 0.12)) * float2(1.0, 0.62);
                half glow = exp(-dot(d, d) * 5.0) * _GlowAmount;
                col += _Glow.rgb * glow;
                // Faint horizontal planks so the wall is not a flat gradient.
                half plank = 0.92h + 0.08h * frac(sin(floor(i.uv.y * 14.0) * 91.7) * 437.5);
                col *= plank;
                float2 c = i.uv - 0.5;
                col *= saturate(1.15h - dot(c, c) * 1.6h);
                return half4(col, 1.0h);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
