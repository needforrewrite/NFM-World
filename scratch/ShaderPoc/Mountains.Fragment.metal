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
};

fragment main0_out main0(main0_in in [[stage_in]], constant _Global& _130 [[buffer(0)]], texture2d<float> ShadowMap0 [[texture(0)]], texture2d<float> ShadowMap1 [[texture(1)]], texture2d<float> ShadowMap2 [[texture(2)]], sampler ShadowMapSampler0 [[sampler(0)]], sampler ShadowMapSampler1 [[sampler(1)]], sampler ShadowMapSampler2 [[sampler(2)]])
{
    main0_out out = {};
    float3 _340 = dfdx(in.input_WorldPos.xyz);
    float3 _344 = dfdy(in.input_WorldPos.xyz);
    float4 _353 = float4(in.input_WorldPos.xyz, 1.0);
    bool _738;
    do
    {
        if (_130.NumCascades > 0.0)
        {
            if (abs(dot(fast::normalize(cross(_340, _344)), _130.LightDirection)) >= 0.0500000007450580596923828125)
            {
                float4 _478 = _353 * _130.LightViewProj0;
                float _483 = _478.w;
                float2 _486 = ((_478.xy * 0.5) / float2(_483)) + float2(0.5);
                float _489 = 1.0 - _486.y;
                float2 _755 = _486;
                _755.y = _489;
                float _492 = _486.x;
                float _507 = _478.z;
                bool _509 = ((((_492 >= 0.0) && (_492 <= 1.0)) && (_489 >= 0.0)) && (_489 <= 1.0)) && (_507 > 0.0);
                bool _730;
                if (_509)
                {
                    float _521 = _507 / _483;
                    float _523 = dfdx(_521);
                    float _525 = dfdy(_521);
                    _730 = ShadowMap0.sample(ShadowMapSampler0, _755).x < (_521 - (_130.DepthBias + fast::clamp(sqrt((_523 * _523) + (_525 * _525)), 0.0, 0.00999999977648258209228515625)));
                }
                else
                {
                    _730 = false;
                }
                if (_509)
                {
                    _738 = _730;
                    break;
                }
                if (_130.NumCascades > 1.0)
                {
                    float4 _561 = _353 * _130.LightViewProj1;
                    float _566 = _561.w;
                    float2 _569 = ((_561.xy * 0.5) / float2(_566)) + float2(0.5);
                    float _572 = 1.0 - _569.y;
                    float2 _766 = _569;
                    _766.y = _572;
                    float _575 = _569.x;
                    float _590 = _561.z;
                    bool _592 = ((((_575 >= 0.0) && (_575 <= 1.0)) && (_572 >= 0.0)) && (_572 <= 1.0)) && (_590 > 0.0);
                    bool _733;
                    if (_592)
                    {
                        float _604 = _590 / _566;
                        float _606 = dfdx(_604);
                        float _608 = dfdy(_604);
                        _733 = ShadowMap1.sample(ShadowMapSampler1, _766).x < (_604 - (_130.DepthBias + fast::clamp(sqrt((_606 * _606) + (_608 * _608)), 0.0, 0.00999999977648258209228515625)));
                    }
                    else
                    {
                        _733 = false;
                    }
                    if (_592)
                    {
                        _738 = _733;
                        break;
                    }
                    if (_130.NumCascades > 2.0)
                    {
                        float4 _644 = _353 * _130.LightViewProj2;
                        float _649 = _644.w;
                        float2 _652 = ((_644.xy * 0.5) / float2(_649)) + float2(0.5);
                        float _655 = 1.0 - _652.y;
                        float2 _777 = _652;
                        _777.y = _655;
                        float _658 = _652.x;
                        float _673 = _644.z;
                        bool _675 = ((((_658 >= 0.0) && (_658 <= 1.0)) && (_655 >= 0.0)) && (_655 <= 1.0)) && (_673 > 0.0);
                        bool _736;
                        if (_675)
                        {
                            float _687 = _673 / _649;
                            float _689 = dfdx(_687);
                            float _691 = dfdy(_687);
                            _736 = ShadowMap2.sample(ShadowMapSampler2, _777).x < (_687 - (_130.DepthBias + fast::clamp(sqrt((_689 * _689) + (_691 * _691)), 0.0, 0.00999999977648258209228515625)));
                        }
                        else
                        {
                            _736 = false;
                        }
                        if (_675)
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
    float3 _750;
    if (_738)
    {
        _750 = in.input_Color.xyz * float3(0.5);
    }
    else
    {
        _750 = in.input_Color.xyz;
    }
    out._entryPointOutput = float4(_750, in.input_Color.w);
    return out;
}

