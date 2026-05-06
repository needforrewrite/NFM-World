// PolyShadow.frag.hlsl — Poly shadow map fragment shader (replaces Poly.fx "CreateShadowMap" technique PS)

#include "SDLGPU.hlsli"

struct PSInput
{
    float4 Position : SV_POSITION;
    float  Depth    : ATTRIBUTE(0);
};

float4 main(PSInput input) : SV_TARGET
{
    return float4(input.Depth, input.Depth, input.Depth, 1.0);
}
