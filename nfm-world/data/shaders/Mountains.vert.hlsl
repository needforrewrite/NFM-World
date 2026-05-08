// Mountains.vert.hlsl — Mountains vertex shader (replaces Mountains.fx "Fullbright" technique VS)

#include "Mad.hlsli"
#include "SDLGPU.hlsli"

cbuffer MountainsUniforms : SDL_VS_UNIFORM(0)
{
    float4x4  WorldView;
    float4x4  WorldViewProj;
    FogParams Fog;
};

struct VSInput
{
    float4 Position : ATTRIBUTE(0);
    float3 Color    : ATTRIBUTE(1);
};

struct VSOutput
{
    float4 Position : SV_POSITION;
    float4 Color    : COLOR0;
    float4 WorldPos : ATTRIBUTE(2);
};

VSOutput main(VSInput input)
{
    VSOutput output;
    output.Position = mul(WorldViewProj, input.Position);

    float3 color = input.Color;
    float3 viewPos = mul(WorldView, input.Position).xyz;
    VS_ApplyFog(color, viewPos, Fog.Color, Fog.Distance, Fog.Density);

    VS_ColorCorrect(color);

    output.Color = float4(color, 1.0);
    output.WorldPos = input.Position;

    return output;
}
