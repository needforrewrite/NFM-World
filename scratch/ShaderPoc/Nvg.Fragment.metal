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
    float4 _entryPointOutput [[color(0)]];
};

struct main0_in
{
    float2 input_ftcoord [[user(locn0)]];
};

fragment main0_out main0(main0_in in [[stage_in]], constant _Global& _39 [[buffer(0)]])
{
    main0_out out = {};
    if ((fast::min(1.0, (1.0 - abs((in.input_ftcoord.x * 2.0) - 1.0)) * _39.strokeMult) * fast::min(1.0, in.input_ftcoord.y)) < _39.strokeThr)
    {
        discard_fragment();
    }
    out._entryPointOutput = float4(1.0);
    return out;
}

