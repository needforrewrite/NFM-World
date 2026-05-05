// Line.vert.hlsl — Line vertex shader (replaces Line.fx "Basic" technique VS)

#include "Mad.hlsli"
#include "SDLGPURegisters.hlsli"

cbuffer LineUniforms : SDL_VS_UNIFORM(0)
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
    float    HalfThickness;
    float    ChargedBlinkAmount;
    float2   _pad;
    FogParams Fog;
};

struct VSInput
{
    float3 Position   : POSITION0;
    float3 Normal     : NORMAL0;
    float3 Centroid   : POSITION1;
    float4 Color      : COLOR0;
    float  DecalOffset : TEXCOORD0;
    float3 Right      : TEXCOORD1;
    float3 Up         : TEXCOORD2;
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

    float distanceToCamera = mul(mul(mul(float4(input.Position, 1), input.World), View), Projection).z;

    float3 position = input.Position + input.Right * HalfThickness * distanceToCamera + input.Up * HalfThickness * distanceToCamera;

    VS_DecalOffset(position, input.Normal, input.DecalOffset);

    if (Expand)
    {
        VS_Expand(position, input.Centroid, RandomFloat);
    }

    output.WorldPos = mul(float4(position, 1), input.World);
    output.GetsShadowed = getsShadowed ? 1.0 : 0.0;

    float4 viewPos = mul(output.WorldPos, View);

    float3 color = input.Color.rgb;

    if (UseBaseColor)
    {
        color = BaseColor;
    }

    output.Position = mul(viewPos, Projection);

    if (Darken < 1.0f)
    {
        VS_Darken(color, Darken);
    }

    if (glow)
    {
        color = min(color * 1.6, float3(1.0, 1.0, 1.0));
    }

    if (!IsFullbright && !isFullbright && !glow)
    {
        VS_ApplyPolygonDiffuse(
            color,
            mul(float4(input.Centroid, 1), input.World).xyz,
            normalize(mul(float4(input.Normal, 0), input.World).xyz),
            LightDirection,
            CameraPosition,
            EnvironmentLight);

        VS_Snap(color, SnapColor);
    }

    if (ChargedBlinkAmount > 0.0f)
    {
        color.r = (25.5 * ChargedBlinkAmount) / 255.0;
        color.g = (128.0 + 12.8 * ChargedBlinkAmount) / 255.0;
        color.b = 1.0;
    }

    VS_ApplyFog(color, viewPos.xyz, Fog.Color, Fog.Distance, Fog.Density);
    VS_ColorCorrect(color);

    output.Color = float4(color, min(alphaOverride, Alpha));

    return output;
}
