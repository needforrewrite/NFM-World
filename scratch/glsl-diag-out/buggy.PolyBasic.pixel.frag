
#version 410
#ifdef GL_ARB_shading_language_420pack
#extension GL_ARB_shading_language_420pack : require
#endif

uniform mat4 _LightViewProj0;
uniform mat4 _LightViewProj1;
uniform mat4 _LightViewProj2;
uniform float _DepthBias;
uniform float _NumCascades;
uniform vec3 _LightDirection;
uniform mat4 _View;
uniform mat4 _Projection;
uniform mat4 _ViewProj;
uniform vec3 _SnapColor;
uniform uint _IsFullbright;
uniform uint _UseBaseColor;
uniform vec3 _BaseColor;
uniform vec3 _FogColor;
uniform float _FogDistance;
uniform float _g1_FogLogDensity;
uniform vec2 _g1_EnvironmentLight;
uniform vec3 _g1_CameraPosition;
uniform float _g1_Alpha;
uniform uint _g1_Expand;
uniform float _g1_RandomFloat;
uniform float _g1_Darken;




uniform sampler2D ShadowMap0;
uniform sampler2D ShadowMap1;
uniform sampler2D ShadowMap2;

layout(location = 0) in vec4 input_Color;
layout(location = 1) in vec4 input_WorldPos;
layout(location = 2) in float input_GetsShadowed;
layout(location = 3) in vec3 input_NormalWorld;
layout(location = 4) in float input_ViewLength;
layout(location = 5) in float input_Lit;
layout(location = 6) in float input_Diffuse;
layout(location = 0) out vec4 _entryPointOutput;

mat4 spvWorkaroundRowMajor(mat4 wrap) { return wrap; }

void main()
{
    vec3 _972;
    if (input_Lit > 0.0)
    {
        bool _947;
        do
        {
            if (_197_NumCascades > 0.0)
            {
                if (abs(dot(input_NormalWorld, _197_LightDirection)) >= 0.0500000007450580596923828125)
                {
                    vec4 _617 = spvWorkaroundRowMajor(_197_LightViewProj0) * input_WorldPos;
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
                        _939 = (texture(ShadowMap0, _980).x < (_660 - (_197_DepthBias + clamp(sqrt(fma(_662, _662, _664 * _664)), 0.0, 0.00999999977648258209228515625)))) ? true : false;
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
                    if (_197_NumCascades > 1.0)
                    {
                        vec4 _700 = spvWorkaroundRowMajor(_197_LightViewProj1) * input_WorldPos;
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
                            _942 = (texture(ShadowMap1, _991).x < (_743 - (_197_DepthBias + clamp(sqrt(fma(_745, _745, _747 * _747)), 0.0, 0.00999999977648258209228515625)))) ? true : false;
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
                        if (_197_NumCascades > 2.0)
                        {
                            vec4 _783 = spvWorkaroundRowMajor(_197_LightViewProj2) * input_WorldPos;
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
                                _945 = (texture(ShadowMap2, _1002).x < (_826 - (_197_DepthBias + clamp(sqrt(fma(_828, _828, _830 * _830)), 0.0, 0.00999999977648258209228515625)))) ? true : false;
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
        vec3 _864 = input_Color.xyz * fma(_197_g1_EnvironmentLight.y, ((input_GetsShadowed > 0.0) && _947) ? 0.0 : input_Diffuse, _197_g1_EnvironmentLight.x);
        _972 = min(fma(_864, (_197_SnapColor * 255.0) * vec3(0.00999999977648258209228515625), _864), vec3(1.0));
    }
    else
    {
        _972 = input_Color.xyz;
    }
    float _887 = exp2(max(fma(-_197_FogDistance, 0.5, input_ViewLength) / _197_FogDistance, 0.0) * _197_g1_FogLogDensity);
    _entryPointOutput = vec4(fma(_972, vec3(_887), _197_FogColor * vec3(1.0 - _887)), input_Color.w);
}