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

struct VertexShaderInput
{
	float3 Position : POSITION0;
	float3 Normal : NORMAL0;
	float3 Color : COLOR0;
	float3 Centroid : POSITION1;
	float DecalOffset : TEXCOORD0;
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

    float3 position = input.Position;

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

	// Apply diffuse lighting
	if (IsFullbright == false && isFullbright == false)
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

struct CreateShadowMap_VSOut
{
    float4 Position : SV_POSITION;
    float Depth     : TEXCOORD0;
};

// Transforms the model into light space an renders out the depth of the object
CreateShadowMap_VSOut CreateShadowMapVS(
    in VertexShaderInput input,
    float4x4 world : TEXCOORD3
)
{
    CreateShadowMap_VSOut output = (CreateShadowMap_VSOut)0;

    output.Position = mul(mul(mul(float4(input.Position, 1), world), View), Projection);
    output.Depth = output.Position.z / output.Position.w;
    return output;
}

// Saves the depth value out to the 32bit floating point texture
float4 CreateShadowMapPS(CreateShadowMap_VSOut input) : COLOR
{
    return float4(input.Depth, input.Depth, input.Depth, 1.0);
}


technique CreateShadowMap
{
    pass Pass1
    {
        VertexShader = compile vs_2_0 CreateShadowMapVS();
        PixelShader = compile ps_2_0 CreateShadowMapPS();
    }
}

technique Basic
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};