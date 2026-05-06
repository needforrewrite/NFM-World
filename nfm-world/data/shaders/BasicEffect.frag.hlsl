// BasicEffect fragment shader — outputs interpolated vertex color.

#include "SDLGPU.hlsli"

struct PSInput
{
    float4 Position : SV_Position;
    float4 Color    : ATTRIBUTE(0);
};

float4 main(PSInput input) : SV_Target0
{
    return input.Color;
}
