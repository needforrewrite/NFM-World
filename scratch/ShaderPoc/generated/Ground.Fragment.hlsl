cbuffer _Global : register(b0)
{
    column_major float4x4 _164_LightViewProj0 : packoffset(c0);
    column_major float4x4 _164_LightViewProj1 : packoffset(c4);
    column_major float4x4 _164_LightViewProj2 : packoffset(c8);
    float _164_DepthBias : packoffset(c12);
    float _164_NumCascades : packoffset(c12.y);
    float3 _164_LightDirection : packoffset(c13);
    column_major float4x4 _164_WorldView : packoffset(c14);
    column_major float4x4 _164_WorldViewProj : packoffset(c18);
    float3 _164_FogColor : packoffset(c22);
    float _164_FogDistance : packoffset(c22.w);
    float _164_FogLogDensity : packoffset(c23);
};

Texture2D<float4> ShadowMap0 : register(t0);
SamplerState ShadowMapSampler0 : register(s0);
Texture2D<float4> ShadowMap1 : register(t1);
SamplerState ShadowMapSampler1 : register(s1);
Texture2D<float4> ShadowMap2 : register(t2);
SamplerState ShadowMapSampler2 : register(s2);

static float4 input_Color;
static float4 input_WorldPos;
static float input_ViewLength;
static float4 _entryPointOutput;

struct SPIRV_Cross_Input
{
    float4 input_Color : TEXCOORD0;
    float4 input_WorldPos : TEXCOORD1;
    float input_ViewLength : TEXCOORD2;
};

struct SPIRV_Cross_Output
{
    float4 _entryPointOutput : SV_Target0;
};

void frag_main()
{
    float _425 = exp2(max((input_ViewLength - (_164_FogDistance * 0.5f)) / _164_FogDistance, 0.0f) * _164_FogLogDensity);
    float3 _439 = (input_Color.xyz * _425.xxx) + (_164_FogColor * (1.0f - _425).xxx);
    float4 _403 = float4(input_WorldPos.xyz, 1.0f);
    bool _817;
    do
    {
        if (_164_NumCascades > 0.0f)
        {
            if (abs(_164_LightDirection.y) >= 0.0500000007450580596923828125f)
            {
                float4 _553 = mul(_403, _164_LightViewProj0);
                float _558 = _553.w;
                float2 _561 = ((_553.xy * 0.5f) / _558.xx) + 0.5f.xx;
                float _564 = 1.0f - _561.y;
                float2 _833 = _561;
                _833.y = _564;
                float _567 = _561.x;
                float _582 = _553.z;
                bool _584 = ((((_567 >= 0.0f) && (_567 <= 1.0f)) && (_564 >= 0.0f)) && (_564 <= 1.0f)) && (_582 > 0.0f);
                bool _809;
                if (_584)
                {
                    float _596 = _582 / _558;
                    float _598 = ddx(_596);
                    float _600 = ddy(_596);
                    _809 = ShadowMap0.Sample(ShadowMapSampler0, _833).x < (_596 - (_164_DepthBias + clamp(sqrt((_598 * _598) + (_600 * _600)), 0.0f, 0.00999999977648258209228515625f)));
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
                if (_164_NumCascades > 1.0f)
                {
                    float4 _636 = mul(_403, _164_LightViewProj1);
                    float _641 = _636.w;
                    float2 _644 = ((_636.xy * 0.5f) / _641.xx) + 0.5f.xx;
                    float _647 = 1.0f - _644.y;
                    float2 _844 = _644;
                    _844.y = _647;
                    float _650 = _644.x;
                    float _665 = _636.z;
                    bool _667 = ((((_650 >= 0.0f) && (_650 <= 1.0f)) && (_647 >= 0.0f)) && (_647 <= 1.0f)) && (_665 > 0.0f);
                    bool _812;
                    if (_667)
                    {
                        float _679 = _665 / _641;
                        float _681 = ddx(_679);
                        float _683 = ddy(_679);
                        _812 = ShadowMap1.Sample(ShadowMapSampler1, _844).x < (_679 - (_164_DepthBias + clamp(sqrt((_681 * _681) + (_683 * _683)), 0.0f, 0.00999999977648258209228515625f)));
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
                    if (_164_NumCascades > 2.0f)
                    {
                        float4 _719 = mul(_403, _164_LightViewProj2);
                        float _724 = _719.w;
                        float2 _727 = ((_719.xy * 0.5f) / _724.xx) + 0.5f.xx;
                        float _730 = 1.0f - _727.y;
                        float2 _855 = _727;
                        _855.y = _730;
                        float _733 = _727.x;
                        float _748 = _719.z;
                        bool _750 = ((((_733 >= 0.0f) && (_733 <= 1.0f)) && (_730 >= 0.0f)) && (_730 <= 1.0f)) && (_748 > 0.0f);
                        bool _815;
                        if (_750)
                        {
                            float _762 = _748 / _724;
                            float _764 = ddx(_762);
                            float _766 = ddy(_762);
                            _815 = ShadowMap2.Sample(ShadowMapSampler2, _855).x < (_762 - (_164_DepthBias + clamp(sqrt((_764 * _764) + (_766 * _766)), 0.0f, 0.00999999977648258209228515625f)));
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
        _829 = _439 * 0.5f.xxx;
    }
    else
    {
        _829 = _439;
    }
    _entryPointOutput = float4(_829, input_Color.w);
}

SPIRV_Cross_Output main(SPIRV_Cross_Input stage_input)
{
    input_Color = stage_input.input_Color;
    input_WorldPos = stage_input.input_WorldPos;
    input_ViewLength = stage_input.input_ViewLength;
    frag_main();
    SPIRV_Cross_Output stage_output;
    stage_output._entryPointOutput = _entryPointOutput;
    return stage_output;
}
