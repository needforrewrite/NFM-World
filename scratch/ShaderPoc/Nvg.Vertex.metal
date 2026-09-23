#include <metal_stdlib>
#include <simd/simd.h>

using namespace metal;

struct _Global
{
    float4x4 transformMat;
    float4x4 scissorMat;
    float4x4 paintMat;
    float4 innerCol;
    float4 outerCol;
    float2 scissorExt;
    float2 scissorScale;
    float2 extent;
    float radius;
    float feather;
    float strokeMult;
    float strokeThr;
};

struct main0_out
{
    float2 _entryPointOutput_ftcoord [[user(locn0)]];
    float2 _entryPointOutput_fpos [[user(locn1)]];
    float4 gl_Position [[position]];
};

struct main0_in
{
    float2 pt [[attribute(0)]];
    float2 tex [[attribute(1)]];
};

vertex main0_out main0(main0_in in [[stage_in]], constant _Global& _30 [[buffer(0)]])
{
    main0_out out = {};
    out.gl_Position = float4(in.pt.x, in.pt.y, 0.0, 1.0) * _30.transformMat;
    out._entryPointOutput_ftcoord = in.tex;
    out._entryPointOutput_fpos = in.pt;
    return out;
}

