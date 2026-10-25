cbuffer _Global : register(b0)
{
    column_major float4x4 ViewProj : packoffset(c0);
    float4 Color : packoffset(c4);
    float2 ViewportSize : packoffset(c5);
    float Thickness : packoffset(c5.z);
};

// What spirv-cross emits when the original declared POSITION0, COLOR0, TEXCOORD0,
// TEXCOORD1, TEXCOORD2 but the shader only dereferences POSITION and TEXCOORD2:
// the unused parameters are simply not declared.
struct VSInput
{
    float3 Position : POSITION0;
    float4 World2 : TEXCOORD2;
};

struct VSOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

VSOutput main(VSInput input)
{
    float4x4 world = float4x4(1,0,0,0, 0,1,0,0, 0,0,1,0, input.World2.x,0,0,1);
    VSOutput output;
    output.Position = mul(mul(float4(input.Position, 1.0), world), ViewProj);
    output.Color = Color;
    output.TexCoord = float2(input.Position.z * 0.5, input.Position.x * 0.5);
    return output;
}
