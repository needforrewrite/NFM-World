#include <metal_stdlib>
#include <simd/simd.h>

using namespace metal;

struct _Global
{
    float4x4 LightViewProj0;
    float4x4 LightViewProj1;
    float4x4 LightViewProj2;
    float DepthBias;
    float NumCascades;
    float3 LightDirection;
    float4x4 WorldView;
    float4x4 WorldViewProj;
    packed_float3 FogColor;
    float FogDistance;
    float FogLogDensity;
};

struct main0_out
{
    float4 _entryPointOutput [[color(0)]];
};

struct main0_in
{
    float4 input_Color [[user(locn0)]];
    float4 input_WorldPos [[user(locn1)]];
    float input_ViewLength [[user(locn2)]];
};

fragment main0_out main0(main0_in in [[stage_in]], constant _Global& _164 [[buffer(0)]], texture2d<float> ShadowMap0 [[texture(0)]], texture2d<float> ShadowMap1 [[texture(1)]], texture2d<float> ShadowMap2 [[texture(2)]], sampler ShadowMapSampler0 [[sampler(0)]], sampler ShadowMapSampler1 [[sampler(1)]], sampler ShadowMapSampler2 [[sampler(2)]])
{
    main0_out out = {};
    float _425 = exp2(fast::max((in.input_ViewLength - (_164.FogDistance * 0.5)) / _164.FogDistance, 0.0) * _164.FogLogDensity);
    float3 _439 = (in.input_Color.xyz * float3(_425)) + (float3(_164.FogColor) * float3(1.0 - _425));
    float4 _403 = float4(in.input_WorldPos.xyz, 1.0);
    bool _817;
    do
    {
        if (_164.NumCascades > 0.0)
        {
            if (abs(_164.LightDirection.y) >= 0.0500000007450580596923828125)
            {
                float4 _553 = _403 * _164.LightViewProj0;
                float _558 = _553.w;
                float2 _561 = ((_553.xy * 0.5) / float2(_558)) + float2(0.5);
                float _564 = 1.0 - _561.y;
                float2 _833 = _561;
                _833.y = _564;
                float _567 = _561.x;
                float _582 = _553.z;
                bool _584 = ((((_567 >= 0.0) && (_567 <= 1.0)) && (_564 >= 0.0)) && (_564 <= 1.0)) && (_582 > 0.0);
                bool _809;
                if (_584)
                {
                    float _596 = _582 / _558;
                    float _598 = dfdx(_596);
                    float _600 = dfdy(_596);
                    _809 = ShadowMap0.sample(ShadowMapSampler0, _833).x < (_596 - (_164.DepthBias + fast::clamp(sqrt((_598 * _598) + (_600 * _600)), 0.0, 0.00999999977648258209228515625)));
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
                    float4 _636 = _403 * _164.LightViewProj1;
                    float _641 = _636.w;
                    float2 _644 = ((_636.xy * 0.5) / float2(_641)) + float2(0.5);
                    float _647 = 1.0 - _644.y;
                    float2 _844 = _644;
                    _844.y = _647;
                    float _650 = _644.x;
                    float _665 = _636.z;
                    bool _667 = ((((_650 >= 0.0) && (_650 <= 1.0)) && (_647 >= 0.0)) && (_647 <= 1.0)) && (_665 > 0.0);
                    bool _812;
                    if (_667)
                    {
                        float _679 = _665 / _641;
                        float _681 = dfdx(_679);
                        float _683 = dfdy(_679);
                        _812 = ShadowMap1.sample(ShadowMapSampler1, _844).x < (_679 - (_164.DepthBias + fast::clamp(sqrt((_681 * _681) + (_683 * _683)), 0.0, 0.00999999977648258209228515625)));
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
                        float4 _719 = _403 * _164.LightViewProj2;
                        float _724 = _719.w;
                        float2 _727 = ((_719.xy * 0.5) / float2(_724)) + float2(0.5);
                        float _730 = 1.0 - _727.y;
                        float2 _855 = _727;
                        _855.y = _730;
                        float _733 = _727.x;
                        float _748 = _719.z;
                        bool _750 = ((((_733 >= 0.0) && (_733 <= 1.0)) && (_730 >= 0.0)) && (_730 <= 1.0)) && (_748 > 0.0);
                        bool _815;
                        if (_750)
                        {
                            float _762 = _748 / _724;
                            float _764 = dfdx(_762);
                            float _766 = dfdy(_762);
                            _815 = ShadowMap2.sample(ShadowMapSampler2, _855).x < (_762 - (_164.DepthBias + fast::clamp(sqrt((_764 * _764) + (_766 * _766)), 0.0, 0.00999999977648258209228515625)));
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
    float3 _829;
    if (_817)
    {
        _829 = _439 * float3(0.5);
    }
    else
    {
        _829 = _439;
    }
    out._entryPointOutput = float4(_829, in.input_Color.w);
    return out;
}

