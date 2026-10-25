#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#endif

// Replacement for the four BasicEffect(LightingEnabled = false, VertexColorEnabled = true)
// instances Chips/Dust/FixFlare/Flames/FixHoop used to share - FNA3D has no BasicEffect
// equivalent, so this is a minimal hand-authored vertex-color pass-through shader. No fog/
// lighting/shadowing: none of the five original consumers had FogEnabled or LightingEnabled set.

float4x4 World;
float4x4 View;
float4x4 Projection;

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
};

VertexShaderOutput VertexShaderFunction(
    float4 Position : POSITION, float4 Color : COLOR0)
{
    VertexShaderOutput output;
    float4 worldPosition = mul(Position, World);
    float4 viewPosition = mul(worldPosition, View);
    output.Position = mul(viewPosition, Projection);
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
