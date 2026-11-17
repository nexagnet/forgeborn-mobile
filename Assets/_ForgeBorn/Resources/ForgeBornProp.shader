// Textured PBR-ish prop shader (anvil, furnace, wall): albedo + normal + roughness maps, metal flag, environment cube reflection,
// optional emissive HDR glow and a warm radial furnace light on the wall. Single forward pass, cheap on purpose.
Shader "ForgeBorn/Prop"
{
    Properties
    {
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _MainTex ("Albedo", 2D) = "white" {}
        _BumpMap ("Normal (GL)", 2D) = "bump" {}
        _RoughTex ("Roughness", 2D) = "white" {}
        _Tiling ("Tiling", Vector) = (1, 1, 0, 0)
        _Metal ("Metalness", Range(0, 1)) = 0
        _RoughScale ("Roughness scale", Range(0, 2)) = 1
        _BumpScale ("Normal strength", Range(0, 2)) = 1
        [HDR] _Emission ("Emission", Color) = (0, 0, 0, 1)
        [HDR] _WallGlow ("Furnace light on surface", Color) = (0, 0, 0, 1)
        _GlowPos ("Furnace light position (world)", Vector) = (-0.3, -0.1, 0, 0)
        _GlowRadius ("Furnace light radius", Float) = 0.8
        [NoScaleOffset] _EnvCube ("Environment cube", Cube) = "grey" {}
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

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURE2D(_RoughTex); SAMPLER(sampler_RoughTex);
            TEXTURECUBE(_EnvCube); SAMPLER(sampler_EnvCube);

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float4 _Tiling;
                half _Metal;
                half _RoughScale;
                half _BumpScale;
                half4 _Emission;
                half4 _WallGlow;
                float4 _GlowPos;
                float _GlowRadius;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 tangentWS : TEXCOORD2;
                float3 bitangentWS : TEXCOORD3;
                float2 uv : TEXCOORD4;
            };

            Varyings Vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs vp = GetVertexPositionInputs(i.positionOS.xyz);
                VertexNormalInputs vn = GetVertexNormalInputs(i.normalOS, i.tangentOS);
                o.positionCS = vp.positionCS;
                o.positionWS = vp.positionWS;
                o.normalWS = vn.normalWS;
                o.tangentWS = vn.tangentWS;
                o.bitangentWS = vn.bitangentWS;
                o.uv = i.uv * _Tiling.xy + _Tiling.zw;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half3 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb * _Tint.rgb;
                half rough = saturate(SAMPLE_TEXTURE2D(_RoughTex, sampler_RoughTex, i.uv).r * _RoughScale);
                half3 nt = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv), _BumpScale);
                float3 n = normalize(nt.x * i.tangentWS + nt.y * i.bitangentWS + nt.z * i.normalWS);
                float3 v = unity_OrthoParams.w > 0.5
                    ? normalize(UNITY_MATRIX_V[2].xyz)
                    : normalize(GetCameraPositionWS() - i.positionWS);

                Light l = GetMainLight();
                half ndl = saturate(dot(n, l.direction));
                float3 h = normalize(l.direction + v);
                half ndh = saturate(dot(n, h));
                half shin = exp2(10.0h * (1.0h - rough) + 1.0h);
                half spec = pow(ndh, shin) * (shin + 8.0h) * 0.03h;

                float3 r = reflect(-v, n);
                half3 envCol = SAMPLE_TEXTURECUBE_LOD(_EnvCube, sampler_EnvCube, r, rough * 5.0h).rgb;
                half ndv = saturate(dot(n, v));
                half fres = pow(1.0h - ndv, 5.0h);
                half3 f0 = lerp(half3(0.04h, 0.04h, 0.04h), albedo, _Metal);
                half3 fr = f0 + (1.0h - f0) * fres;
                half3 diffuse = albedo * (1.0h - _Metal);

                half3 col = diffuse * (l.color * ndl * 0.6h + SampleSH(n) * 0.4h);
                col += l.color * spec * f0;
                col += envCol * fr * (1.0h - rough * 0.6h) * 2.6h;

                // Warm light from the furnace mouth: falls off with distance in the view plane.
                float2 dp = i.positionWS.xy - _GlowPos.xy;
                half fall = saturate(1.0h - dot(dp, dp) / (_GlowRadius * _GlowRadius));
                col += albedo * _WallGlow.rgb * fall * fall * (0.5h + 0.5h * saturate(n.z * -1.0h + 0.5h));

                col += _Emission.rgb;
                return half4(col, 1.0h);
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

            struct DepthAttributes { float4 positionOS : POSITION; };
            struct DepthVaryings { float4 positionCS : SV_POSITION; };

            DepthVaryings DepthVert(DepthAttributes i)
            {
                DepthVaryings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                return o;
            }

            half DepthFrag(DepthVaryings i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
    Fallback Off
}
