// PolyShadow.vert.hlsl — Poly shadow map vertex shader (replaces Poly.fx "CreateShadowMap" technique VS)

#include "SDLGPU.hlsli"

cbuffer ShadowUniforms : SDL_VS_UNIFORM(0)
{
    float4x4 View;
    float4x4 Projection;
};

struct VSInput
{
    float3 Position   : ATTRIBUTE(0);
    float3 Normal     : ATTRIBUTE(1);
    float3 Centroid   : ATTRIBUTE(2);
    float4 Color      : ATTRIBUTE(3);
    float  DecalOffset : ATTRIBUTE(4);
    // Instance data
    float4x4 World    : ATTRIBUTE(5); // slots 5-8
};

struct VSOutput
{
    float4 Position : SV_POSITION;
    float  Depth    : ATTRIBUTE(0);
};

VSOutput main(VSInput input)
{
    VSOutput output = (VSOutput)0;

    output.Position = mul(Projection, mul(View, mul(input.World, float4(input.Position, 1))));
    output.Depth = output.Position.z / output.Position.w;

    return output;
}
