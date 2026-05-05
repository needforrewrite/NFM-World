// Ground.vert.hlsl — Ground vertex shader (replaces Ground.fx "Fullbright" technique VS)

#include "Mad.hlsli"

cbuffer GroundUniforms : register(b0, space1)
{
    float4x4 WorldView;
    float4x4 WorldViewProj;
    float3 FogColor;
    float FogDistance;
    float FogDensity;
    float3 _pad;
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
    output.Position = mul(input.Position, WorldViewProj);
    output.Position1 = mul(input.Position, WorldViewProj);

    float3 color = input.Color;

    VS_ColorCorrect(color);

    output.Color = float4(color, 1.0);
    output.WorldPos = input.Position;

    return output;
}
