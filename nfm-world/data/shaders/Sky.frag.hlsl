// Sky.frag.hlsl — Sky fragment shader (replaces Sky.fx "Fullbright" technique PS)

struct PSInput
{
    float4 Position : SV_POSITION;
    float4 Color    : COLOR0;
};

float4 main(PSInput input) : SV_TARGET
{
    return input.Color;
}
