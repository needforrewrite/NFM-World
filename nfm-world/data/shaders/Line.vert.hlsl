// Line.vert.hlsl — Line vertex shader (replaces Line.fx "Basic" technique VS)

#include "Mad.hlsli"
#include "SDLGPU.hlsli"

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
    float3 Position   : ATTRIBUTE(0);
    float3 Normal     : ATTRIBUTE(1);
    float3 Centroid   : ATTRIBUTE(2);
    float4 Color      : ATTRIBUTE(3);
    float  DecalOffset : ATTRIBUTE(4);
    float3 Right      : ATTRIBUTE(5);
    float3 Up         : ATTRIBUTE(6);
    // Instance data
    float4x4 World    : ATTRIBUTE(7); // slots 7-10
    float4 Parameters : ATTRIBUTE(11);
};

struct VSOutput
{
    float4 Position     : SV_POSITION;
    float4 Color        : COLOR0;
    float4 WorldPos     : ATTRIBUTE(2);
    float  GetsShadowed : ATTRIBUTE(3);
};

VSOutput main(VSInput input)
{
    bool getsShadowed;
    float alphaOverride;
    bool isFullbright;
    bool glow;
    VS_UnpackParameters(input.Parameters, getsShadowed, alphaOverride, isFullbright, glow);

    VSOutput output = (VSOutput)0;

    float distanceToCamera = mul(Projection, mul(View, mul(input.World, float4(input.Position, 1)))).z;

    float3 position = input.Position + input.Right * HalfThickness * distanceToCamera + input.Up * HalfThickness * distanceToCamera;

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

    if (glow)
    {
        color = min(color * 1.6, float3(1.0, 1.0, 1.0));
    }

    if (!IsFullbright && !isFullbright && !glow)
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
