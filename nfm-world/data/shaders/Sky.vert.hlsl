// Sky.vert.hlsl — Sky vertex shader (replaces Sky.fx "Fullbright" technique VS)

#include "SDLGPURegisters.hlsli"

cbuffer SkyUniforms : SDL_VS_UNIFORM(0)
{
    float4x4 WorldViewProj;
};

struct VSInput
{
    float4 Position : POSITION;
    float4 Color    : COLOR0;
};

struct VSOutput
{
    float4 Position : SV_POSITION;
    float4 Color    : COLOR0;
};

VSOutput main(VSInput input)
{
    VSOutput output;
    output.Position = mul(input.Position, WorldViewProj);
    output.Color = input.Color;
    return output;
}
