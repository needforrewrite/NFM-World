// Poly.vert.hlsl — Poly main vertex shader (replaces Poly.fx "Basic" technique VS)

#include "Mad.hlsli"
#include "SDLGPURegisters.hlsli"

cbuffer PolyUniforms : SDL_VS_UNIFORM(0)
{
    float4x4 View;
    float4x4 Projection;
    float4x4 ViewProj;
    float3   CameraPosition;
    float    Alpha;
    float3   SnapColor;
    float    Darken;
    float3   LightDirection;
    float    RandomFloat;
    float2   EnvironmentLight;
    bool     IsFullbright;
    bool     UseBaseColor;
    float3   BaseColor;
    bool     Expand;
    FogParams Fog;
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
    float4 Parameters : TEXCOORD7;
};

struct VSOutput
{
    float4 Position     : SV_POSITION;
    float4 Color        : COLOR0;
    float4 WorldPos     : TEXCOORD2;
    float  GetsShadowed : TEXCOORD3;
};

VSOutput main(VSInput input)
{
    bool getsShadowed;
    float alphaOverride;
    bool isFullbright;
    bool glow;
    VS_UnpackParameters(input.Parameters, getsShadowed, alphaOverride, isFullbright, glow);

    VSOutput output = (VSOutput)0;

    float3 position = input.Position;
    VS_DecalOffset(position, input.Normal, input.DecalOffset);

    if (Expand)
    {
        VS_Expand(position, input.Centroid, RandomFloat);
    }

    output.WorldPos = mul(input.World, float4(position, 1));
    output.GetsShadowed = getsShadowed ? 1.0 : 0.0;

    float4 viewPos = mul(View, output.WorldPos);

    float3 color = input.Color.rgb;

    if (UseBaseColor)
    {
        color = BaseColor;
    }

    output.Position = mul(Projection, viewPos);

    if (Darken < 1.0f)
    {
        VS_Darken(color, Darken);
    }

    if (!IsFullbright && !isFullbright)
    {
        VS_ApplyPolygonDiffuse(
            color,
            mul(input.World, float4(input.Centroid, 1)).xyz,
            normalize(mul(input.World, float4(input.Normal, 0)).xyz),
            LightDirection,
            CameraPosition,
            EnvironmentLight);

        VS_Snap(color, SnapColor);
    }

    VS_ApplyFog(color, viewPos.xyz, Fog.Color, Fog.Distance, Fog.Density);
    VS_ColorCorrect(color);

    output.Color = float4(color, min(alphaOverride, Alpha));

    return output;
}
