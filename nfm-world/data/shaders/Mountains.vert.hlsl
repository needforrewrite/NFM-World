// Mountains.vert.hlsl — Mountains vertex shader (replaces Mountains.fx "Fullbright" technique VS)

#include "Mad.hlsli"
#include "SDLGPURegisters.hlsli"

cbuffer MountainsUniforms : SDL_VS_UNIFORM(0)
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
    float4 Position : SV_POSITION;
    float4 Color    : COLOR0;
    float4 WorldPos : TEXCOORD2;
};

VSOutput main(VSInput input)
{
    VSOutput output;
    output.Position = mul(input.Position, WorldViewProj);

    float3 color = input.Color;
    float3 viewPos = mul(input.Position, WorldView).xyz;
    VS_ApplyFog(color, viewPos, Fog.Color, Fog.Distance, Fog.Density);

    VS_ColorCorrect(color);

    output.Color = float4(color, 1.0);
    output.WorldPos = input.Position;

    return output;
}
