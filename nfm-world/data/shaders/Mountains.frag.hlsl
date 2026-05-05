// Mountains.frag.hlsl — Mountains fragment shader (replaces Mountains.fx "Fullbright" technique PS)

#include "Mad.hlsli"

struct PSInput
{
    float4 Position : SV_POSITION;
    float4 Color    : COLOR0;
    float4 WorldPos : TEXCOORD2;
};

float4 main(PSInput input) : SV_TARGET
{
    float3 diffuse = input.Color.xyz;

    PS_ApplyShadowing(diffuse, float4(input.WorldPos.xyz, 1));

    return float4(diffuse, input.Color.w);
}
