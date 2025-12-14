#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_4_0_level_9_1
	#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

#include "./Mad.fxh"

matrix WorldView;
matrix WorldViewProj;

float3 FogColor;
float FogDistance;
float FogDensity;

// Lighting
matrix LightViewProj;
float DepthBias = 0.25f;

texture ShadowMap;
sampler ShadowMapSampler = sampler_state
{
    Texture = <ShadowMap>;
    AddressU = Clamp;
    AddressV = Clamp;
};

struct VertexShaderOutput
{
    float4 Position : SV_POSITION; // Vertex position in screen space
    float4 Color : COLOR0; // Vertex color
    float4 WorldPos : TEXCOORD2;
};

VertexShaderOutput VertexShaderFunction(
    float4 Position : POSITION, float4 Color : COLOR0)
{
    VertexShaderOutput output;
    output.Position = mul(Position, WorldViewProj); // Transform vertex position

    float3 color = Color.xyz;
	float3 viewPos = mul(Position, WorldView).xyz;
    VS_ApplyFog(color, viewPos, FogColor, FogDistance, FogDensity);

    VS_ColorCorrect(color);

    output.Color = float4(color, Color.w);

    // Save the vertices postion in world space (for shadow mapping)
    output.WorldPos = Position;

    return output;
}

float4 PixelShaderFunction(VertexShaderOutput input) : COLOR
{
    float3 diffuse = input.Color.xyz;

    // Find the position of this pixel in light space
    float4 lightingPosition = mul(float4(input.WorldPos.xyz, 1), LightViewProj);

    PS_ApplyShadowing(diffuse, lightingPosition, ShadowMapSampler, DepthBias);
    return float4(diffuse, input.Color.w);
}

// Technique definition for use in C#
technique Fullbright
{
    pass Pass0
    {
        VertexShader = compile VS_SHADERMODEL VertexShaderFunction();
        PixelShader = compile PS_SHADERMODEL PixelShaderFunction();
    }
};