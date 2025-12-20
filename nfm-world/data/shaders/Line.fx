#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#endif

#include "./Mad.fxh"

// BasicEffect
matrix World;
matrix WorldInverseTranspose;
matrix WorldViewProj;
matrix View;
matrix Projection;

// Custom
matrix WorldView;
float3 SnapColor;
bool IsFullbright;
bool UseBaseColor;
float3 BaseColor;
float3 LightDirection;
float3 FogColor;
float FogDistance;
float FogDensity;
float2 EnvironmentLight;
float3 CameraPosition;
float Alpha;

// Lighting
bool GetsShadowed;

// Damage
bool Expand;
float RandomFloat;
float Darken; // set below 1.0f to adjust brightness

// Charged line blink
float ChargedBlinkAmount;

// Next checkpoint line glow
bool Glow;

float HalfThickness;

struct VertexShaderInput
{
	float3 Position : POSITION0;
	float3 Normal : NORMAL0;
	float3 Color : COLOR0;
	float3 Centroid : POSITION1;
	float DecalOffset : TEXCOORD0;
	float3 Right : TEXCOORD1;
	float3 Up : TEXCOORD2;
};

struct VertexShaderOutput
{
	float4 Position : SV_POSITION;
	float4 Color : COLOR0;
    float4 WorldPos : TEXCOORD2;
};

VertexShaderOutput MainVS(in VertexShaderInput input)
{
	VertexShaderOutput output = (VertexShaderOutput)0;

    float distanceToCamera = length(CameraPosition - mul(float4(input.Position, 1), World).xyz);

    float3 position = input.Position + input.Right * HalfThickness * distanceToCamera + input.Up * HalfThickness * distanceToCamera;

    VS_DecalOffset(position, input.Normal, input.DecalOffset);

	float3 viewPos = mul(float4(position, 1), WorldView).xyz;
	float3 color = input.Color;

    // Apply base color
    if (UseBaseColor == true)
    {
        color = BaseColor;
    }

    if (Expand == true)
    {
        VS_Expand(position, input.Centroid, RandomFloat);
    }

    output.Position = mul(float4(position, 1), WorldViewProj);

    if (Darken < 1.0f)
    {
        VS_Darken(color, Darken);
    }

    if (Glow == true)
    {
        color = color * 1.6;
        // clamp to 1.0
        color = min(color, float3(1.0, 1.0, 1.0));
    }

	// Apply diffuse lighting
	if (IsFullbright == false)
    {
        VS_ApplyPolygonDiffuse(
            color,
            mul(float4(input.Centroid, 1), World).xyz,
            normalize(mul(float4(input.Normal, 0), WorldInverseTranspose).xyz),
            LightDirection,
            CameraPosition,
            EnvironmentLight
        );
	}

	// Apply snap
    VS_Snap(color, SnapColor);

    if (ChargedBlinkAmount > 0.0f)
    {
        color.r = (25.5 * ChargedBlinkAmount) / 255.0;
        color.g = (128.0 + 12.8 * ChargedBlinkAmount) / 255.0;
        color.b = 1.0;
    }

    VS_ApplyFog(color, viewPos, FogColor, FogDistance, FogDensity);

    VS_ColorCorrect(color);

    output.Color = float4(color, Alpha);

    // Save the vertices postion in world space (for shadow mapping)
    output.WorldPos = mul(float4(position, 1), World);

	return output;
}

float4 MainPS(VertexShaderOutput input) : SV_TARGET
{
    float4 diffuse = input.Color;

    if (GetsShadowed == true)
    {
        float3 diffuseRGB = diffuse.xyz;
        PS_ApplyShadowing(diffuseRGB, input.WorldPos);
        diffuse = float4(diffuseRGB, diffuse.w);
    }

	return diffuse;
}

technique Basic
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};