// BasicEffect vertex shader — simple WVP transform with vertex color pass-through.
// Replaces FNA/XNA BasicEffect for VertexPositionColor rendering.

#include "SDLGPU.hlsli"

cbuffer VertexUniforms : SDL_VS_UNIFORM(0)
{
    float4x4 WorldViewProjection;
};

struct VSInput
{
    float3 Position : ATTRIBUTE(0);
    float4 Color    : ATTRIBUTE(1);
};

struct VSOutput
{
    float4 Position : SV_Position;
    float4 Color    : ATTRIBUTE(0);
};

VSOutput main(VSInput input)
{
    VSOutput output;
    output.Position = mul(WorldViewProjection, float4(input.Position, 1.0));
    output.Color = input.Color;
    return output;
}
