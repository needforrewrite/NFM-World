
#version 330
#ifdef GL_ARB_shading_language_420pack
#extension GL_ARB_shading_language_420pack : require
#endif

layout(binding = 0, std140) uniform _Global
{
    layout(row_major) mat4 LightViewProj0;
    layout(row_major) mat4 LightViewProj1;
    layout(row_major) mat4 LightViewProj2;
    float DepthBias;
    float NumCascades;
    vec3 LightDirection;
    layout(row_major) mat4 View;
    layout(row_major) mat4 Projection;
    layout(row_major) mat4 ViewProj;
    vec3 SnapColor;
    uint IsFullbright;
    uint UseBaseColor;
    vec3 BaseColor;
    vec3 FogColor;
    float FogDistance;
    float FogLogDensity;
    vec2 EnvironmentLight;
    vec3 CameraPosition;
    float Alpha;
    uint Expand;
    float RandomFloat;
    float Darken;
    float ChargedBlinkAmount;
    float HalfThickness;
    vec2 Resolution;
    float DistantOutlineDistanceFalloffWithCutoffMask;
    float DistantOutlineClassicCutoffMask;
    float DistantOutlineDistanceFalloffMask;
    float OutlineClassicCutoffDistance;
    float OutlineFalloffStartDistance;
    float OutlineFalloffCutoffDistance;
    float OutlineFalloffLinearFadeStartDistance;
    float OutlineFalloffLinearFadeStartThickness;
    float OutlineFalloffInverseLinearFadeLength;
} _325;

layout(location = 0) in vec3 input_PositionA;
layout(location = 1) in vec3 input_PositionB;
layout(location = 2) in float input_Side;
layout(location = 3) in vec3 input_Normal;
layout(location = 4) in vec3 input_Color;
layout(location = 5) in vec3 input_Centroid;
layout(location = 6) in float input_DecalOffset;
layout(location = 7) in mat4 world;
layout(location = 11) in vec4 parameters;
layout(location = 12) in vec4 parameters2;
out vec4 varying_0;
out vec4 varying_1;
out float varying_2;
out vec3 varying_3;
out float varying_4;
out float varying_5;
out float varying_6;

mat4 spvWorkaroundRowMajor(mat4 wrap) { return wrap; }

