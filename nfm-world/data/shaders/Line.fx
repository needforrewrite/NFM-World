#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#endif

#include "./Mad.fxh"

matrix View;
matrix Projection;
matrix ViewProj;
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

// Damage
bool Expand;
float RandomFloat;
float Darken; // set below 1.0f to adjust brightness

// Charged line blink
float ChargedBlinkAmount;

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
    float GetsShadowed : TEXCOORD3;
};

VertexShaderOutput MainVS(
    in VertexShaderInput input,
    // instance parameters
    in float4x4 world : TEXCOORD3,
    in float4 parameters : TEXCOORD7
)
{
    bool getsShadowed;
    float alphaOverride;
    bool isFullbright;
    bool glow;
    VS_UnpackParameters(parameters, getsShadowed, alphaOverride, isFullbright, glow);

	VertexShaderOutput output = (VertexShaderOutput)0;

    float distanceToCamera = mul(mul(mul(float4(input.Position, 1), world), View), Projection).z;

    float3 position = input.Position + input.Right * HalfThickness * distanceToCamera + input.Up * HalfThickness * distanceToCamera;

    VS_DecalOffset(position, input.Normal, input.DecalOffset);

    if (Expand == true)
    {
        VS_Expand(position, input.Centroid, RandomFloat);
    }

    // Save the vertices postion in world space (for shadow mapping)
    output.WorldPos = mul(float4(position, 1), world);
    output.GetsShadowed = getsShadowed;

    float4 viewPos = mul(output.WorldPos, View);

	float3 color = input.Color;

    // Apply base color
    if (UseBaseColor == true)
    {
        color = BaseColor;
    }

    output.Position = mul(viewPos, Projection);

    if (Darken < 1.0f)
    {
        VS_Darken(color, Darken);
    }

    if (glow == true)
    {
        color = color * 1.6;
        // clamp to 1.0
        color = min(color, float3(1.0, 1.0, 1.0));
    }

	// Apply diffuse lighting
	if (IsFullbright == false && isFullbright == false && glow == false)
    {
        VS_ApplyPolygonDiffuse(
            color,
            mul(float4(input.Centroid, 1), world).xyz,
            normalize(mul(float4(input.Normal, 0), world).xyz),
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

    VS_ApplyFog(color, viewPos.xyz, FogColor, FogDistance, FogDensity);

    VS_ColorCorrect(color);

    output.Color = float4(color, min(alphaOverride, Alpha));

	return output;
}

float4 MainPS(VertexShaderOutput input) : SV_TARGET
{
    float4 diffuse = input.Color;

    if (input.GetsShadowed > 0.0)
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