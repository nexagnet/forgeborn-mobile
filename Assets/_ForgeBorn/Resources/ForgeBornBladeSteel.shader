// Steel look for the blade: heat glow (HDR emission from UV1), oxide tint + crack damage (vertex colour), brushed grind streaks.
// Single forward pass, no shadows, orthographic-camera aware. Deliberately cheap; not validated on any device yet.
Shader "ForgeBorn/BladeSteel"
{
    Properties
    {
        _BaseColor ("Steel colour", Color) = (0.66, 0.68, 0.72, 1)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.6
        _Brush ("Brushed streaks (0 = off)", Range(0, 1)) = 0.6
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Smoothness;
                half _Brush;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                float3 glow : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                half4 color : COLOR;
                float3 glow : TEXCOORD3;
            };

            Varyings Vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs vp = GetVertexPositionInputs(i.positionOS.xyz);
                VertexNormalInputs vn = GetVertexNormalInputs(i.normalOS);
                o.positionCS = vp.positionCS;
                o.positionWS = vp.positionWS;
                o.normalWS = vn.normalWS;
                o.positionOS = i.positionOS.xyz;
                o.color = i.color;
                o.glow = i.glow;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 v = unity_OrthoParams.w > 0.5
                    ? normalize(-UNITY_MATRIX_V[2].xyz)
                    : normalize(GetCameraPositionWS() - i.positionWS);

                // Brushed streaks run along the blade length (object X): vary with the cross-section position.
                float cell = floor(i.positionOS.y * 900.0 + i.positionOS.z * 900.0);
                half streak = frac(sin(cell * 127.1) * 43758.5453);
                half brush = 1.0h + (streak - 0.5h) * 0.18h * _Brush;

                half3 albedo = _BaseColor.rgb * i.color.rgb * brush;

                Light l = GetMainLight();
                half ndl = saturate(dot(n, l.direction));
                float3 h = normalize(l.direction + v);
                half ndh = saturate(dot(n, h));
                half shin = exp2(10.0h * _Smoothness + 1.0h);
                half spec = pow(ndh, shin) * (shin + 8.0h) * 0.04h * (1.0h + (streak - 0.5h) * _Brush);

                float3 r = reflect(-v, n);
                half env = saturate(r.y * 0.5h + 0.5h);

                half3 col = albedo * l.color * ndl * 0.55h;
                col += albedo * SampleSH(n) * 0.6h;
                col += albedo * l.color * spec;
                col += albedo * env * 0.22h;

                // Cracks: dark bands across the blade where damage is high.
                half crack = i.color.a * step(0.55h, frac(i.positionOS.x * 70.0));
                col = lerp(col, col * 0.15h, crack);

                // Heat glow: tone-map the HDR emission to LDR and let it replace the steel colour (hot metal is self-lit,
                // not a lit grey surface), otherwise additive glow washes out to pastel pink/white.
                half3 g = (1.0h - exp(-i.glow * 1.2h)) * (0.72h + 0.28h * ndl + 0.1h * streak * _Brush);
                half cover = saturate(max(g.r, max(g.g, g.b)) * 1.3h);
                col = lerp(col, g, cover);
                return half4(col, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            ShadowVaryings ShadowVert(ShadowAttributes i)
            {
                ShadowVaryings o;
                float3 positionWS = TransformObjectToWorld(i.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(i.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                o.positionCS = positionCS;
                return o;
            }

            half4 ShadowFrag(ShadowVaryings i) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            DepthVaryings DepthVert(DepthAttributes i)
            {
                DepthVaryings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                return o;
            }

            half DepthFrag(DepthVaryings i) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
