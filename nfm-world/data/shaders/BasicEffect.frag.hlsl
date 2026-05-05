// BasicEffect fragment shader — outputs interpolated vertex color.

struct PSInput
{
    float4 Position : SV_Position;
    float4 Color    : TEXCOORD0;
};

float4 main(PSInput input) : SV_Target0
{
    return input.Color;
}
