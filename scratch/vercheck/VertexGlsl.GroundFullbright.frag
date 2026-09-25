
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
    mat4 WorldView;
    mat4 WorldViewProj;
    vec3 FogColor;
    float FogDistance;
    float FogLogDensity;
};

uniform _Global _166;

uniform sampler2D ShadowMap0;
uniform sampler2D ShadowMap1;
uniform sampler2D ShadowMap2;

layout(location = 0) in vec4 input_Color;
layout(location = 1) in vec4 input_WorldPos;
layout(location = 2) in float input_ViewLength;
layout(location = 0) out vec4 _entryPointOutput;

mat4 spvWorkaroundRowMajor(mat4 wrap) { return wrap; }

void main()
{
    float _427 = exp2(max(fma(-_166.FogDistance, 0.5, input_ViewLength) / _166.FogDistance, 0.0) * _166.FogLogDensity);
    vec3 _443 = fma(input_Color.xyz, vec3(_427), _166.FogColor * vec3(1.0 - _427));
    vec4 _405 = vec4(input_WorldPos.xyz, 1.0);
    bool _821;
    do
    {
        if (_166.NumCascades > 0.0)
        {
            if (abs(_166.LightDirection.y) >= 0.0500000007450580596923828125)
            {
                vec4 _557 = spvWorkaroundRowMajor(_166.LightViewProj0) * _405;
                float _562 = _557.w;
                vec2 _565 = ((_557.xy * 0.5) / vec2(_562)) + vec2(0.5);
                float _568 = 1.0 - _565.y;
                vec2 _838 = _565;
                _838.y = _568;
                float _571 = _565.x;
                float _586 = _557.z;
                bool _588 = ((((_571 >= 0.0) && (_571 <= 1.0)) && (_568 >= 0.0)) && (_568 <= 1.0)) && (_586 > 0.0);
                bool _813;
                if (_588)
                {
                    float _600 = _586 / _562;
                    float _602 = dFdx(_600);
                    float _604 = dFdy(_600);
                    _813 = (texture(ShadowMap0, _838).x < (_600 - (_166.DepthBias + clamp(sqrt(fma(_602, _602, _604 * _604)), 0.0, 0.00999999977648258209228515625)))) ? true : false;
                }
                else
                {
                    _813 = false;
                }
                if (_588 ? true : false)
                {
                    _821 = _813;
                    break;
                }
                if (_166.NumCascades > 1.0)
                {
                    vec4 _640 = spvWorkaroundRowMajor(_166.LightViewProj1) * _405;
                    float _645 = _640.w;
                    vec2 _648 = ((_640.xy * 0.5) / vec2(_645)) + vec2(0.5);
                    float _651 = 1.0 - _648.y;
                    vec2 _849 = _648;
                    _849.y = _651;
                    float _654 = _648.x;
                    float _669 = _640.z;
                    bool _671 = ((((_654 >= 0.0) && (_654 <= 1.0)) && (_651 >= 0.0)) && (_651 <= 1.0)) && (_669 > 0.0);
                    bool _816;
                    if (_671)
                    {
                        float _683 = _669 / _645;
                        float _685 = dFdx(_683);
                        float _687 = dFdy(_683);
                        _816 = (texture(ShadowMap1, _849).x < (_683 - (_166.DepthBias + clamp(sqrt(fma(_685, _685, _687 * _687)), 0.0, 0.00999999977648258209228515625)))) ? true : false;
                    }
                    else
                    {
                        _816 = false;
                    }
                    if (_671 ? true : false)
                    {
                        _821 = _816;
                        break;
                    }
                    if (_166.NumCascades > 2.0)
                    {
                        vec4 _723 = spvWorkaroundRowMajor(_166.LightViewProj2) * _405;
                        float _728 = _723.w;
                        vec2 _731 = ((_723.xy * 0.5) / vec2(_728)) + vec2(0.5);
                        float _734 = 1.0 - _731.y;
                        vec2 _860 = _731;
                        _860.y = _734;
                        float _737 = _731.x;
                        float _752 = _723.z;
                        bool _754 = ((((_737 >= 0.0) && (_737 <= 1.0)) && (_734 >= 0.0)) && (_734 <= 1.0)) && (_752 > 0.0);
                        bool _819;
                        if (_754)
                        {
                            float _766 = _752 / _728;
                            float _768 = dFdx(_766);
                            float _770 = dFdy(_766);
                            _819 = (texture(ShadowMap2, _860).x < (_766 - (_166.DepthBias + clamp(sqrt(fma(_768, _768, _770 * _770)), 0.0, 0.00999999977648258209228515625)))) ? true : false;
                        }
                        else
                        {
                            _819 = false;
                        }
                        if (_754 ? true : false)
                        {
                            _821 = _819;
                            break;
                        }
                    }
                }
            }
        }
        _821 = false;
        break;
    } while(false);
    vec3 _833;
    if (_821)
    {
        _833 = _443 * vec3(0.5);
    }
    else
    {
        _833 = _443;
    }
    _entryPointOutput = vec4(_833, input_Color.w);
}