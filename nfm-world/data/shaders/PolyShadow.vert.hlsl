// PolyShadow.vert.hlsl — Poly shadow map vertex shader (replaces Poly.fx "CreateShadowMap" technique VS)

#include "SDLGPURegisters.hlsli"

cbuffer ShadowUniforms : SDL_VS_UNIFORM(0)
{
    float4x4 View;
    float4x4 Projection;
};

struct VSInput
{
    float3 Position   : POSITION0;
    float3 Normal     : NORMAL0;
    float3 Centroid   : POSITION1;
    float4 Color      : COLOR0;
    float  DecalOffset : TEXCOORD0;
    // Instance data
    float4x4 World    : TEXCOORD3; // slots 3-6
};

struct VSOutput
{
    float4 Position : SV_POSITION;
    float  Depth    : TEXCOORD0;
};

VSOutput main(VSInput input)
{
    VSOutput output = (VSOutput)0;

    output.Position = mul(Projection, mul(View, mul(input.World, float4(input.Position, 1))));
    output.Depth = output.Position.z / output.Position.w;

    return output;
}
