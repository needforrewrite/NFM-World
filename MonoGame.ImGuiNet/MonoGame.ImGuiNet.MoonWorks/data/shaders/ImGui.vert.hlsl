// ImGui vertex shader for MoonWorks (ShaderCross HLSL)

cbuffer VertexUniforms : register(b0, space1)
{
    float4x4 ProjectionMatrix;
};

struct VSInput
{
    float2 Position : TEXCOORD0;
    float2 TexCoord : TEXCOORD1;
    float4 Color    : TEXCOORD2;
};

struct VSOutput
{
    float4 Position : SV_Position;
    float2 TexCoord : TEXCOORD0;
    float4 Color    : TEXCOORD1;
};

VSOutput main(VSInput input)
{
    VSOutput output;
    output.Position = mul(float4(input.Position, 0.0, 1.0), ProjectionMatrix);
    output.TexCoord = input.TexCoord;
    output.Color = input.Color;
    return output;
}
