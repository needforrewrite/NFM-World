cbuffer _Global : register(b0)
{
    column_major float4x4 _195_LightViewProj0 : packoffset(c0);
    column_major float4x4 _195_LightViewProj1 : packoffset(c4);
    column_major float4x4 _195_LightViewProj2 : packoffset(c8);
    float _195_DepthBias : packoffset(c12);
    float _195_NumCascades : packoffset(c12.y);
    float3 _195_LightDirection : packoffset(c13);
    column_major float4x4 _195_View : packoffset(c14);
    column_major float4x4 _195_Projection : packoffset(c18);
    column_major float4x4 _195_ViewProj : packoffset(c22);
    float3 _195_SnapColor : packoffset(c26);
    uint _195_IsFullbright : packoffset(c26.w);
    uint _195_UseBaseColor : packoffset(c27);
    float3 _195_BaseColor : packoffset(c28);
    float3 _195_FogColor : packoffset(c29);
    float _195_FogDistance : packoffset(c29.w);
    float _195_FogLogDensity : packoffset(c30);
    float2 _195_EnvironmentLight : packoffset(c30.z);
    float3 _195_CameraPosition : packoffset(c31);
    float _195_Alpha : packoffset(c31.w);
    uint _195_Expand : packoffset(c32);
    float _195_RandomFloat : packoffset(c32.y);
    float _195_Darken : packoffset(c32.z);
    float _195_ChargedBlinkAmount : packoffset(c32.w);
    float _195_HalfThickness : packoffset(c33);
    float2 _195_Resolution : packoffset(c33.z);
    float _195_DistantOutlineDistanceFalloffWithCutoffMask : packoffset(c34);
    float _195_DistantOutlineClassicCutoffMask : packoffset(c34.y);
    float _195_DistantOutlineDistanceFalloffMask : packoffset(c34.z);
    float _195_OutlineClassicCutoffDistance : packoffset(c34.w);
    float _195_OutlineFalloffStartDistance : packoffset(c35);
    float _195_OutlineFalloffCutoffDistance : packoffset(c35.y);
    float _195_OutlineFalloffLinearFadeStartDistance : packoffset(c35.z);
    float _195_OutlineFalloffLinearFadeStartThickness : packoffset(c35.w);
    float _195_OutlineFalloffInverseLinearFadeLength : packoffset(c36);
};

Texture2D<float4> ShadowMap0 : register(t0);
SamplerState ShadowMapSampler0 : register(s0);
Texture2D<float4> ShadowMap1 : register(t1);
SamplerState ShadowMapSampler1 : register(s1);
Texture2D<float4> ShadowMap2 : register(t2);
SamplerState ShadowMapSampler2 : register(s2);

static float4 input_Color;
static float4 input_WorldPos;
static float input_GetsShadowed;
static float3 input_NormalWorld;
static float input_ViewLength;
static float input_Lit;
static float input_Diffuse;
static float4 _entryPointOutput;

struct SPIRV_Cross_Input
{
    float4 input_Color : TEXCOORD0;
    float4 input_WorldPos : TEXCOORD1;
    float input_GetsShadowed : TEXCOORD2;
    float3 input_NormalWorld : TEXCOORD3;
    float input_ViewLength : TEXCOORD4;
    float input_Lit : TEXCOORD5;
    float input_Diffuse : TEXCOORD6;
};

struct SPIRV_Cross_Output
{
    float4 _entryPointOutput : SV_Target0;
};

