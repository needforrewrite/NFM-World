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
    float3 _968;
    if (input_Lit > 0.0f)
    {
        bool _943;
        do
        {
            if (_195_NumCascades > 0.0f)
            {
                if (abs(dot(input_NormalWorld, _195_LightDirection)) >= 0.0500000007450580596923828125f)
                {
                    float4 _615 = mul(input_WorldPos, _195_LightViewProj0);
                    float _620 = _615.w;
                    float2 _623 = ((_615.xy * 0.5f) / _620.xx) + 0.5f.xx;
                    float _626 = 1.0f - _623.y;
                    float2 _974 = _623;
                    _974.y = _626;
                    float _629 = _623.x;
                    float _644 = _615.z;
                    bool _646 = ((((_629 >= 0.0f) && (_629 <= 1.0f)) && (_626 >= 0.0f)) && (_626 <= 1.0f)) && (_644 > 0.0f);
                    bool _935;
                    if (_646)
                    {
                        float _658 = _644 / _620;
                        float _660 = ddx(_658);
                        float _662 = ddy(_658);
                        _935 = ShadowMap0.Sample(ShadowMapSampler0, _974).x < (_658 - (_195_DepthBias + clamp(sqrt((_660 * _660) + (_662 * _662)), 0.0f, 0.00999999977648258209228515625f)));
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
                    if (_195_NumCascades > 1.0f)
                    {
                        float4 _698 = mul(input_WorldPos, _195_LightViewProj1);
                        float _703 = _698.w;
                        float2 _706 = ((_698.xy * 0.5f) / _703.xx) + 0.5f.xx;
                        float _709 = 1.0f - _706.y;
                        float2 _985 = _706;
                        _985.y = _709;
                        float _712 = _706.x;
                        float _727 = _698.z;
                        bool _729 = ((((_712 >= 0.0f) && (_712 <= 1.0f)) && (_709 >= 0.0f)) && (_709 <= 1.0f)) && (_727 > 0.0f);
                        bool _938;
                        if (_729)
                        {
                            float _741 = _727 / _703;
                            float _743 = ddx(_741);
                            float _745 = ddy(_741);
                            _938 = ShadowMap1.Sample(ShadowMapSampler1, _985).x < (_741 - (_195_DepthBias + clamp(sqrt((_743 * _743) + (_745 * _745)), 0.0f, 0.00999999977648258209228515625f)));
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
                        if (_195_NumCascades > 2.0f)
                        {
                            float4 _781 = mul(input_WorldPos, _195_LightViewProj2);
                            float _786 = _781.w;
                            float2 _789 = ((_781.xy * 0.5f) / _786.xx) + 0.5f.xx;
                            float _792 = 1.0f - _789.y;
                            float2 _996 = _789;
                            _996.y = _792;
                            float _795 = _789.x;
                            float _810 = _781.z;
                            bool _812 = ((((_795 >= 0.0f) && (_795 <= 1.0f)) && (_792 >= 0.0f)) && (_792 <= 1.0f)) && (_810 > 0.0f);
                            bool _941;
                            if (_812)
                            {
                                float _824 = _810 / _786;
                                float _826 = ddx(_824);
                                float _828 = ddy(_824);
                                _941 = ShadowMap2.Sample(ShadowMapSampler2, _996).x < (_824 - (_195_DepthBias + clamp(sqrt((_826 * _826) + (_828 * _828)), 0.0f, 0.00999999977648258209228515625f)));
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
        float3 _862 = input_Color.xyz * (_195_EnvironmentLight.x + (_195_EnvironmentLight.y * (((input_GetsShadowed > 0.0f) && _943) ? 0.0f : input_Diffuse)));
        _968 = min(_862 + (_862 * ((_195_SnapColor * 255.0f) * 0.00999999977648258209228515625f.xxx)), 1.0f.xxx);
    }
    else
    {
        _968 = input_Color.xyz;
    }
    float _885 = exp2(max((input_ViewLength - (_195_FogDistance * 0.5f)) / _195_FogDistance, 0.0f) * _195_FogLogDensity);
    _entryPointOutput = float4((_968 * _885.xxx) + (_195_FogColor * (1.0f - _885).xxx), input_Color.w);
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
