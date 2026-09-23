struct VSInput
{
    float3 Position : POSITION0;
    float4 Unused  : TEXCOORD0;
    float4 AlsoUnused : TEXCOORD2;
    float4 Used    : TEXCOORD3;
};

cbuffer _Global : register(b0)
{
    column_major float4x4 ViewProj : packoffset(c0);
};

float4 VertexShaderFunction(VSInput input) : SV_POSITION
{
    return mul(float4(input.Position + input.Used.xyz, 1.0), ViewProj);
}

float4 PixelShaderFunction(float4 pos : SV_POSITION) : SV_TARGET
{
    return float4(1, 1, 1, 1);
}