void frag_main()
{
    float3 _1051;
    if (input_Lit > 0.0f)
    {
        bool _981;
        do
        {
            if (_195_NumCascades > 0.0f)
            {
                if (abs(dot(input_NormalWorld, _195_LightDirection)) >= 0.0500000007450580596923828125f)
                {
                    float4 _653 = mul(input_WorldPos, _195_LightViewProj0);
                    float _658 = _653.w;
                    float2 _661 = ((_653.xy * 0.5f) / _658.xx) + 0.5f.xx;
                    float _664 = 1.0f - _661.y;
                    float2 _1001 = _661;
                    _1001.y = _664;
                    float _667 = _661.x;
                    float _682 = _653.z;
                    bool _684 = ((((_667 >= 0.0f) && (_667 <= 1.0f)) && (_664 >= 0.0f)) && (_664 <= 1.0f)) && (_682 > 0.0f);
                    bool _973;
                    if (_684)
                    {
                        float _696 = _682 / _658;
                        float _698 = ddx(_696);
                        float _700 = ddy(_696);
                        _973 = ShadowMap0.Sample(ShadowMapSampler0, _1001).x < (_696 - (_195_DepthBias + clamp(sqrt((_698 * _698) + (_700 * _700)), 0.0f, 0.00999999977648258209228515625f)));
                    }
                    else
                    {
                        _973 = false;
                    }
                    if (_684)
                    {
                        _981 = _973;
                        break;
                    }
                    if (_195_NumCascades > 1.0f)
                    {
                        float4 _736 = mul(input_WorldPos, _195_LightViewProj1);
                        float _741 = _736.w;
                        float2 _744 = ((_736.xy * 0.5f) / _741.xx) + 0.5f.xx;
                        float _747 = 1.0f - _744.y;
                        float2 _1012 = _744;
                        _1012.y = _747;
                        float _750 = _744.x;
                        float _765 = _736.z;
                        bool _767 = ((((_750 >= 0.0f) && (_750 <= 1.0f)) && (_747 >= 0.0f)) && (_747 <= 1.0f)) && (_765 > 0.0f);
                        bool _976;
                        if (_767)
                        {
                            float _779 = _765 / _741;
                            float _781 = ddx(_779);
                            float _783 = ddy(_779);
                            _976 = ShadowMap1.Sample(ShadowMapSampler1, _1012).x < (_779 - (_195_DepthBias + clamp(sqrt((_781 * _781) + (_783 * _783)), 0.0f, 0.00999999977648258209228515625f)));
                        }
                        else
                        {
                            _976 = false;
                        }
                        if (_767)
                        {
                            _981 = _976;
                            break;
                        }
                        if (_195_NumCascades > 2.0f)
                        {
                            float4 _819 = mul(input_WorldPos, _195_LightViewProj2);
                            float _824 = _819.w;
                            float2 _827 = ((_819.xy * 0.5f) / _824.xx) + 0.5f.xx;
                            float _830 = 1.0f - _827.y;
                            float2 _1023 = _827;
                            _1023.y = _830;
                            float _833 = _827.x;
                            float _848 = _819.z;
                            bool _850 = ((((_833 >= 0.0f) && (_833 <= 1.0f)) && (_830 >= 0.0f)) && (_830 <= 1.0f)) && (_848 > 0.0f);
                            bool _979;
                            if (_850)
                            {
                                float _862 = _848 / _824;
                                float _864 = ddx(_862);
                                float _866 = ddy(_862);
                                _979 = ShadowMap2.Sample(ShadowMapSampler2, _1023).x < (_862 - (_195_DepthBias + clamp(sqrt((_864 * _864) + (_866 * _866)), 0.0f, 0.00999999977648258209228515625f)));
                            }
                            else
                            {
                                _979 = false;
                            }
                            if (_850)
                            {
                                _981 = _979;
                                break;
                            }
                        }
                    }
                }
            }
            _981 = false;
            break;
        } while(false);
        float3 _900 = input_Color.xyz * (_195_EnvironmentLight.x + (_195_EnvironmentLight.y * (((input_GetsShadowed > 0.0f) && _981) ? 0.0f : input_Diffuse)));
        _1051 = min(_900 + (_900 * ((_195_SnapColor * 255.0f) * 0.00999999977648258209228515625f.xxx)), 1.0f.xxx);
    }
    else
    {
        _1051 = input_Color.xyz;
    }
    float3 _1052;
    if (_195_ChargedBlinkAmount > 0.0f)
    {
        _1052 = float3(_195_ChargedBlinkAmount * 0.10000000894069671630859375f, (128.0f + (12.80000019073486328125f * _195_ChargedBlinkAmount)) * 0.0039215688593685626983642578125f, 1.0f);
    }
    else
    {
        _1052 = _1051;
    }
    float _923 = exp2(max((input_ViewLength - (_195_FogDistance * 0.5f)) / _195_FogDistance, 0.0f) * _195_FogLogDensity);
    _entryPointOutput = float4((_1052 * _923.xxx) + (_195_FogColor * (1.0f - _923).xxx), input_Color.w);
}

SPIRV_Cross_Output main(SPIRV_Cross_Input stage_input)
{
    input_Color = stage_input.input_Color;
    input_WorldPos = stage_input.input_WorldPos;
    input_GetsShadowed = stage_input.input_GetsShadowed;
    input_NormalWorld = stage_input.input_NormalWorld;
    input_ViewLength = stage_input.input_ViewLength;
    input_Lit = stage_input.input_Lit;
    input_Diffuse = stage_input.input_Diffuse;
    frag_main();
    SPIRV_Cross_Output stage_output;
    stage_output._entryPointOutput = _entryPointOutput;
    return stage_output;
}
