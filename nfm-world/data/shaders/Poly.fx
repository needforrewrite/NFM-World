#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_4_0_level_9_1
	#define PS_SHADERMODEL ps_4_0_level_9_1
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
matrix LightViewProj;
float DepthBias = 0.25f;
bool GetsShadowed;

texture ShadowMap;
sampler ShadowMapSampler = sampler_state
{
    Texture = <ShadowMap>;
    AddressU = Clamp;
    AddressV = Clamp;
};

struct VertexShaderInput
{
	float3 Position : POSITION0;
	float3 Normal : NORMAL0;
	float3 Color : COLOR0;
	float3 Centroid : POSITION1;
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

	float3 viewPos = mul(float4(input.Position, 1), WorldView).xyz;
	output.Position = mul(float4(input.Position, 1), WorldViewProj);

	float3 color = lerp(input.Color, BaseColor, UseBaseColor ? 1.0 : 0.0);

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
	color += (color * (SnapColor * 255.0 / 100.0));

    VS_ApplyFog(color, viewPos, FogColor, FogDistance, FogDensity);

    VS_ColorCorrect(color);

    output.Color = float4(color, Alpha);

    // Save the vertices postion in world space (for shadow mapping)
    output.WorldPos = mul(float4(input.Position, 1), World);

	return output;
}

float4 MainPS(VertexShaderOutput input) : COLOR
{
    float4 diffuse = input.Color;

    if (GetsShadowed == true)
    {
        // Find the position of this pixel in light space
        float4 lightingPosition = mul(input.WorldPos, LightViewProj);

        float3 diffuseRGB = diffuse.xyz;
        PS_ApplyShadowing(diffuseRGB, lightingPosition, ShadowMapSampler, DepthBias);
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
CreateShadowMap_VSOut CreateShadowMapVS(in VertexShaderInput input)
{
    CreateShadowMap_VSOut output = (CreateShadowMap_VSOut)0;
    output.Position = mul(float4(input.Position, 1), WorldViewProj);
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