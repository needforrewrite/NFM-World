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
};

uniform _Global _195;

uniform sampler2D _1062;
uniform sampler2D _1064;
uniform sampler2D _1066;

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
    vec3 _1051;
    if (input_Lit > 0.0)
    {
        bool _981;
        do
        {
            if (_195.NumCascades > 0.0)
            {
                if (abs(dot(input_NormalWorld, _195.LightDirection)) >= 0.0500000007450580596923828125)
                {
                    vec4 _653 = spvWorkaroundRowMajor(_195.LightViewProj0) * input_WorldPos;
                    float _658 = _653.w;
                    vec2 _661 = ((_653.xy * 0.5) / vec2(_658)) + vec2(0.5);
                    float _664 = 1.0 - _661.y;
                    vec2 _1001 = _661;
                    _1001.y = _664;
                    float _667 = _661.x;
                    float _682 = _653.z;
                    bool _684 = ((((_667 >= 0.0) && (_667 <= 1.0)) && (_664 >= 0.0)) && (_664 <= 1.0)) && (_682 > 0.0);
                    bool _973;
                    if (_684)
                    {
                        float _696 = _682 / _658;
                        float _698 = dFdx(_696);
                        float _700 = dFdy(_696);
                        _973 = texture(_1062, _1001).x < (_696 - (_195.DepthBias + clamp(sqrt((_698 * _698) + (_700 * _700)), 0.0, 0.00999999977648258209228515625)));
                    }
                    else
                    {
                        _973 = false;
                    }
                    if (_684)
                    {
                        _981 = _973;
                        break;
                    }
                    if (_195.NumCascades > 1.0)
                    {
                        vec4 _736 = spvWorkaroundRowMajor(_195.LightViewProj1) * input_WorldPos;
                        float _741 = _736.w;
                        vec2 _744 = ((_736.xy * 0.5) / vec2(_741)) + vec2(0.5);
                        float _747 = 1.0 - _744.y;
                        vec2 _1012 = _744;
                        _1012.y = _747;
                        float _750 = _744.x;
                        float _765 = _736.z;
                        bool _767 = ((((_750 >= 0.0) && (_750 <= 1.0)) && (_747 >= 0.0)) && (_747 <= 1.0)) && (_765 > 0.0);
                        bool _976;
                        if (_767)
                        {
                            float _779 = _765 / _741;
                            float _781 = dFdx(_779);
                            float _783 = dFdy(_779);
                            _976 = texture(_1064, _1012).x < (_779 - (_195.DepthBias + clamp(sqrt((_781 * _781) + (_783 * _783)), 0.0, 0.00999999977648258209228515625)));
                        }
                        else
                        {
                            _976 = false;
                        }
                        if (_767)
                        {
                            _981 = _976;
                            break;
                        }
                        if (_195.NumCascades > 2.0)
                        {
                            vec4 _819 = spvWorkaroundRowMajor(_195.LightViewProj2) * input_WorldPos;
                            float _824 = _819.w;
                            vec2 _827 = ((_819.xy * 0.5) / vec2(_824)) + vec2(0.5);
                            float _830 = 1.0 - _827.y;
                            vec2 _1023 = _827;
                            _1023.y = _830;
                            float _833 = _827.x;
                            float _848 = _819.z;
                            bool _850 = ((((_833 >= 0.0) && (_833 <= 1.0)) && (_830 >= 0.0)) && (_830 <= 1.0)) && (_848 > 0.0);
                            bool _979;
                            if (_850)
                            {
                                float _862 = _848 / _824;
                                float _864 = dFdx(_862);
                                float _866 = dFdy(_862);
                                _979 = texture(_1066, _1023).x < (_862 - (_195.DepthBias + clamp(sqrt((_864 * _864) + (_866 * _866)), 0.0, 0.00999999977648258209228515625)));
                            }
                            else
                            {
                                _979 = false;
                            }
                            if (_850)
                            {
                                _981 = _979;
                                break;
                            }
                        }
                    }
                }
            }
            _981 = false;
            break;
        } while(false);
        vec3 _900 = input_Color.xyz * (_195.EnvironmentLight.x + (_195.EnvironmentLight.y * (((input_GetsShadowed > 0.0) && _981) ? 0.0 : input_Diffuse)));
        _1051 = min(_900 + (_900 * ((_195.SnapColor * 255.0) * vec3(0.00999999977648258209228515625))), vec3(1.0));
    }
    else
    {
        _1051 = input_Color.xyz;
    }
    vec3 _1052;
    if (_195.ChargedBlinkAmount > 0.0)
    {
        _1052 = vec3(_195.ChargedBlinkAmount * 0.10000000894069671630859375, (128.0 + (12.80000019073486328125 * _195.ChargedBlinkAmount)) * 0.0039215688593685626983642578125, 1.0);
    }
    else
    {
        _1052 = _1051;
    }
    float _923 = exp2(max((input_ViewLength - (_195.FogDistance * 0.5)) / _195.FogDistance, 0.0) * _195.FogLogDensity);
    _entryPointOutput = vec4((_1052 * vec3(_923)) + (_195.FogColor * vec3(1.0 - _923)), input_Color.w);
}

