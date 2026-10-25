#include <metal_stdlib>
#include <simd/simd.h>

using namespace metal;

struct _Global
{
    float4x4 LightViewProj0;
    float4x4 LightViewProj1;
    float4x4 LightViewProj2;
    float DepthBias;
    float NumCascades;
    float3 LightDirection;
    float4x4 WorldView;
    float4x4 WorldViewProj;
    packed_float3 FogColor;
    float FogDistance;
    float FogLogDensity;
};

struct main0_out
{
    float4 _entryPointOutput_Color [[user(locn0)]];
    float4 _entryPointOutput_WorldPos [[user(locn1)]];
    float4 gl_Position [[position]];
};

struct main0_in
{
    float4 Position [[attribute(0)]];
    float3 Color [[attribute(1)]];
};

vertex main0_out main0(main0_in in [[stage_in]], constant _Global& _67 [[buffer(0)]])
{
    main0_out out = {};
    float _216 = exp2(fast::max((length((in.Position * _67.WorldView).xyz) - (_67.FogDistance * 0.5)) / _67.FogDistance, 0.0) * _67.FogLogDensity);
    out.gl_Position = in.Position * _67.WorldViewProj;
    out._entryPointOutput_Color = float4((in.Color * float3(_216)) + (float3(_67.FogColor) * float3(1.0 - _216)), 1.0);
    out._entryPointOutput_WorldPos = in.Position;
    return out;
}

