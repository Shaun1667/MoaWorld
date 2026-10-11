#ifndef MOA_SHADOW_DEPTH_PASSES_INCLUDED
#define MOA_SHADOW_DEPTH_PASSES_INCLUDED

// Shadow caster, depth and depth-normals passes shared by the MoaWorld shaders (the normals feed SSAO).

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

float3 _LightDirection;
float3 _LightPosition;

struct DepthAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct DepthVaryings
{
    float4 positionCS : SV_POSITION;
    half3 normalWS : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

DepthVaryings ShadowVert(DepthAttributes input)
{
    DepthVaryings output = (DepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
#if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
    float3 lightDirection = normalize(_LightPosition - positionWS);
#else
    float3 lightDirection = _LightDirection;
#endif
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection));
#if UNITY_REVERSED_Z
    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#else
    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#endif
    output.positionCS = positionCS;
    return output;
}

half4 ShadowFrag(DepthVaryings input) : SV_Target
{
    return 0;
}

DepthVaryings DepthVert(DepthAttributes input)
{
    DepthVaryings output = (DepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    return output;
}

half4 DepthFrag(DepthVaryings input) : SV_Target
{
    return input.positionCS.z;
}

half4 DepthNormalsFrag(DepthVaryings input) : SV_Target
{
#if defined(_GBUFFER_NORMALS_OCT)
    float3 normalWS = normalize(input.normalWS);
    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
    float2 remapped = saturate(octNormalWS * 0.5 + 0.5);
    return half4(PackFloat2To888(remapped), 0.0);
#else
    return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
#endif
}

#endif
