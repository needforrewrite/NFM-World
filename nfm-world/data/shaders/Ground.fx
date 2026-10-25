#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#endif

#include "./Mad.fxh"

float4x4 WorldView;
float4x4 WorldViewProj;

float3 FogColor;
float FogDistance;
// log2 of the normalized fog density (see VS_ApplyFog)
float FogLogDensity;

struct VertexShaderOutput
{
    float4 Position : SV_POSITION; // Vertex position in screen space
    float4 Color : COLOR0; // Vertex color
    float4 WorldPos : TEXCOORD2;
    float4 ViewPos : TEXCOORD4;
};

VertexShaderOutput VertexShaderFunction(
    float4 Position : POSITION, float3 Color : COLOR0)
{
    VertexShaderOutput output;
    output.Position = mul(Position, WorldViewProj); // Transform vertex position

    float3 color = Color;
    float3 viewPos = mul(Position, WorldView).xyz;
    output.ViewPos = float4(viewPos, 1.0);
    // VS_ApplyFog(color, viewPos, FogColor, FogDistance, FogLogDensity);
    VS_ColorCorrect(color);

    output.Color = float4(color, 1.0);

    // Save the vertices postion in world space (for shadow mapping)
    output.WorldPos = Position;

    return output;
}

float4 PixelShaderFunction(VertexShaderOutput input) : SV_TARGET
{
    float3 diffuse = input.Color.xyz;

    float viewLength = length(input.ViewPos.xyz);
    VS_ApplyFog(diffuse, viewLength, FogColor, FogDistance, FogLogDensity);

    PS_ApplyShadowing(diffuse, float4(input.WorldPos.xyz, 1), float3(0, 1, 0));

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
