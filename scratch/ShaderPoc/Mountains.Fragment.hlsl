cbuffer _Global : register(b0)
{
    column_major float4x4 _130_LightViewProj0 : packoffset(c0);
    column_major float4x4 _130_LightViewProj1 : packoffset(c4);
    column_major float4x4 _130_LightViewProj2 : packoffset(c8);
    float _130_DepthBias : packoffset(c12);
    float _130_NumCascades : packoffset(c12.y);
    float3 _130_LightDirection : packoffset(c13);
    column_major float4x4 _130_WorldView : packoffset(c14);
    column_major float4x4 _130_WorldViewProj : packoffset(c18);
    float3 _130_FogColor : packoffset(c22);
    float _130_FogDistance : packoffset(c22.w);
    float _130_FogLogDensity : packoffset(c23);
};

Texture2D<float4> ShadowMap0 : register(t0);
SamplerState ShadowMapSampler0 : register(s0);
Texture2D<float4> ShadowMap1 : register(t1);
SamplerState ShadowMapSampler1 : register(s1);
Texture2D<float4> ShadowMap2 : register(t2);
SamplerState ShadowMapSampler2 : register(s2);

static float4 input_Color;
static float4 input_WorldPos;
static float4 _entryPointOutput;

struct SPIRV_Cross_Input
{
    float4 input_Color : TEXCOORD0;
    float4 input_WorldPos : TEXCOORD1;
};

struct SPIRV_Cross_Output
{
    float4 _entryPointOutput : SV_Target0;
};

void frag_main()
{
    float3 _340 = ddx(input_WorldPos.xyz);
    float3 _344 = ddy(input_WorldPos.xyz);
    float4 _353 = float4(input_WorldPos.xyz, 1.0f);
    bool _738;
    do
    {
        if (_130_NumCascades > 0.0f)
        {
            if (abs(dot(normalize(cross(_340, _344)), _130_LightDirection)) >= 0.0500000007450580596923828125f)
            {
                float4 _478 = mul(_353, _130_LightViewProj0);
                float _483 = _478.w;
                float2 _486 = ((_478.xy * 0.5f) / _483.xx) + 0.5f.xx;
                float _489 = 1.0f - _486.y;
                float2 _755 = _486;
                _755.y = _489;
                float _492 = _486.x;
                float _507 = _478.z;
                bool _509 = ((((_492 >= 0.0f) && (_492 <= 1.0f)) && (_489 >= 0.0f)) && (_489 <= 1.0f)) && (_507 > 0.0f);
                bool _730;
                if (_509)
                {
                    float _521 = _507 / _483;
                    float _523 = ddx(_521);
                    float _525 = ddy(_521);
                    _730 = ShadowMap0.Sample(ShadowMapSampler0, _755).x < (_521 - (_130_DepthBias + clamp(sqrt((_523 * _523) + (_525 * _525)), 0.0f, 0.00999999977648258209228515625f)));
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
                if (_130_NumCascades > 1.0f)
                {
                    float4 _561 = mul(_353, _130_LightViewProj1);
                    float _566 = _561.w;
                    float2 _569 = ((_561.xy * 0.5f) / _566.xx) + 0.5f.xx;
                    float _572 = 1.0f - _569.y;
                    float2 _766 = _569;
                    _766.y = _572;
                    float _575 = _569.x;
                    float _590 = _561.z;
                    bool _592 = ((((_575 >= 0.0f) && (_575 <= 1.0f)) && (_572 >= 0.0f)) && (_572 <= 1.0f)) && (_590 > 0.0f);
                    bool _733;
                    if (_592)
                    {
                        float _604 = _590 / _566;
                        float _606 = ddx(_604);
                        float _608 = ddy(_604);
                        _733 = ShadowMap1.Sample(ShadowMapSampler1, _766).x < (_604 - (_130_DepthBias + clamp(sqrt((_606 * _606) + (_608 * _608)), 0.0f, 0.00999999977648258209228515625f)));
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
                    if (_130_NumCascades > 2.0f)
                    {
                        float4 _644 = mul(_353, _130_LightViewProj2);
                        float _649 = _644.w;
                        float2 _652 = ((_644.xy * 0.5f) / _649.xx) + 0.5f.xx;
                        float _655 = 1.0f - _652.y;
                        float2 _777 = _652;
                        _777.y = _655;
                        float _658 = _652.x;
                        float _673 = _644.z;
                        bool _675 = ((((_658 >= 0.0f) && (_658 <= 1.0f)) && (_655 >= 0.0f)) && (_655 <= 1.0f)) && (_673 > 0.0f);
                        bool _736;
                        if (_675)
                        {
                            float _687 = _673 / _649;
                            float _689 = ddx(_687);
                            float _691 = ddy(_687);
                            _736 = ShadowMap2.Sample(ShadowMapSampler2, _777).x < (_687 - (_130_DepthBias + clamp(sqrt((_689 * _689) + (_691 * _691)), 0.0f, 0.00999999977648258209228515625f)));
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
        _750 = input_Color.xyz * 0.5f.xxx;
    }
    else
    {
        _750 = input_Color.xyz;
    }
    _entryPointOutput = float4(_750, input_Color.w);
}

SPIRV_Cross_Output main(SPIRV_Cross_Input stage_input)
{
    input_Color = stage_input.input_Color;
    input_WorldPos = stage_input.input_WorldPos;
    frag_main();
    SPIRV_Cross_Output stage_output;
    stage_output._entryPointOutput = _entryPointOutput;
    return stage_output;
}
