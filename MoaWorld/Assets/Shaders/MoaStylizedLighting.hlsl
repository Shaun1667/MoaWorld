#ifndef MOA_STYLIZED_LIGHTING_INCLUDED
#define MOA_STYLIZED_LIGHTING_INCLUDED

// Soft stylized lighting shared by the MoaWorld shaders: wrapped diffuse with a soft light/shade edge,
// real-time main light shadows, SSAO, spherical-harmonics ambient and fog. Only the main (sun) light is used.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

struct MoaSurface
{
    half3 albedo;
    float3 positionWS;
    half3 normalWS;
    half3 viewDirWS;
    float4 positionCS;
};

struct MoaLightParams
{
    half wrap;          // 0 = plain Lambert, 1 = light wraps all the way around
    half edge;          // where the light/shade edge sits on the wrapped term
    half softness;      // width of that edge
    half3 shadeColor;   // tint of the shaded side (warm bounce light)
    half shadeStrength;
    half ambientStrength;
};

// Light amount on the surface in 0..1 (after wrap, edge, shadows and direct AO) and the main light itself.
half MoaLightAmount(MoaSurface s, MoaLightParams p, out Light mainLight, out AmbientOcclusionFactor ao)
{
    float4 shadowCoord = TransformWorldToShadowCoord(s.positionWS);
    mainLight = GetMainLight(shadowCoord);
    float2 screenUV = GetNormalizedScreenSpaceUV(s.positionCS);
    ao = GetScreenSpaceAmbientOcclusion(screenUV);

    half ndl = dot(s.normalWS, mainLight.direction);
    half wrapped = saturate((ndl + p.wrap) / (1.0h + p.wrap));
    half lit = smoothstep(p.edge - p.softness, p.edge + p.softness, wrapped);
    return lit * mainLight.shadowAttenuation * mainLight.distanceAttenuation * ao.directAmbientOcclusion;
}

half3 MoaShade(MoaSurface s, MoaLightParams p, half lit, Light mainLight, AmbientOcclusionFactor ao)
{
    half3 direct = s.albedo * mainLight.color * lit;
    // The shaded side picks up a little warm light so shadows never go flat grey.
    half3 bounce = s.albedo * p.shadeColor * mainLight.color * (1.0h - lit) * p.shadeStrength;
    half3 ambient = s.albedo * SampleSH(s.normalWS) * ao.indirectAmbientOcclusion * p.ambientStrength;
    return direct + bounce + ambient;
}

#endif
