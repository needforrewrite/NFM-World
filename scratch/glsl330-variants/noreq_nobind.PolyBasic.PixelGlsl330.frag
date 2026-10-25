
#version 330 core

layout(std140) uniform _Global
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
} _197;

uniform sampler2D ShadowMap0;
uniform sampler2D ShadowMap1;
uniform sampler2D ShadowMap2;

in vec4 varying_0;
in vec4 varying_1;
in float varying_2;
in vec3 varying_3;
in float varying_4;
in float varying_5;
in float varying_6;
layout(location = 0) out vec4 _entryPointOutput;

mat4 spvWorkaroundRowMajor(mat4 wrap) { return wrap; }

void main()
{
    vec3 _972;
    if (varying_5 > 0.0)
    {
        bool _947;
        do
        {
            if (_197.NumCascades > 0.0)
            {
                if (abs(dot(varying_3, _197.LightDirection)) >= 0.0500000007450580596923828125)
                {
                    vec4 _617 = spvWorkaroundRowMajor(_197.LightViewProj0) * varying_1;
                    float _622 = _617.w;
                    vec2 _625 = ((_617.xy * 0.5) / vec2(_622)) + vec2(0.5);
                    float _628 = 1.0 - _625.y;
                    vec2 _980 = _625;
                    _980.y = _628;
                    float _631 = _625.x;
                    float _646 = _617.z;
                    bool _648 = ((((_631 >= 0.0) && (_631 <= 1.0)) && (_628 >= 0.0)) && (_628 <= 1.0)) && (_646 > 0.0);
                    bool _939;
                    if (_648)
                    {
                        float _660 = _646 / _622;
                        float _662 = dFdx(_660);
                        float _664 = dFdy(_660);
                        _939 = (texture(ShadowMap0, _980).x < (_660 - (_197.DepthBias + clamp(sqrt(_662 * _662 + (_664 * _664)), 0.0, 0.00999999977648258209228515625)))) ? true : false;
                    }
                    else
                    {
                        _939 = false;
                    }
                    if (_648 ? true : false)
                    {
                        _947 = _939;
                        break;
                    }
                    if (_197.NumCascades > 1.0)
                    {
                        vec4 _700 = spvWorkaroundRowMajor(_197.LightViewProj1) * varying_1;
                        float _705 = _700.w;
                        vec2 _708 = ((_700.xy * 0.5) / vec2(_705)) + vec2(0.5);
                        float _711 = 1.0 - _708.y;
                        vec2 _991 = _708;
                        _991.y = _711;
                        float _714 = _708.x;
                        float _729 = _700.z;
                        bool _731 = ((((_714 >= 0.0) && (_714 <= 1.0)) && (_711 >= 0.0)) && (_711 <= 1.0)) && (_729 > 0.0);
                        bool _942;
                        if (_731)
                        {
                            float _743 = _729 / _705;
                            float _745 = dFdx(_743);
                            float _747 = dFdy(_743);
                            _942 = (texture(ShadowMap1, _991).x < (_743 - (_197.DepthBias + clamp(sqrt(_745 * _745 + (_747 * _747)), 0.0, 0.00999999977648258209228515625)))) ? true : false;
                        }
                        else
                        {
                            _942 = false;
                        }
                        if (_731 ? true : false)
                        {
                            _947 = _942;
                            break;
                        }
                        if (_197.NumCascades > 2.0)
                        {
                            vec4 _783 = spvWorkaroundRowMajor(_197.LightViewProj2) * varying_1;
                            float _788 = _783.w;
                            vec2 _791 = ((_783.xy * 0.5) / vec2(_788)) + vec2(0.5);
                            float _794 = 1.0 - _791.y;
                            vec2 _1002 = _791;
                            _1002.y = _794;
                            float _797 = _791.x;
                            float _812 = _783.z;
                            bool _814 = ((((_797 >= 0.0) && (_797 <= 1.0)) && (_794 >= 0.0)) && (_794 <= 1.0)) && (_812 > 0.0);
                            bool _945;
                            if (_814)
                            {
                                float _826 = _812 / _788;
                                float _828 = dFdx(_826);
                                float _830 = dFdy(_826);
                                _945 = (texture(ShadowMap2, _1002).x < (_826 - (_197.DepthBias + clamp(sqrt(_828 * _828 + (_830 * _830)), 0.0, 0.00999999977648258209228515625)))) ? true : false;
                            }
                            else
                            {
                                _945 = false;
                            }
                            if (_814 ? true : false)
                            {
                                _947 = _945;
                                break;
                            }
                        }
                    }
                }
            }
            _947 = false;
            break;
        } while(false);
        vec3 _864 = varying_0.xyz * (_197.EnvironmentLight.y * (((varying_2 > 0.0) && _947) ? 0.0 : varying_6) + _197.EnvironmentLight.x);
        _972 = min(_864 * ((_197.SnapColor * 255.0) * vec3(0.00999999977648258209228515625)) + _864, vec3(1.0));
    }
    else
    {
        _972 = varying_0.xyz;
    }
    float _887 = exp2(max(((-_197.FogDistance) * 0.5 + varying_4) / _197.FogDistance, 0.0) * _197.FogLogDensity);
    _entryPointOutput = vec4(_972 * vec3(_887) + (_197.FogColor * vec3(1.0 - _887)), varying_0.w);
}