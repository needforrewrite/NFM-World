
#version 330
#ifdef GL_ARB_shading_language_420pack
#extension GL_ARB_shading_language_420pack : require
#endif

struct _Global
{
    mat4 LightViewProj0;
    mat4 LightViewProj1;
    mat4 LightViewProj2;
    float DepthBias;
    float NumCascades;
    vec3 LightDirection;
    mat4 View;
    mat4 Projection;
    mat4 ViewProj;
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
};

uniform _Global _360;

layout(location = 0) in vec3 input_Position;
layout(location = 1) in vec3 input_Normal;
layout(location = 2) in vec3 input_Color;
layout(location = 3) in vec3 input_Centroid;
layout(location = 4) in float input_DecalOffset;
layout(location = 5) in mat4 world;
layout(location = 9) in vec4 parameters;
layout(location = 10) in vec4 parameters2;
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
    vec3 _775 = input_Position - ((input_Normal * input_DecalOffset) * 0.100000001490116119384765625);
    vec3 _1056;
    if ((_360.Expand != 0u) == true)
    {
        _1056 = fma(normalize(_775 - input_Centroid), vec3(fma(-fract(sin((input_Centroid.x + _360.RandomFloat) * 12.98980045318603515625) * 43758.546875), 30.0, 15.0), fma(-fract(sin((input_Centroid.y + _360.RandomFloat) * 12.98980045318603515625) * 43758.546875), 30.0, 15.0), fma(-fract(sin((input_Centroid.z + _360.RandomFloat) * 12.98980045318603515625) * 43758.546875), 30.0, 15.0)), _775);
    }
    else
    {
        _1056 = _775;
    }
    vec4 _656 = world * vec4(_1056, 1.0);
    vec4 _665 = spvWorkaroundRowMajor(_360.View) * _656;
    vec3 _679 = mix(input_Color, _360.BaseColor, vec3(float(_360.UseBaseColor != 0u)));
    vec4 _683 = spvWorkaroundRowMajor(_360.Projection) * _665;
    _683.z = fma(0.00999999977648258209228515625, parameters2.x, _683.z + 0.100000001490116119384765625);
    vec3 _1060;
    if (_360.Darken < 1.0)
    {
        float _861 = _679.z;
        float _862 = _679.y;
        vec4 _881 = mix(vec4(_861, _862, -1.0, 0.666666686534881591796875), vec4(_862, _861, 0.0, -0.3333333432674407958984375), vec4(step(_861, _862)));
        float _885 = _679.x;
        float _886 = _881.x;
        vec4 _904 = mix(vec4(_886, _881.yw, _885), vec4(_885, _881.yz, _886), vec4(step(_886, _885)));
        float _906 = _904.x;
        float _908 = _904.w;
        float _910 = _904.y;
        float _912 = _906 - min(_908, _910);
        float _932 = _912 / (_906 + 1.0000000133514319600180897396058e-10);
        vec3 _1057;
        if (_906 > _360.Darken)
        {
            _1057 = mix(vec3(1.0), clamp(abs((fract(vec3(abs(_904.z + ((_908 - _910) / fma(6.0, _912, 1.0000000133514319600180897396058e-10))), _932, _906).xxx + vec3(1.0, 0.666666686534881591796875, 0.3333333432674407958984375)) * 6.0) - vec3(3.0)) - vec3(1.0), vec3(0.0), vec3(1.0)), vec3(_932)) * _360.Darken;
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
    vec3 _714 = normalize((world * vec4(input_Normal, 0.0)).xyz);
    float _974 = dot(_714, _360.LightDirection);
    float _1058;
    if (sign(_974) == sign(dot(_714, (world * vec4(input_Centroid, 1.0)).xyz - _360.CameraPosition)))
    {
        _1058 = abs(_974);
    }
    else
    {
        _1058 = 0.0;
    }
    gl_Position = _683;
    _entryPointOutput_Color = vec4(_1060, min(parameters.y, _360.Alpha));
    _entryPointOutput_WorldPos = _656;
    _entryPointOutput_GetsShadowed = float(parameters.x > 0.0);
    _entryPointOutput_NormalWorld = _714;
    _entryPointOutput_ViewLength = length(_665);
    _entryPointOutput_Lit = float(((_360.IsFullbright != 0u) == false) && ((parameters.z > 0.0) == false));
    _entryPointOutput_Diffuse = _1058;
}