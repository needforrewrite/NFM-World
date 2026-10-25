#include <metal_stdlib>
#include <simd/simd.h>

using namespace metal;

struct _Global
{
    float4x4 WorldViewProj;
};

struct main0_out
{
    float4 _entryPointOutput_Color [[user(locn0)]];
    float4 gl_Position [[position]];
};

struct main0_in
{
    float4 Position [[attribute(0)]];
    float4 Color [[attribute(1)]];
};

vertex main0_out main0(main0_in in [[stage_in]], constant _Global& _23 [[buffer(0)]])
{
    main0_out out = {};
    out.gl_Position = in.Position * _23.WorldViewProj;
    out._entryPointOutput_Color = in.Color;
    return out;
}

