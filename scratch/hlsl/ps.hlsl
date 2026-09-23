cbuffer _Global : register(b0)
{
    column_major float4x4 ViewProj : packoffset(c0);
    float4 Color : packoffset(c4);
    float2 ViewportSize : packoffset(c5);
    float Thickness : packoffset(c5.z);
};

Texture2D<float4> Texture : register(t0);
SamplerState TextureSampler : register(s0);

struct PSInput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

float4 main(PSInput input) : SV_TARGET
{
    return input.Color * Texture.Sample(TextureSampler, input.TexCoord);
}