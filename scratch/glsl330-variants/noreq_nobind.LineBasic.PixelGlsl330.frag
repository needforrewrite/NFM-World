
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
    vec3 _1057;
    if (varying_5 > 0.0)
    {
        bool _985;
        do
        {
            if (_197.NumCascades > 0.0)
            {
                if (abs(dot(varying_3, _197.LightDirection)) >= 0.0500000007450580596923828125)
                {
                    vec4 _655 = spvWorkaroundRowMajor(_197.LightViewProj0) * varying_1;
                    float _660 = _655.w;
                    vec2 _663 = ((_655.xy * 0.5) / vec2(_660)) + vec2(0.5);
                    float _666 = 1.0 - _663.y;
                    vec2 _1007 = _663;
                    _1007.y = _666;
                    float _669 = _663.x;
                    float _684 = _655.z;
                    bool _686 = ((((_669 >= 0.0) && (_669 <= 1.0)) && (_666 >= 0.0)) && (_666 <= 1.0)) && (_684 > 0.0);
                    bool _977;
                    if (_686)
                    {
                        float _698 = _684 / _660;
                        float _700 = dFdx(_698);
                        float _702 = dFdy(_698);
                        _977 = (texture(ShadowMap0, _1007).x < (_698 - (_197.DepthBias + clamp(sqrt(_700 * _700 + (_702 * _702)), 0.0, 0.00999999977648258209228515625)))) ? true : false;
                    }
                    else
                    {
                        _977 = false;
                    }
                    if (_686 ? true : false)
                    {
                        _985 = _977;
                        break;
                    }
                    if (_197.NumCascades > 1.0)
                    {
                        vec4 _738 = spvWorkaroundRowMajor(_197.LightViewProj1) * varying_1;
                        float _743 = _738.w;
                        vec2 _746 = ((_738.xy * 0.5) / vec2(_743)) + vec2(0.5);
                        float _749 = 1.0 - _746.y;
                        vec2 _1018 = _746;
                        _1018.y = _749;
                        float _752 = _746.x;
                        float _767 = _738.z;
                        bool _769 = ((((_752 >= 0.0) && (_752 <= 1.0)) && (_749 >= 0.0)) && (_749 <= 1.0)) && (_767 > 0.0);
                        bool _980;
                        if (_769)
                        {
                            float _781 = _767 / _743;
                            float _783 = dFdx(_781);
                            float _785 = dFdy(_781);
                            _980 = (texture(ShadowMap1, _1018).x < (_781 - (_197.DepthBias + clamp(sqrt(_783 * _783 + (_785 * _785)), 0.0, 0.00999999977648258209228515625)))) ? true : false;
                        }
                        else
                        {
                            _980 = false;
                        }
                        if (_769 ? true : false)
                        {
                            _985 = _980;
                            break;
                        }
                        if (_197.NumCascades > 2.0)
                        {
                            vec4 _821 = spvWorkaroundRowMajor(_197.LightViewProj2) * varying_1;
                            float _826 = _821.w;
                            vec2 _829 = ((_821.xy * 0.5) / vec2(_826)) + vec2(0.5);
                            float _832 = 1.0 - _829.y;
                            vec2 _1029 = _829;
                            _1029.y = _832;
                            float _835 = _829.x;
                            float _850 = _821.z;
                            bool _852 = ((((_835 >= 0.0) && (_835 <= 1.0)) && (_832 >= 0.0)) && (_832 <= 1.0)) && (_850 > 0.0);
                            bool _983;
                            if (_852)
                            {
                                float _864 = _850 / _826;
                                float _866 = dFdx(_864);
                                float _868 = dFdy(_864);
                                _983 = (texture(ShadowMap2, _1029).x < (_864 - (_197.DepthBias + clamp(sqrt(_866 * _866 + (_868 * _868)), 0.0, 0.00999999977648258209228515625)))) ? true : false;
                            }
                            else
                            {
                                _983 = false;
                            }
                            if (_852 ? true : false)
                            {
                                _985 = _983;
                                break;
                            }
                        }
                    }
                }
            }
            _985 = false;
            break;
        } while(false);
        vec3 _902 = varying_0.xyz * (_197.EnvironmentLight.y * (((varying_2 > 0.0) && _985) ? 0.0 : varying_6) + _197.EnvironmentLight.x);
        _1057 = min(_902 * ((_197.SnapColor * 255.0) * vec3(0.00999999977648258209228515625)) + _902, vec3(1.0));
    }
    else
    {
        _1057 = varying_0.xyz;
    }
    vec3 _1058;
    if (_197.ChargedBlinkAmount > 0.0)
    {
        _1058 = vec3(_197.ChargedBlinkAmount * 0.10000000894069671630859375, (12.80000019073486328125 * _197.ChargedBlinkAmount + 128.0) * 0.0039215688593685626983642578125, 1.0);
    }
    else
    {
        _1058 = _1057;
    }
    float _925 = exp2(max(((-_197.FogDistance) * 0.5 + varying_4) / _197.FogDistance, 0.0) * _197.FogLogDensity);
    _entryPointOutput = vec4(_1058 * vec3(_925) + (_197.FogColor * vec3(1.0 - _925)), varying_0.w);
}