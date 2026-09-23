cbuffer _Global : register(b0)
{
    column_major float4x4 ViewProj : packoffset(c0);
    float4 Color : packoffset(c4);
    float2 ViewportSize : packoffset(c5);
    float Thickness : packoffset(c5.z);
};

struct VSInput
{
    float3 Position : POSITION0;
    float4 Color : COLOR0;
    float Side : TEXCOORD0;
};

struct VSOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

VSOutput main(VSInput input)
{
    VSOutput output;
    output.Position = mul(float4(input.Position, 1.0), ViewProj);
    output.Color = input.Color * Color;
    output.TexCoord = float2(input.Side * 0.5 + 0.5, input.Position.z * 0.5);
    return output;
}
