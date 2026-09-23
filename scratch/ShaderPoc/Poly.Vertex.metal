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
    float4x4 View;
    float4x4 Projection;
    float4x4 ViewProj;
    packed_float3 SnapColor;
    uint IsFullbright;
    uint UseBaseColor;
    float3 BaseColor;
    packed_float3 FogColor;
    float FogDistance;
    float FogLogDensity;
    float2 EnvironmentLight;
    packed_float3 CameraPosition;
    float Alpha;
    uint Expand;
    float RandomFloat;
    float Darken;
};

struct main0_out
{
    float4 _entryPointOutput_Color [[user(locn0)]];
    float4 _entryPointOutput_WorldPos [[user(locn1)]];
    float _entryPointOutput_GetsShadowed [[user(locn2)]];
    float3 _entryPointOutput_NormalWorld [[user(locn3)]];
    float _entryPointOutput_ViewLength [[user(locn4)]];
    float _entryPointOutput_Lit [[user(locn5)]];
    float _entryPointOutput_Diffuse [[user(locn6)]];
    float4 gl_Position [[position]];
};

struct main0_in
{
    float3 input_Position [[attribute(0)]];
    float3 input_Normal [[attribute(1)]];
    float3 input_Color [[attribute(2)]];
    float3 input_Centroid [[attribute(3)]];
    float input_DecalOffset [[attribute(4)]];
    float4 world_0 [[attribute(5)]];
    float4 world_1 [[attribute(6)]];
    float4 world_2 [[attribute(7)]];
    float4 world_3 [[attribute(8)]];
    float4 parameters [[attribute(9)]];
    float4 parameters2 [[attribute(10)]];
};

vertex main0_out main0(main0_in in [[stage_in]], constant _Global& _360 [[buffer(0)]])
{
    main0_out out = {};
    float4x4 world = {};
    world[0] = in.world_0;
    world[1] = in.world_1;
    world[2] = in.world_2;
    world[3] = in.world_3;
    float3 _775 = in.input_Position - ((in.input_Normal * in.input_DecalOffset) * 0.100000001490116119384765625);
    float3 _1056;
    if (_360.Expand != 0u)
    {
        _1056 = _775 + (fast::normalize(_775 - in.input_Centroid) * float3(15.0 - (fract(sin((in.input_Centroid.x + _360.RandomFloat) * 12.98980045318603515625) * 43758.546875) * 30.0), 15.0 - (fract(sin((in.input_Centroid.y + _360.RandomFloat) * 12.98980045318603515625) * 43758.546875) * 30.0), 15.0 - (fract(sin((in.input_Centroid.z + _360.RandomFloat) * 12.98980045318603515625) * 43758.546875) * 30.0)));
    }
    else
    {
        _1056 = _775;
    }
    float4 _656 = world * float4(_1056, 1.0);
    float4 _665 = _656 * _360.View;
    float3 _679 = mix(in.input_Color, _360.BaseColor, float3(float(_360.UseBaseColor != 0u)));
    float4 _683 = _665 * _360.Projection;
    _683.z = (_683.z + 0.100000001490116119384765625) + (0.00999999977648258209228515625 * in.parameters2.x);
    float3 _1060;
    if (_360.Darken < 1.0)
    {
        float _861 = _679.z;
        float _862 = _679.y;
        float4 _881 = mix(float4(_861, _862, -1.0, 0.666666686534881591796875), float4(_862, _861, 0.0, -0.3333333432674407958984375), float4(step(_861, _862)));
        float _885 = _679.x;
        float _886 = _881.x;
        float4 _904 = mix(float4(_886, _881.yw, _885), float4(_885, _881.yz, _886), float4(step(_886, _885)));
        float _906 = _904.x;
        float _908 = _904.w;
        float _910 = _904.y;
        float _912 = _906 - fast::min(_908, _910);
        float _932 = _912 / (_906 + 1.0000000133514319600180897396058e-10);
        float3 _1057;
        if (_906 > _360.Darken)
        {
            _1057 = mix(float3(1.0), fast::clamp(abs((fract(float3(abs(_904.z + ((_908 - _910) / ((6.0 * _912) + 1.0000000133514319600180897396058e-10))), _932, _906).xxx + float3(1.0, 0.666666686534881591796875, 0.3333333432674407958984375)) * 6.0) - float3(3.0)) - float3(1.0), float3(0.0), float3(1.0)), float3(_932)) * _360.Darken;
        }
        else
        {
            _1057 = _679;
        }
        _1060 = _1057;
    }
    else
    {
        _1060 = _679;
    }
    float3 _714 = fast::normalize((world * float4(in.input_Normal, 0.0)).xyz);
    float _974 = dot(_714, _360.LightDirection);
    float _1058;
    if (sign(_974) == sign(dot(_714, (world * float4(in.input_Centroid, 1.0)).xyz - float3(_360.CameraPosition))))
    {
        _1058 = abs(_974);
    }
    else
    {
        _1058 = 0.0;
    }
    out.gl_Position = _683;
    out._entryPointOutput_Color = float4(_1060, fast::min(in.parameters.y, _360.Alpha));
    out._entryPointOutput_WorldPos = _656;
    out._entryPointOutput_GetsShadowed = float(in.parameters.x > 0.0);
    out._entryPointOutput_NormalWorld = _714;
    out._entryPointOutput_ViewLength = length(_665);
    out._entryPointOutput_Lit = float((_360.IsFullbright == 0u) && (isunordered(in.parameters.z, 0.0) || in.parameters.z <= 0.0));
    out._entryPointOutput_Diffuse = _1058;
    return out;
}

