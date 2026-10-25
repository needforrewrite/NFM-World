cbuffer _Global : register(b0)
{
    column_major float4x4 ViewProj : packoffset(c0);
    float4 Color : packoffset(c4);
    float2 ViewportSize : packoffset(c5);
    float Thickness : packoffset(c5.z);
};

Texture2D<float4> Texture : register(t0);
SamplerState TextureSampler : register(s0);

struct VSInput
{
    float3 Position : POSITION0;
    float4 Color : COLOR0;
    float Side : TEXCOORD0;
    float4 World0 : TEXCOORD1;
    float4 World1 : TEXCOORD2;
    float4 World2 : TEXCOORD3;
    float4 World3 : TEXCOORD4;
    float4 Parameters : TEXCOORD5;
};

struct VSOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

VSOutput main(VSInput input)
{
    float4x4 world = float4x4(input.World0, input.World1, input.World2, input.World3);
    float4 worldPosition = mul(float4(input.Position, 1.0), world);

    VSOutput output;
    output.Position = mul(worldPosition, ViewProj);
    output.Color = input.Color * Color;
    output.TexCoord = float2(input.Side * 0.5 + 0.5, input.Position.z * 0.5);
    // output.Color *= input.Parameters;
    return output;
}