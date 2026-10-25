#version 410
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

uniform _Global _164;

uniform sampler2D _857;
uniform sampler2D _859;
uniform sampler2D _861;

layout(location = 0) in vec4 input_Color;
layout(location = 1) in vec4 input_WorldPos;
layout(location = 2) in float input_ViewLength;
layout(location = 0) out vec4 _entryPointOutput;

mat4 spvWorkaroundRowMajor(mat4 wrap) { return wrap; }

void main()
{
    float _425 = exp2(max((input_ViewLength - (_164.FogDistance * 0.5)) / _164.FogDistance, 0.0) * _164.FogLogDensity);
    vec3 _439 = (input_Color.xyz * vec3(_425)) + (_164.FogColor * vec3(1.0 - _425));
    vec4 _403 = vec4(input_WorldPos.xyz, 1.0);
    bool _817;
    do
    {
        if (_164.NumCascades > 0.0)
        {
            if (abs(_164.LightDirection.y) >= 0.0500000007450580596923828125)
            {
                vec4 _553 = spvWorkaroundRowMajor(_164.LightViewProj0) * _403;
                float _558 = _553.w;
                vec2 _561 = ((_553.xy * 0.5) / vec2(_558)) + vec2(0.5);
                float _564 = 1.0 - _561.y;
                vec2 _833 = _561;
                _833.y = _564;
                float _567 = _561.x;
                float _582 = _553.z;
                bool _584 = ((((_567 >= 0.0) && (_567 <= 1.0)) && (_564 >= 0.0)) && (_564 <= 1.0)) && (_582 > 0.0);
                bool _809;
                if (_584)
                {
                    float _596 = _582 / _558;
                    float _598 = dFdx(_596);
                    float _600 = dFdy(_596);
                    _809 = texture(_857, _833).x < (_596 - (_164.DepthBias + clamp(sqrt((_598 * _598) + (_600 * _600)), 0.0, 0.00999999977648258209228515625)));
                }
                else
                {
                    _809 = false;
                }
                if (_584)
                {
                    _817 = _809;
                    break;
                }
                if (_164.NumCascades > 1.0)
                {
                    vec4 _636 = spvWorkaroundRowMajor(_164.LightViewProj1) * _403;
                    float _641 = _636.w;
                    vec2 _644 = ((_636.xy * 0.5) / vec2(_641)) + vec2(0.5);
                    float _647 = 1.0 - _644.y;
                    vec2 _844 = _644;
                    _844.y = _647;
                    float _650 = _644.x;
                    float _665 = _636.z;
                    bool _667 = ((((_650 >= 0.0) && (_650 <= 1.0)) && (_647 >= 0.0)) && (_647 <= 1.0)) && (_665 > 0.0);
                    bool _812;
                    if (_667)
                    {
                        float _679 = _665 / _641;
                        float _681 = dFdx(_679);
                        float _683 = dFdy(_679);
                        _812 = texture(_859, _844).x < (_679 - (_164.DepthBias + clamp(sqrt((_681 * _681) + (_683 * _683)), 0.0, 0.00999999977648258209228515625)));
                    }
                    else
                    {
                        _812 = false;
                    }
                    if (_667)
                    {
                        _817 = _812;
                        break;
                    }
                    if (_164.NumCascades > 2.0)
                    {
                        vec4 _719 = spvWorkaroundRowMajor(_164.LightViewProj2) * _403;
                        float _724 = _719.w;
                        vec2 _727 = ((_719.xy * 0.5) / vec2(_724)) + vec2(0.5);
                        float _730 = 1.0 - _727.y;
                        vec2 _855 = _727;
                        _855.y = _730;
                        float _733 = _727.x;
                        float _748 = _719.z;
                        bool _750 = ((((_733 >= 0.0) && (_733 <= 1.0)) && (_730 >= 0.0)) && (_730 <= 1.0)) && (_748 > 0.0);
                        bool _815;
                        if (_750)
                        {
                            float _762 = _748 / _724;
                            float _764 = dFdx(_762);
                            float _766 = dFdy(_762);
                            _815 = texture(_861, _855).x < (_762 - (_164.DepthBias + clamp(sqrt((_764 * _764) + (_766 * _766)), 0.0, 0.00999999977648258209228515625)));
                        }
                        else
                        {
                            _815 = false;
                        }
                        if (_750)
                        {
                            _817 = _815;
                            break;
                        }
                    }
                }
            }
        }
        _817 = false;
        break;
    } while(false);
    vec3 _829;
    if (_817)
    {
        _829 = _439 * vec3(0.5);
    }
    else
    {
        _829 = _439;
    }
    _entryPointOutput = vec4(_829, input_Color.w);
}

