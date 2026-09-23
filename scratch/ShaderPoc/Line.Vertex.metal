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
    float ChargedBlinkAmount;
    float HalfThickness;
    float2 Resolution;
    float DistantOutlineDistanceFalloffWithCutoffMask;
    float DistantOutlineClassicCutoffMask;
    float DistantOutlineDistanceFalloffMask;
    float OutlineClassicCutoffDistance;
    float OutlineFalloffStartDistance;
    float OutlineFalloffCutoffDistance;
    float OutlineFalloffLinearFadeStartDistance;
    float OutlineFalloffLinearFadeStartThickness;
    float OutlineFalloffInverseLinearFadeLength;
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
    float3 input_PositionA [[attribute(0)]];
    float3 input_PositionB [[attribute(1)]];
    float input_Side [[attribute(2)]];
    float3 input_Normal [[attribute(3)]];
    float3 input_Color [[attribute(4)]];
    float3 input_Centroid [[attribute(5)]];
    float input_DecalOffset [[attribute(6)]];
    float4 world_0 [[attribute(7)]];
    float4 world_1 [[attribute(8)]];
    float4 world_2 [[attribute(9)]];
    float4 world_3 [[attribute(10)]];
    float4 parameters [[attribute(11)]];
    float4 parameters2 [[attribute(12)]];
};

