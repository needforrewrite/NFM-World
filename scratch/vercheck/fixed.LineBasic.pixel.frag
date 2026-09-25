
#version 420
#ifdef GL_ARB_shading_language_420pack
#extension GL_ARB_shading_language_420pack : require
#endif

uniform mat4 _197_LightViewProj0;
uniform mat4 _197_LightViewProj1;
uniform mat4 _197_LightViewProj2;
uniform float _197_DepthBias;
uniform float _197_NumCascades;
uniform vec3 _197_LightDirection;
uniform mat4 _197_View;
uniform mat4 _197_Projection;
uniform mat4 _197_ViewProj;
uniform vec3 _197_SnapColor;
uniform uint _197_IsFullbright;
uniform uint _197_UseBaseColor;
uniform vec3 _197_BaseColor;
uniform vec3 _197_FogColor;
uniform float _197_FogDistance;
uniform float _197_g1_FogLogDensity;
uniform vec2 _197_g1_EnvironmentLight;
uniform vec3 _197_g1_CameraPosition;
uniform float _197_g1_Alpha;
uniform uint _197_g1_Expand;
uniform float _197_g1_RandomFloat;
uniform float _197_g1_Darken;
uniform float _197_g1_ChargedBlinkAmount;
uniform float _197_g1_HalfThickness;
uniform vec2 _197_g1_Resolution;
uniform float _197_g1_DistantOutlineDistanceFalloffWithCutoffMask;
uniform float _197_g1_DistantOutlineClassicCutoffMask;
uniform float _197_g1_DistantOutlineDistanceFalloffMask;
uniform float _197_g1_OutlineClassicCutoffDistance;
uniform float _197_g2_OutlineFalloffStartDistance;
uniform float _197_g2_OutlineFalloffCutoffDistance;
uniform float _197_g2_OutlineFalloffLinearFadeStartDistance;
uniform float _197_g2_OutlineFalloffLinearFadeStartThickness;
uniform float _197_g2_OutlineFalloffInverseLinearFadeLength;




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
    vec3 _1057;
    if (input_Lit > 0.0)
    {
        bool _985;
        do
        {
            if (_197_NumCascades > 0.0)
            {
                if (abs(dot(input_NormalWorld, _197_LightDirection)) >= 0.0500000007450580596923828125)
                {
                    vec4 _655 = spvWorkaroundRowMajor(_197_LightViewProj0) * input_WorldPos;
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
                        _977 = (texture(ShadowMap0, _1007).x < (_698 - (_197_DepthBias + clamp(sqrt(fma(_700, _700, _702 * _702)), 0.0, 0.00999999977648258209228515625)))) ? true : false;
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
                    if (_197_NumCascades > 1.0)
                    {
                        vec4 _738 = spvWorkaroundRowMajor(_197_LightViewProj1) * input_WorldPos;
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
                            _980 = (texture(ShadowMap1, _1018).x < (_781 - (_197_DepthBias + clamp(sqrt(fma(_783, _783, _785 * _785)), 0.0, 0.00999999977648258209228515625)))) ? true : false;
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
                        if (_197_NumCascades > 2.0)
                        {
                            vec4 _821 = spvWorkaroundRowMajor(_197_LightViewProj2) * input_WorldPos;
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
                                _983 = (texture(ShadowMap2, _1029).x < (_864 - (_197_DepthBias + clamp(sqrt(fma(_866, _866, _868 * _868)), 0.0, 0.00999999977648258209228515625)))) ? true : false;
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
        vec3 _902 = input_Color.xyz * fma(_197_g1_EnvironmentLight.y, ((input_GetsShadowed > 0.0) && _985) ? 0.0 : input_Diffuse, _197_g1_EnvironmentLight.x);
        _1057 = min(fma(_902, (_197_SnapColor * 255.0) * vec3(0.00999999977648258209228515625), _902), vec3(1.0));
    }
    else
    {
        _1057 = input_Color.xyz;
    }
    vec3 _1058;
    if (_197_g1_ChargedBlinkAmount > 0.0)
    {
        _1058 = vec3(_197_g1_ChargedBlinkAmount * 0.10000000894069671630859375, fma(12.80000019073486328125, _197_g1_ChargedBlinkAmount, 128.0) * 0.0039215688593685626983642578125, 1.0);
    }
    else
    {
        _1058 = _1057;
    }
    float _925 = exp2(max(fma(-_197_FogDistance, 0.5, input_ViewLength) / _197_FogDistance, 0.0) * _197_g1_FogLogDensity);
    _entryPointOutput = vec4(fma(_1058, vec3(_925), _197_FogColor * vec3(1.0 - _925)), input_Color.w);
}