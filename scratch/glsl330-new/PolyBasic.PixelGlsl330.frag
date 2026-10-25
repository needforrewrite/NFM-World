#version 330

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
} _195;

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
    vec3 _968;
    if (varying_5 > 0.0)
    {
        bool _943;
        do
        {
            if (_195.NumCascades > 0.0)
            {
                if (abs(dot(varying_3, _195.LightDirection)) >= 0.0500000007450580596923828125)
                {
                    vec4 _615 = spvWorkaroundRowMajor(_195.LightViewProj0) * varying_1;
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
                        _935 = texture(ShadowMap0, _974).x < (_658 - (_195.DepthBias + clamp(sqrt((_660 * _660) + (_662 * _662)), 0.0, 0.00999999977648258209228515625)));
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
                        vec4 _698 = spvWorkaroundRowMajor(_195.LightViewProj1) * varying_1;
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
                            _938 = texture(ShadowMap1, _985).x < (_741 - (_195.DepthBias + clamp(sqrt((_743 * _743) + (_745 * _745)), 0.0, 0.00999999977648258209228515625)));
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
                            vec4 _781 = spvWorkaroundRowMajor(_195.LightViewProj2) * varying_1;
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
                                _941 = texture(ShadowMap2, _996).x < (_824 - (_195.DepthBias + clamp(sqrt((_826 * _826) + (_828 * _828)), 0.0, 0.00999999977648258209228515625)));
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
        vec3 _862 = varying_0.xyz * (_195.EnvironmentLight.x + (_195.EnvironmentLight.y * (((varying_2 > 0.0) && _943) ? 0.0 : varying_6)));
        _968 = min(_862 + (_862 * ((_195.SnapColor * 255.0) * vec3(0.00999999977648258209228515625))), vec3(1.0));
    }
    else
    {
        _968 = varying_0.xyz;
    }
    float _885 = exp2(max((varying_4 - (_195.FogDistance * 0.5)) / _195.FogDistance, 0.0) * _195.FogLogDensity);
    _entryPointOutput = vec4((_968 * vec3(_885)) + (_195.FogColor * vec3(1.0 - _885)), varying_0.w);
}