void main()
{
    bool _1144 = parameters.w > 0.0;
    vec4 _900 = world * vec4(input_Centroid, 1.0);
    float _911 = -(spvWorkaroundRowMajor(_325.View) * vec4(_900.xyz, 1.0)).z;
    float _1171 = _325.HalfThickness * min(1.0, max(_325.OutlineFalloffStartDistance, 9.9999997473787516355514526367188e-05) / max(_911, 9.9999997473787516355514526367188e-05));
    bvec3 _924 = bvec3(abs(input_Side) > 1.5);
    vec3 _1238 = vec3(_924.x ? input_PositionB.x : input_PositionA.x, _924.y ? input_PositionB.y : input_PositionA.y, _924.z ? input_PositionB.z : input_PositionA.z) - ((input_Normal * input_DecalOffset) * 0.100000001490116119384765625);
    vec3 _1525;
    if ((_325.Expand != 0u) == true)
    {
        _1525 = normalize(_1238 - input_Centroid) * vec3((-fract(sin((input_Centroid.x + _325.RandomFloat) * 12.98980045318603515625) * 43758.546875)) * 30.0 + 15.0, (-fract(sin((input_Centroid.y + _325.RandomFloat) * 12.98980045318603515625) * 43758.546875)) * 30.0 + 15.0, (-fract(sin((input_Centroid.z + _325.RandomFloat) * 12.98980045318603515625) * 43758.546875)) * 30.0 + 15.0) + _1238;
    }
    else
    {
        _1525 = _1238;
    }
    vec4 _955 = world * vec4(_1525, 1.0);
    vec4 _964 = spvWorkaroundRowMajor(_325.View) * _955;
    vec4 _978 = spvWorkaroundRowMajor(_325.ViewProj) * (world * vec4(input_PositionA, 1.0));
    vec4 _989 = spvWorkaroundRowMajor(_325.ViewProj) * (world * vec4(input_PositionB, 1.0));
    vec2 _1010 = ((_325.Resolution * _989.xy) / vec2(_989.w)) - ((_325.Resolution * _978.xy) / vec2(_978.w));
    vec2 _1017 = normalize(_1010);
    bvec2 _1018 = bvec2(dot(_1010, _1010) < 9.9999997473787516355514526367188e-05);
    vec2 _1019 = vec2(_1018.x ? vec2(1.0, 0.0).x : _1017.x, _1018.y ? vec2(1.0, 0.0).y : _1017.y);
    vec4 _1029 = spvWorkaroundRowMajor(_325.Projection) * _964;
    vec3 _1049 = mix(input_Color, _325.BaseColor, vec3(float(_325.UseBaseColor != 0u)));
    vec4 _1058 = _1029 + vec4(((((vec2(-_1019.y, _1019.x) * mix(_325.HalfThickness, mix(_1171, mix(_1171, _325.OutlineFalloffLinearFadeStartThickness * clamp((_325.OutlineFalloffCutoffDistance - _911) * _325.OutlineFalloffInverseLinearFadeLength, 0.0, 1.0), clamp(sign(_911 - _325.OutlineFalloffLinearFadeStartDistance), 0.0, 1.0)), _325.DistantOutlineDistanceFalloffWithCutoffMask), _325.DistantOutlineDistanceFalloffMask + _325.DistantOutlineDistanceFalloffWithCutoffMask)) * sign(input_Side)) / _325.Resolution) * 2.0) * _1029.w, 0.0, 0.0);
    _1058.z = 0.00999999977648258209228515625 * parameters2.x + _1058.z;
    vec3 _1527;
    if (_325.Darken < 1.0)
    {
        float _1324 = _1049.z;
        float _1325 = _1049.y;
        vec4 _1344 = mix(vec4(_1324, _1325, -1.0, 0.666666686534881591796875), vec4(_1325, _1324, 0.0, -0.3333333432674407958984375), vec4(step(_1324, _1325)));
        float _1348 = _1049.x;
        float _1349 = _1344.x;
        vec4 _1367 = mix(vec4(_1349, _1344.yw, _1348), vec4(_1348, _1344.yz, _1349), vec4(step(_1349, _1348)));
        float _1369 = _1367.x;
        float _1371 = _1367.w;
        float _1373 = _1367.y;
        float _1375 = _1369 - min(_1371, _1373);
        float _1395 = _1375 / (_1369 + 1.0000000133514319600180897396058e-10);
        vec3 _1526;
        if (_1369 > _325.Darken)
        {
            _1526 = mix(vec3(1.0), clamp(abs((fract(vec3(abs(_1367.z + ((_1371 - _1373) / (6.0 * _1375 + 1.0000000133514319600180897396058e-10))), _1395, _1369).xxx + vec3(1.0, 0.666666686534881591796875, 0.3333333432674407958984375)) * 6.0) - vec3(3.0)) - vec3(1.0), vec3(0.0), vec3(1.0)), vec3(_1395)) * _325.Darken;
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
    vec3 _1530;
    if (_1144 == true)
    {
        _1530 = min(_1527 * 1.60000002384185791015625, vec3(1.0));
    }
    else
    {
        _1530 = _1527;
    }
    vec3 _1099 = normalize((world * vec4(input_Normal, 0.0)).xyz);
    float _1437 = dot(_1099, _325.LightDirection);
    float _1528;
    if (sign(_1437) == sign(dot(_1099, _900.xyz - _325.CameraPosition)))
    {
        _1528 = abs(_1437);
    }
    else
    {
        _1528 = 0.0;
    }
    gl_Position = mix(_1058, vec4(2.0, 2.0, 0.0, 1.0), vec4(max(_325.DistantOutlineClassicCutoffMask * clamp(sign(_911 - _325.OutlineClassicCutoffDistance), 0.0, 1.0), _325.DistantOutlineDistanceFalloffWithCutoffMask * clamp(sign(_911 - _325.OutlineFalloffCutoffDistance), 0.0, 1.0))));
    varying_0 = vec4(_1530, min(parameters.y, _325.Alpha));
    varying_1 = _955;
    varying_2 = float(parameters.x > 0.0);
    varying_3 = _1099;
    varying_4 = length(_964);
    varying_5 = float((((_325.IsFullbright != 0u) == false) && ((parameters.z > 0.0) == false)) && (_1144 == false));
    varying_6 = _1528;
}