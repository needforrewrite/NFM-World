#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#endif

// Dear ImGui's per-vertex draw shader: vertex positions are already in orthographic screen space
// (ImGui itself never has a "world" - Projection is the only transform), vertex color modulates
// whatever texture is currently bound (the font atlas, or any other ImTextureID) - matches what
// every reference ImGui backend (OpenGL3/DX11/etc.) does.

float4x4 Projection;

#if SM6
Texture2D Texture : register(t0);
SamplerState TextureSampler : register(s0);
#else
texture Texture;
sampler TextureSampler = sampler_state
{
    Texture = <Texture>;
    MinFilter = LINEAR;
    MagFilter = LINEAR;
    MipFilter = LINEAR;
    AddressU = CLAMP;
    AddressV = CLAMP;
};
#endif

struct VertexShaderInput
{
    float2 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
    float4 Color : COLOR0;
};

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

VertexShaderOutput VertexShaderFunction(VertexShaderInput input)
{
    VertexShaderOutput output;
    output.Position = mul(float4(input.Position, 0, 1), Projection);
    output.Color = input.Color;
    output.TexCoord = input.TexCoord;
    return output;
}

float4 PixelShaderFunction(VertexShaderOutput input) : SV_TARGET
{
    #if SM6
    return input.Color * Texture.Sample(TextureSampler, input.TexCoord);
    #else
    return input.Color * tex2D(TextureSampler, input.TexCoord);
    #endif
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