vertex main0_out main0(main0_in in [[stage_in]], constant _Global& _325 [[buffer(0)]])
{
    main0_out out = {};
    float4x4 world = {};
    world[0] = in.world_0;
    world[1] = in.world_1;
    world[2] = in.world_2;
    world[3] = in.world_3;
    bool _1144 = in.parameters.w > 0.0;
    float4 _900 = world * float4(in.input_Centroid, 1.0);
    float4 _909 = float4(_900.xyz, 1.0) * _325.View;
    float _910 = _909.z;
    float _911 = -_910;
    float _1171 = _325.HalfThickness * fast::min(1.0, fast::max(_325.OutlineFalloffStartDistance, 9.9999997473787516355514526367188e-05) / fast::max(_911, 9.9999997473787516355514526367188e-05));
    float3 _1238 = select(in.input_PositionA, in.input_PositionB, bool3(abs(in.input_Side) > 1.5)) - ((in.input_Normal * in.input_DecalOffset) * 0.100000001490116119384765625);
    float3 _1525;
    if (_325.Expand != 0u)
    {
        _1525 = _1238 + (fast::normalize(_1238 - in.input_Centroid) * float3(15.0 - (fract(sin((in.input_Centroid.x + _325.RandomFloat) * 12.98980045318603515625) * 43758.546875) * 30.0), 15.0 - (fract(sin((in.input_Centroid.y + _325.RandomFloat) * 12.98980045318603515625) * 43758.546875) * 30.0), 15.0 - (fract(sin((in.input_Centroid.z + _325.RandomFloat) * 12.98980045318603515625) * 43758.546875) * 30.0)));
    }
    else
    {
        _1525 = _1238;
    }
    float4 _955 = world * float4(_1525, 1.0);
    float4 _964 = _955 * _325.View;
    float4 _978 = (world * float4(in.input_PositionA, 1.0)) * _325.ViewProj;
    float4 _989 = (world * float4(in.input_PositionB, 1.0)) * _325.ViewProj;
    float2 _1010 = ((_325.Resolution * _989.xy) / float2(_989.w)) - ((_325.Resolution * _978.xy) / float2(_978.w));
    float2 _1019 = select(fast::normalize(_1010), float2(1.0, 0.0), bool2(dot(_1010, _1010) < 9.9999997473787516355514526367188e-05));
    float4 _1029 = _964 * _325.Projection;
    float3 _1049 = mix(in.input_Color, _325.BaseColor, float3(float(_325.UseBaseColor != 0u)));
    float4 _1058 = _1029 + float4(((((float2(-_1019.y, _1019.x) * mix(_325.HalfThickness, mix(_1171, mix(_1171, _325.OutlineFalloffLinearFadeStartThickness * fast::clamp((_325.OutlineFalloffCutoffDistance + _910) * _325.OutlineFalloffInverseLinearFadeLength, 0.0, 1.0), fast::clamp(sign(_911 - _325.OutlineFalloffLinearFadeStartDistance), 0.0, 1.0)), _325.DistantOutlineDistanceFalloffWithCutoffMask), _325.DistantOutlineDistanceFalloffMask + _325.DistantOutlineDistanceFalloffWithCutoffMask)) * sign(in.input_Side)) / _325.Resolution) * 2.0) * _1029.w, 0.0, 0.0);
    _1058.z = _1058.z + (0.00999999977648258209228515625 * in.parameters2.x);
    float3 _1527;
    if (_325.Darken < 1.0)
    {
        float _1324 = _1049.z;
        float _1325 = _1049.y;
        float4 _1344 = mix(float4(_1324, _1325, -1.0, 0.666666686534881591796875), float4(_1325, _1324, 0.0, -0.3333333432674407958984375), float4(step(_1324, _1325)));
        float _1348 = _1049.x;
        float _1349 = _1344.x;
        float4 _1367 = mix(float4(_1349, _1344.yw, _1348), float4(_1348, _1344.yz, _1349), float4(step(_1349, _1348)));
        float _1369 = _1367.x;
        float _1371 = _1367.w;
        float _1373 = _1367.y;
        float _1375 = _1369 - fast::min(_1371, _1373);
        float _1395 = _1375 / (_1369 + 1.0000000133514319600180897396058e-10);
        float3 _1526;
        if (_1369 > _325.Darken)
        {
            _1526 = mix(float3(1.0), fast::clamp(abs((fract(float3(abs(_1367.z + ((_1371 - _1373) / ((6.0 * _1375) + 1.0000000133514319600180897396058e-10))), _1395, _1369).xxx + float3(1.0, 0.666666686534881591796875, 0.3333333432674407958984375)) * 6.0) - float3(3.0)) - float3(1.0), float3(0.0), float3(1.0)), float3(_1395)) * _325.Darken;
        }
        else
        {
            _1526 = _1049;
        }
        _1527 = _1526;
    }
    else
    {
        _1527 = _1049;
    }
    float3 _1530;
    if (_1144)
    {
        _1530 = fast::min(_1527 * 1.60000002384185791015625, float3(1.0));
    }
    else
    {
        _1530 = _1527;
    }
    float3 _1099 = fast::normalize((world * float4(in.input_Normal, 0.0)).xyz);
    float _1437 = dot(_1099, _325.LightDirection);
    float _1528;
    if (sign(_1437) == sign(dot(_1099, _900.xyz - float3(_325.CameraPosition))))
    {
        _1528 = abs(_1437);
    }
    else
    {
        _1528 = 0.0;
    }
    out.gl_Position = mix(_1058, float4(2.0, 2.0, 0.0, 1.0), float4(fast::max(_325.DistantOutlineClassicCutoffMask * fast::clamp(sign(_911 - _325.OutlineClassicCutoffDistance), 0.0, 1.0), _325.DistantOutlineDistanceFalloffWithCutoffMask * fast::clamp(sign(_911 - _325.OutlineFalloffCutoffDistance), 0.0, 1.0))));
    out._entryPointOutput_Color = float4(_1530, fast::min(in.parameters.y, _325.Alpha));
    out._entryPointOutput_WorldPos = _955;
    out._entryPointOutput_GetsShadowed = float(in.parameters.x > 0.0);
    out._entryPointOutput_NormalWorld = _1099;
    out._entryPointOutput_ViewLength = length(_964);
    out._entryPointOutput_Lit = float(((_325.IsFullbright == 0u) && (isunordered(in.parameters.z, 0.0) || in.parameters.z <= 0.0)) && (!_1144));
    out._entryPointOutput_Diffuse = _1528;
    return out;
}

