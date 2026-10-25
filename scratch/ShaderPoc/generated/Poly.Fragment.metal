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
    float4x4 View;
    float4x4 Projection;
    float4x4 ViewProj;
    packed_float3 SnapColor;
    uint IsFullbright;
    uint UseBaseColor;
    float3 BaseColor;
    packed_float3 FogColor;
    float FogDistance;
    float FogLogDensity;
    float2 EnvironmentLight;
    packed_float3 CameraPosition;
    float Alpha;
    uint Expand;
    float RandomFloat;
    float Darken;
};

struct main0_out
{
    float4 _entryPointOutput [[color(0)]];
};

struct main0_in
{
    float4 input_Color [[user(locn0)]];
    float4 input_WorldPos [[user(locn1)]];
    float input_GetsShadowed [[user(locn2)]];
    float3 input_NormalWorld [[user(locn3)]];
    float input_ViewLength [[user(locn4)]];
    float input_Lit [[user(locn5)]];
    float input_Diffuse [[user(locn6)]];
};

fragment main0_out main0(main0_in in [[stage_in]], constant _Global& _195 [[buffer(0)]], texture2d<float> ShadowMap0 [[texture(0)]], texture2d<float> ShadowMap1 [[texture(1)]], texture2d<float> ShadowMap2 [[texture(2)]], sampler ShadowMapSampler0 [[sampler(0)]], sampler ShadowMapSampler1 [[sampler(1)]], sampler ShadowMapSampler2 [[sampler(2)]])
{
    main0_out out = {};
    float3 _968;
    if (in.input_Lit > 0.0)
    {
        bool _943;
        do
        {
            if (_195.NumCascades > 0.0)
            {
                if (abs(dot(in.input_NormalWorld, _195.LightDirection)) >= 0.0500000007450580596923828125)
                {
                    float4 _615 = in.input_WorldPos * _195.LightViewProj0;
                    float _620 = _615.w;
                    float2 _623 = ((_615.xy * 0.5) / float2(_620)) + float2(0.5);
                    float _626 = 1.0 - _623.y;
                    float2 _974 = _623;
                    _974.y = _626;
                    float _629 = _623.x;
                    float _644 = _615.z;
                    bool _646 = ((((_629 >= 0.0) && (_629 <= 1.0)) && (_626 >= 0.0)) && (_626 <= 1.0)) && (_644 > 0.0);
                    bool _935;
                    if (_646)
                    {
                        float _658 = _644 / _620;
                        float _660 = dfdx(_658);
                        float _662 = dfdy(_658);
                        _935 = ShadowMap0.sample(ShadowMapSampler0, _974).x < (_658 - (_195.DepthBias + fast::clamp(sqrt((_660 * _660) + (_662 * _662)), 0.0, 0.00999999977648258209228515625)));
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
                        float4 _698 = in.input_WorldPos * _195.LightViewProj1;
                        float _703 = _698.w;
                        float2 _706 = ((_698.xy * 0.5) / float2(_703)) + float2(0.5);
                        float _709 = 1.0 - _706.y;
                        float2 _985 = _706;
                        _985.y = _709;
                        float _712 = _706.x;
                        float _727 = _698.z;
                        bool _729 = ((((_712 >= 0.0) && (_712 <= 1.0)) && (_709 >= 0.0)) && (_709 <= 1.0)) && (_727 > 0.0);
                        bool _938;
                        if (_729)
                        {
                            float _741 = _727 / _703;
                            float _743 = dfdx(_741);
                            float _745 = dfdy(_741);
                            _938 = ShadowMap1.sample(ShadowMapSampler1, _985).x < (_741 - (_195.DepthBias + fast::clamp(sqrt((_743 * _743) + (_745 * _745)), 0.0, 0.00999999977648258209228515625)));
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
                            float4 _781 = in.input_WorldPos * _195.LightViewProj2;
                            float _786 = _781.w;
                            float2 _789 = ((_781.xy * 0.5) / float2(_786)) + float2(0.5);
                            float _792 = 1.0 - _789.y;
                            float2 _996 = _789;
                            _996.y = _792;
                            float _795 = _789.x;
                            float _810 = _781.z;
                            bool _812 = ((((_795 >= 0.0) && (_795 <= 1.0)) && (_792 >= 0.0)) && (_792 <= 1.0)) && (_810 > 0.0);
                            bool _941;
                            if (_812)
                            {
                                float _824 = _810 / _786;
                                float _826 = dfdx(_824);
                                float _828 = dfdy(_824);
                                _941 = ShadowMap2.sample(ShadowMapSampler2, _996).x < (_824 - (_195.DepthBias + fast::clamp(sqrt((_826 * _826) + (_828 * _828)), 0.0, 0.00999999977648258209228515625)));
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
        float3 _862 = in.input_Color.xyz * (_195.EnvironmentLight.x + (_195.EnvironmentLight.y * (((in.input_GetsShadowed > 0.0) && _943) ? 0.0 : in.input_Diffuse)));
        _968 = fast::min(_862 + (_862 * ((float3(_195.SnapColor) * 255.0) * float3(0.00999999977648258209228515625))), float3(1.0));
    }
    else
    {
        _968 = in.input_Color.xyz;
    }
    float _885 = exp2(fast::max((in.input_ViewLength - (_195.FogDistance * 0.5)) / _195.FogDistance, 0.0) * _195.FogLogDensity);
    out._entryPointOutput = float4((_968 * float3(_885)) + (float3(_195.FogColor) * float3(1.0 - _885)), in.input_Color.w);
    return out;
}

