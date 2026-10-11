// Ground and terrain without textures: world-space noise mixes two base colors with larger patches, steep
// slopes turn to rock, and a fine grain breaks up flat areas. The same material on the plane and the hills
// makes them blend into one landscape.
Shader "MoaWorld/Stylized Ground"
{
    Properties
    {
        _ColorA ("Base Color A", Color) = (0.42, 0.66, 0.27, 1)
        _ColorB ("Base Color B", Color) = (0.55, 0.74, 0.32, 1)
        _PatchColor ("Patch Color", Color) = (0.62, 0.6, 0.36, 1)
        _RockColor ("Slope Color", Color) = (0.55, 0.55, 0.52, 1)
        _NoiseScale ("Base Noise Scale", Float) = 0.035
        _PatchScale ("Patch Scale", Float) = 0.012
        _PatchAmount ("Patch Amount", Range(0, 1)) = 0.25
        _SlopeStart ("Slope Start", Range(0, 1)) = 0.35
        _SlopeEnd ("Slope End", Range(0, 1)) = 0.55
        _DetailScale ("Grain Scale", Float) = 0.9
        _DetailStrength ("Grain Strength", Range(0, 0.5)) = 0.08

        [Header(Light)]
        _Wrap ("Wrap", Range(0, 1)) = 0.3
        _Edge ("Shade Edge", Range(0, 1)) = 0.4
        _Softness ("Edge Softness", Range(0.01, 0.5)) = 0.35
        _ShadeColor ("Shade Tint", Color) = (0.55, 0.7, 1, 1)
        _ShadeStrength ("Shade Tint Strength", Range(0, 1)) = 0.15
        _AmbientStrength ("Ambient Strength", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _ColorA;
            half4 _ColorB;
            half4 _PatchColor;
            half4 _RockColor;
            float _NoiseScale;
            float _PatchScale;
            half _PatchAmount;
            half _SlopeStart;
            half _SlopeEnd;
            float _DetailScale;
            half _DetailStrength;
            half _Wrap;
            half _Edge;
            half _Softness;
            half4 _ShadeColor;
            half _ShadeStrength;
            half _AmbientStrength;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "MoaStylizedLighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash(i);
                float b = Hash(i + float2(1, 0));
                float c = Hash(i + float2(0, 1));
                float d = Hash(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float Fbm(float2 p)
            {
                return ValueNoise(p) * 0.65 + ValueNoise(p * 2.13 + 17.1) * 0.35;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float2 xz = input.positionWS.xz;
                half3 normalWS = NormalizeNormalPerPixel(input.normalWS);

                half base = Fbm(xz * _NoiseScale);
                half3 albedo = lerp(_ColorA.rgb, _ColorB.rgb, smoothstep(0.25, 0.75, base));
                half patch = smoothstep(1.0 - _PatchAmount, 1.0 - _PatchAmount + 0.15, Fbm(xz * _PatchScale + 31.7));
                albedo = lerp(albedo, _PatchColor.rgb, patch);

                half slope = smoothstep(_SlopeStart, _SlopeEnd, 1.0h - normalWS.y);
                half3 rock = _RockColor.rgb * (0.85h + 0.3h * Fbm(input.positionWS.xy * 0.08 + input.positionWS.zy * 0.05));
                albedo = lerp(albedo, rock, slope);

                half grain = ValueNoise(xz * _DetailScale) - 0.5h;
                albedo *= 1.0h + grain * _DetailStrength * 2.0h;

                MoaSurface s;
                s.albedo = albedo;
                s.positionWS = input.positionWS;
                s.normalWS = normalWS;
                s.viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                s.positionCS = input.positionCS;

                MoaLightParams p;
                p.wrap = _Wrap;
                p.edge = _Edge;
                p.softness = _Softness;
                p.shadeColor = _ShadeColor.rgb;
                p.shadeStrength = _ShadeStrength;
                p.ambientStrength = _AmbientStrength;

                Light mainLight;
                AmbientOcclusionFactor ao;
                half lit = MoaLightAmount(s, p, mainLight, ao);
                half3 color = MoaShade(s, p, lit, mainLight, ao);
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
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
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "MoaShadowDepthPasses.hlsl"
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
            #pragma multi_compile_instancing
            #include "MoaShadowDepthPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "MoaShadowDepthPasses.hlsl"
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
