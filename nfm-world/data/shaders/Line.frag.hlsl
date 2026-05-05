// Line.frag.hlsl — Line fragment shader (replaces Line.fx "Basic" technique PS)

#include "Mad.hlsli"

struct PSInput
{
    float4 Position     : SV_POSITION;
    float4 Color        : COLOR0;
    float4 WorldPos     : TEXCOORD2;
    float  GetsShadowed : TEXCOORD3;
};

float4 main(PSInput input) : SV_TARGET
{
    float4 diffuse = input.Color;

    if (input.GetsShadowed > 0.0)
    {
        float3 diffuseRGB = diffuse.xyz;
        PS_ApplyShadowing(diffuseRGB, input.WorldPos);
        diffuse = float4(diffuseRGB, diffuse.w);
    }

    return diffuse;
}
