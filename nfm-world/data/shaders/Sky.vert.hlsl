// Sky.vert.hlsl — Sky vertex shader (replaces Sky.fx "Fullbright" technique VS)

#include "SDLGPU.hlsli"

cbuffer SkyUniforms : SDL_VS_UNIFORM(0)
{
    float4x4 WorldViewProj;
};

struct VSInput
{
    float4 Position : ATTRIBUTE(0);
    float4 Color    : ATTRIBUTE(1);
};

struct VSOutput
{
    float4 Position : SV_POSITION;
    float4 Color    : COLOR0;
};

VSOutput main(VSInput input)
{
    VSOutput output;
    output.Position = mul(WorldViewProj, input.Position);
    output.Color = input.Color;
    return output;
}
