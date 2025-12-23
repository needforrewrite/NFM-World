#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#endif

matrix WorldViewProj;

struct VertexShaderOutput
{
    float4 Position : SV_POSITION; // Vertex position in screen space
    float4 Color : COLOR0; // Vertex color
};

VertexShaderOutput VertexShaderFunction(
    float4 Position : POSITION, float4 Color : COLOR0)
{
    VertexShaderOutput output;
    output.Position = mul(Position, WorldViewProj); // Transform vertex position
    output.Color = Color;
    return output;
}

float4 PixelShaderFunction(VertexShaderOutput input) : SV_TARGET
{
    return input.Color;
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