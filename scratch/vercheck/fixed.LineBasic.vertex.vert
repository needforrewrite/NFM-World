
#version 420
#ifdef GL_ARB_shading_language_420pack
#extension GL_ARB_shading_language_420pack : require
#endif

uniform mat4 _325_LightViewProj0;
uniform mat4 _325_LightViewProj1;
uniform mat4 _325_LightViewProj2;
uniform float _325_DepthBias;
uniform float _325_NumCascades;
uniform vec3 _325_LightDirection;
uniform mat4 _325_View;
uniform mat4 _325_Projection;
uniform mat4 _325_ViewProj;
uniform vec3 _325_SnapColor;
uniform uint _325_IsFullbright;
uniform uint _325_UseBaseColor;
uniform vec3 _325_BaseColor;
uniform vec3 _325_FogColor;
uniform float _325_FogDistance;
uniform float _325_g1_FogLogDensity;
uniform vec2 _325_g1_EnvironmentLight;
uniform vec3 _325_g1_CameraPosition;
uniform float _325_g1_Alpha;
uniform uint _325_g1_Expand;
uniform float _325_g1_RandomFloat;
uniform float _325_g1_Darken;
uniform float _325_g1_ChargedBlinkAmount;
uniform float _325_g1_HalfThickness;
uniform vec2 _325_g1_Resolution;
uniform float _325_g1_DistantOutlineDistanceFalloffWithCutoffMask;
uniform float _325_g1_DistantOutlineClassicCutoffMask;
uniform float _325_g1_DistantOutlineDistanceFalloffMask;
uniform float _325_g1_OutlineClassicCutoffDistance;
uniform float _325_g2_OutlineFalloffStartDistance;
uniform float _325_g2_OutlineFalloffCutoffDistance;
uniform float _325_g2_OutlineFalloffLinearFadeStartDistance;
uniform float _325_g2_OutlineFalloffLinearFadeStartThickness;
uniform float _325_g2_OutlineFalloffInverseLinearFadeLength;




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
layout(location = 0) out vec4 _entryPointOutput_Color;
layout(location = 1) out vec4 _entryPointOutput_WorldPos;
layout(location = 2) out float _entryPointOutput_GetsShadowed;
layout(location = 3) out vec3 _entryPointOutput_NormalWorld;
layout(location = 4) out float _entryPointOutput_ViewLength;
layout(location = 5) out float _entryPointOutput_Lit;
layout(location = 6) out float _entryPointOutput_Diffuse;

mat4 spvWorkaroundRowMajor(mat4 wrap) { return wrap; }

