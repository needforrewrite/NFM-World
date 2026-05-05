// Ground.vert.hlsl — Ground vertex shader (replaces Ground.fx "Fullbright" technique VS)

#include "Mad.hlsli"
#include "SDLGPURegisters.hlsli"

cbuffer GroundUniforms : SDL_VS_UNIFORM(0)
{
    float4x4  WorldView;
    float4x4  WorldViewProj;
    FogParams Fog;
};

struct VSInput
{
    float4 Position : POSITION;
    float3 Color    : COLOR0;
};

struct VSOutput
{
    float4 Position  : SV_POSITION;
    float4 Color     : COLOR0;
    float4 WorldPos  : TEXCOORD2;
    float4 Position1 : TEXCOORD3;
};

VSOutput main(VSInput input)
{
    VSOutput output;
    output.Position = mul(WorldViewProj, input.Position);
    output.Position1 = mul(WorldViewProj, input.Position);

    float3 color = input.Color;

    VS_ColorCorrect(color);

    output.Color = float4(color, 1.0);
    output.WorldPos = input.Position;

    return output;
}
