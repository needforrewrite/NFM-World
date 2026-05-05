// PolyShadow.frag.hlsl — Poly shadow map fragment shader (replaces Poly.fx "CreateShadowMap" technique PS)

struct PSInput
{
    float4 Position : SV_POSITION;
    float  Depth    : TEXCOORD0;
};

float4 main(PSInput input) : SV_TARGET
{
    return float4(input.Depth, input.Depth, input.Depth, 1.0);
}