void main()
{
    bool _1144 = parameters.w > 0.0;
    vec4 _900 = world * vec4(input_Centroid, 1.0);
    float _911 = -(spvWorkaroundRowMajor(_325_View) * vec4(_900.xyz, 1.0)).z;
    float _1171 = _325_g1_HalfThickness * min(1.0, max(_325_g2_OutlineFalloffStartDistance, 9.9999997473787516355514526367188e-05) / max(_911, 9.9999997473787516355514526367188e-05));
    bvec3 _924 = bvec3(abs(input_Side) > 1.5);
    vec3 _1238 = vec3(_924.x ? input_PositionB.x : input_PositionA.x, _924.y ? input_PositionB.y : input_PositionA.y, _924.z ? input_PositionB.z : input_PositionA.z) - ((input_Normal * input_DecalOffset) * 0.100000001490116119384765625);
    vec3 _1525;
    if ((_325_g1_Expand != 0u) == true)
    {
        _1525 = fma(normalize(_1238 - input_Centroid), vec3(fma(-fract(sin((input_Centroid.x + _325_g1_RandomFloat) * 12.98980045318603515625) * 43758.546875), 30.0, 15.0), fma(-fract(sin((input_Centroid.y + _325_g1_RandomFloat) * 12.98980045318603515625) * 43758.546875), 30.0, 15.0), fma(-fract(sin((input_Centroid.z + _325_g1_RandomFloat) * 12.98980045318603515625) * 43758.546875), 30.0, 15.0)), _1238);
    }
    else
    {
        _1525 = _1238;
    }
    vec4 _955 = world * vec4(_1525, 1.0);
    vec4 _964 = spvWorkaroundRowMajor(_325_View) * _955;
    vec4 _978 = spvWorkaroundRowMajor(_325_ViewProj) * (world * vec4(input_PositionA, 1.0));
    vec4 _989 = spvWorkaroundRowMajor(_325_ViewProj) * (world * vec4(input_PositionB, 1.0));
    vec2 _1010 = ((_325_g1_Resolution * _989.xy) / vec2(_989.w)) - ((_325_g1_Resolution * _978.xy) / vec2(_978.w));
    vec2 _1017 = normalize(_1010);
    bvec2 _1018 = bvec2(dot(_1010, _1010) < 9.9999997473787516355514526367188e-05);
    vec2 _1019 = vec2(_1018.x ? vec2(1.0, 0.0).x : _1017.x, _1018.y ? vec2(1.0, 0.0).y : _1017.y);
    vec4 _1029 = spvWorkaroundRowMajor(_325_Projection) * _964;
    vec3 _1049 = mix(input_Color, _325_BaseColor, vec3(float(_325_UseBaseColor != 0u)));
    vec4 _1058 = _1029 + vec4(((((vec2(-_1019.y, _1019.x) * mix(_325_g1_HalfThickness, mix(_1171, mix(_1171, _325_g2_OutlineFalloffLinearFadeStartThickness * clamp((_325_g2_OutlineFalloffCutoffDistance - _911) * _325_g2_OutlineFalloffInverseLinearFadeLength, 0.0, 1.0), clamp(sign(_911 - _325_g2_OutlineFalloffLinearFadeStartDistance), 0.0, 1.0)), _325_g1_DistantOutlineDistanceFalloffWithCutoffMask), _325_g1_DistantOutlineDistanceFalloffMask + _325_g1_DistantOutlineDistanceFalloffWithCutoffMask)) * sign(input_Side)) / _325_g1_Resolution) * 2.0) * _1029.w, 0.0, 0.0);
    _1058.z = fma(0.00999999977648258209228515625, parameters2.x, _1058.z);
    vec3 _1527;
    if (_325_g1_Darken < 1.0)
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
        if (_1369 > _325_g1_Darken)
        {
            _1526 = mix(vec3(1.0), clamp(abs((fract(vec3(abs(_1367.z + ((_1371 - _1373) / fma(6.0, _1375, 1.0000000133514319600180897396058e-10))), _1395, _1369).xxx + vec3(1.0, 0.666666686534881591796875, 0.3333333432674407958984375)) * 6.0) - vec3(3.0)) - vec3(1.0), vec3(0.0), vec3(1.0)), vec3(_1395)) * _325_g1_Darken;
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
    float _1437 = dot(_1099, _325_LightDirection);
    float _1528;
    if (sign(_1437) == sign(dot(_1099, _900.xyz - _325_g1_CameraPosition)))
    {
        _1528 = abs(_1437);
    }
    else
    {
        _1528 = 0.0;
    }
    gl_Position = mix(_1058, vec4(2.0, 2.0, 0.0, 1.0), vec4(max(_325_g1_DistantOutlineClassicCutoffMask * clamp(sign(_911 - _325_g1_OutlineClassicCutoffDistance), 0.0, 1.0), _325_g1_DistantOutlineDistanceFalloffWithCutoffMask * clamp(sign(_911 - _325_g2_OutlineFalloffCutoffDistance), 0.0, 1.0))));
    _entryPointOutput_Color = vec4(_1530, min(parameters.y, _325_g1_Alpha));
    _entryPointOutput_WorldPos = _955;
    _entryPointOutput_GetsShadowed = float(parameters.x > 0.0);
    _entryPointOutput_NormalWorld = _1099;
    _entryPointOutput_ViewLength = length(_964);
    _entryPointOutput_Lit = float((((_325_IsFullbright != 0u) == false) && ((parameters.z > 0.0) == false)) && (_1144 == false));
    _entryPointOutput_Diffuse = _1528;
}