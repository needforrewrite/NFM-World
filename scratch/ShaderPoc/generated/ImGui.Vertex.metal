#include <metal_stdlib>
#include <simd/simd.h>

using namespace metal;

struct _Global
{
    float4x4 Projection;
};

struct main0_out
{
    float4 _entryPointOutput_Color [[user(locn0)]];
    float2 _entryPointOutput_TexCoord [[user(locn1)]];
    float4 gl_Position [[position]];
};

struct main0_in
{
    float2 input_Position [[attribute(0)]];
    float2 input_TexCoord [[attribute(1)]];
    float4 input_Color [[attribute(2)]];
};

vertex main0_out main0(main0_in in [[stage_in]], constant _Global& _24 [[buffer(0)]])
{
    main0_out out = {};
    out.gl_Position = float4(in.input_Position, 0.0, 1.0) * _24.Projection;
    out._entryPointOutput_Color = in.input_Color;
    out._entryPointOutput_TexCoord = in.input_TexCoord;
    return out;
}

