
#version 330
#ifdef GL_ARB_shading_language_420pack
#extension GL_ARB_shading_language_420pack : require
#endif

layout(std140) uniform _Global
{
    layout(row_major) mat4 LightViewProj0;
    layout(row_major) mat4 LightViewProj1;
    layout(row_major) mat4 LightViewProj2;
    float DepthBias;
    float NumCascades;
    vec3 LightDirection;
    layout(row_major) mat4 WorldView;
    layout(row_major) mat4 WorldViewProj;
    vec3 FogColor;
    float FogDistance;
    float FogLogDensity;
} _130;

uniform sampler2D ShadowMap0;
uniform sampler2D ShadowMap1;
uniform sampler2D ShadowMap2;

in vec4 varying_0;
in vec4 varying_1;
layout(location = 0) out vec4 _entryPointOutput;

mat4 spvWorkaroundRowMajor(mat4 wrap) { return wrap; }

void main()
{
    vec3 _340 = dFdx(varying_1.xyz);
    vec3 _344 = dFdy(varying_1.xyz);
    vec4 _353 = vec4(varying_1.xyz, 1.0);
    bool _738;
    do
    {
        if (_130.NumCascades > 0.0)
        {
            if (abs(dot(normalize(cross(_340, _344)), _130.LightDirection)) >= 0.0500000007450580596923828125)
            {
                vec4 _478 = spvWorkaroundRowMajor(_130.LightViewProj0) * _353;
                float _483 = _478.w;
                vec2 _486 = ((_478.xy * 0.5) / vec2(_483)) + vec2(0.5);
                float _489 = 1.0 - _486.y;
                vec2 _754 = _486;
                _754.y = _489;
                float _492 = _486.x;
                float _507 = _478.z;
                bool _509 = ((((_492 >= 0.0) && (_492 <= 1.0)) && (_489 >= 0.0)) && (_489 <= 1.0)) && (_507 > 0.0);
                bool _730;
                if (_509)
                {
                    float _521 = _507 / _483;
                    float _523 = dFdx(_521);
                    float _525 = dFdy(_521);
                    _730 = (texture(ShadowMap0, _754).x < (_521 - (_130.DepthBias + clamp(sqrt(_523 * _523 + (_525 * _525)), 0.0, 0.00999999977648258209228515625)))) ? true : false;
                }
                else
                {
                    _730 = false;
                }
                if (_509 ? true : false)
                {
                    _738 = _730;
                    break;
                }
                if (_130.NumCascades > 1.0)
                {
                    vec4 _561 = spvWorkaroundRowMajor(_130.LightViewProj1) * _353;
                    float _566 = _561.w;
                    vec2 _569 = ((_561.xy * 0.5) / vec2(_566)) + vec2(0.5);
                    float _572 = 1.0 - _569.y;
                    vec2 _765 = _569;
                    _765.y = _572;
                    float _575 = _569.x;
                    float _590 = _561.z;
                    bool _592 = ((((_575 >= 0.0) && (_575 <= 1.0)) && (_572 >= 0.0)) && (_572 <= 1.0)) && (_590 > 0.0);
                    bool _733;
                    if (_592)
                    {
                        float _604 = _590 / _566;
                        float _606 = dFdx(_604);
                        float _608 = dFdy(_604);
                        _733 = (texture(ShadowMap1, _765).x < (_604 - (_130.DepthBias + clamp(sqrt(_606 * _606 + (_608 * _608)), 0.0, 0.00999999977648258209228515625)))) ? true : false;
                    }
                    else
                    {
                        _733 = false;
                    }
                    if (_592 ? true : false)
                    {
                        _738 = _733;
                        break;
                    }
                    if (_130.NumCascades > 2.0)
                    {
                        vec4 _644 = spvWorkaroundRowMajor(_130.LightViewProj2) * _353;
                        float _649 = _644.w;
                        vec2 _652 = ((_644.xy * 0.5) / vec2(_649)) + vec2(0.5);
                        float _655 = 1.0 - _652.y;
                        vec2 _776 = _652;
                        _776.y = _655;
                        float _658 = _652.x;
                        float _673 = _644.z;
                        bool _675 = ((((_658 >= 0.0) && (_658 <= 1.0)) && (_655 >= 0.0)) && (_655 <= 1.0)) && (_673 > 0.0);
                        bool _736;
                        if (_675)
                        {
                            float _687 = _673 / _649;
                            float _689 = dFdx(_687);
                            float _691 = dFdy(_687);
                            _736 = (texture(ShadowMap2, _776).x < (_687 - (_130.DepthBias + clamp(sqrt(_689 * _689 + (_691 * _691)), 0.0, 0.00999999977648258209228515625)))) ? true : false;
                        }
                        else
                        {
                            _736 = false;
                        }
                        if (_675 ? true : false)
                        {
                            _738 = _736;
                            break;
                        }
                    }
                }
            }
        }
        _738 = false;
        break;
    } while(false);
    vec3 _750;
    if (_738)
    {
        _750 = varying_0.xyz * vec3(0.5);
    }
    else
    {
        _750 = varying_0.xyz;
    }
    _entryPointOutput = vec4(_750, varying_0.w);
}