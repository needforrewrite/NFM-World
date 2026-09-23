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
};

uniform _Global _195;

uniform sampler2D _1014;
uniform sampler2D _1016;
uniform sampler2D _1018;

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
    vec3 _968;
    if (input_Lit > 0.0)
    {
        bool _943;
        do
        {
            if (_195.NumCascades > 0.0)
            {
                if (abs(dot(input_NormalWorld, _195.LightDirection)) >= 0.0500000007450580596923828125)
                {
                    vec4 _615 = spvWorkaroundRowMajor(_195.LightViewProj0) * input_WorldPos;
                    float _620 = _615.w;
                    vec2 _623 = ((_615.xy * 0.5) / vec2(_620)) + vec2(0.5);
                    float _626 = 1.0 - _623.y;
                    vec2 _974 = _623;
                    _974.y = _626;
                    float _629 = _623.x;
                    float _644 = _615.z;
                    bool _646 = ((((_629 >= 0.0) && (_629 <= 1.0)) && (_626 >= 0.0)) && (_626 <= 1.0)) && (_644 > 0.0);
                    bool _935;
                    if (_646)
                    {
                        float _658 = _644 / _620;
                        float _660 = dFdx(_658);
                        float _662 = dFdy(_658);
                        _935 = texture(_1014, _974).x < (_658 - (_195.DepthBias + clamp(sqrt((_660 * _660) + (_662 * _662)), 0.0, 0.00999999977648258209228515625)));
                    }
                    else
                    {
                        _935 = false;
                    }
                    if (_646)
                    {
                        _943 = _935;
                        break;
                    }
                    if (_195.NumCascades > 1.0)
                    {
                        vec4 _698 = spvWorkaroundRowMajor(_195.LightViewProj1) * input_WorldPos;
                        float _703 = _698.w;
                        vec2 _706 = ((_698.xy * 0.5) / vec2(_703)) + vec2(0.5);
                        float _709 = 1.0 - _706.y;
                        vec2 _985 = _706;
                        _985.y = _709;
                        float _712 = _706.x;
                        float _727 = _698.z;
                        bool _729 = ((((_712 >= 0.0) && (_712 <= 1.0)) && (_709 >= 0.0)) && (_709 <= 1.0)) && (_727 > 0.0);
                        bool _938;
                        if (_729)
                        {
                            float _741 = _727 / _703;
                            float _743 = dFdx(_741);
                            float _745 = dFdy(_741);
                            _938 = texture(_1016, _985).x < (_741 - (_195.DepthBias + clamp(sqrt((_743 * _743) + (_745 * _745)), 0.0, 0.00999999977648258209228515625)));
                        }
                        else
                        {
                            _938 = false;
                        }
                        if (_729)
                        {
                            _943 = _938;
                            break;
                        }
                        if (_195.NumCascades > 2.0)
                        {
                            vec4 _781 = spvWorkaroundRowMajor(_195.LightViewProj2) * input_WorldPos;
                            float _786 = _781.w;
                            vec2 _789 = ((_781.xy * 0.5) / vec2(_786)) + vec2(0.5);
                            float _792 = 1.0 - _789.y;
                            vec2 _996 = _789;
                            _996.y = _792;
                            float _795 = _789.x;
                            float _810 = _781.z;
                            bool _812 = ((((_795 >= 0.0) && (_795 <= 1.0)) && (_792 >= 0.0)) && (_792 <= 1.0)) && (_810 > 0.0);
                            bool _941;
                            if (_812)
                            {
                                float _824 = _810 / _786;
                                float _826 = dFdx(_824);
                                float _828 = dFdy(_824);
                                _941 = texture(_1018, _996).x < (_824 - (_195.DepthBias + clamp(sqrt((_826 * _826) + (_828 * _828)), 0.0, 0.00999999977648258209228515625)));
                            }
                            else
                            {
                                _941 = false;
                            }
                            if (_812)
                            {
                                _943 = _941;
                                break;
                            }
                        }
                    }
                }
            }
            _943 = false;
            break;
        } while(false);
        vec3 _862 = input_Color.xyz * (_195.EnvironmentLight.x + (_195.EnvironmentLight.y * (((input_GetsShadowed > 0.0) && _943) ? 0.0 : input_Diffuse)));
        _968 = min(_862 + (_862 * ((_195.SnapColor * 255.0) * vec3(0.00999999977648258209228515625))), vec3(1.0));
    }
    else
    {
        _968 = input_Color.xyz;
    }
    float _885 = exp2(max((input_ViewLength - (_195.FogDistance * 0.5)) / _195.FogDistance, 0.0) * _195.FogLogDensity);
    _entryPointOutput = vec4((_968 * vec3(_885)) + (_195.FogColor * vec3(1.0 - _885)), input_Color.w);
}

