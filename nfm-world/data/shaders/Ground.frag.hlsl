// Ground.frag.hlsl — Ground fragment shader (replaces Ground.fx "Fullbright" technique PS)

#include "Mad.hlsli"

cbuffer GroundFragUniforms : register(b0, space3)
{
    float3 FogColor;
    float FogDistance;
    float FogDensity;
    float3 _pad;
};

struct PSInput
{
    float4 Position  : SV_POSITION;
    float4 Color     : COLOR0;
    float4 WorldPos  : TEXCOORD2;
    float4 Position1 : TEXCOORD3;
};

float4 main(PSInput input) : SV_TARGET
{
    float3 diffuse = input.Color.xyz;

    VS_ApplyFog(diffuse, input.Position1.z, FogColor, FogDistance, FogDensity);

    PS_ApplyShadowing(diffuse, float4(input.WorldPos.xyz, 1));

    return float4(diffuse, input.Color.w);
}
