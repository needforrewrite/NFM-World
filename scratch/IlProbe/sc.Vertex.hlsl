cbuffer _Global : register(b0)
{
    column_major float4x4 _108_view_projection : packoffset(c0);
    float2 _108_half_viewport : packoffset(c4);
    float _108_dither_scale : packoffset(c4.z);
    float _108_dither_mode : packoffset(c4.w);
    float2 _108_ramp_texel : packoffset(c5);
    float2 _108_band_tex_size : packoffset(c5.z);
    float2 _108_band_texel : packoffset(c6);
    float2 _108_curve_texel : packoffset(c6.z);
};

Texture2D<float4> TextureTex : register(t0);
SamplerState TextureSampler : register(s0);
Texture2D<float4> BandTex : register(t1);
SamplerState BandSampler : register(s1);
Texture2D<float4> CurveTex : register(t2);
SamplerState CurveSampler : register(s2);
Texture2D<float4> ArcTex : register(t3);
SamplerState ArcSampler : register(s3);
Texture2D<float4> BlueNoiseTex : register(t4);
SamplerState BlueNoiseSampler : register(s4);
Texture2D<float4> RampTex : register(t5);
SamplerState RampSampler : register(s5);

static float4 v_Position;
static float4 v_TexCoord;
static float4 v_FillA;
static float4 v_FillB;
static float4 v_BorderA;
static float4 v_BorderB;
static float4 v_FillCoord;
static float4 v_BorderCoord;
static float4 v_Meta1;
static float4 v_Meta2;
static float4 v_Meta3;
static float4 v_ClipDist;
static float2 v_ClipRoundAA;
static float4 _entryPointOutput_Position;
static float4 _entryPointOutput_TexCoord;
static float4 _entryPointOutput_Fill;
static float4 _entryPointOutput_Border;
static float4 _entryPointOutput_FillCoord;
static float4 _entryPointOutput_BorderCoord;
static float4 _entryPointOutput_Meta1;
static float4 _entryPointOutput_Meta2;
static float4 _entryPointOutput_Meta3;
static float4 _entryPointOutput_Pos;
static float4 _entryPointOutput_ClipMeta;

struct SPIRV_Cross_Input
{
    float4 v_Position : TEXCOORD0;
    float4 v_TexCoord : TEXCOORD1;
    float4 v_FillA : TEXCOORD2;
    float4 v_FillB : TEXCOORD3;
    float4 v_BorderA : TEXCOORD4;
    float4 v_BorderB : TEXCOORD5;
    float4 v_FillCoord : TEXCOORD6;
    float4 v_BorderCoord : TEXCOORD7;
    float4 v_Meta1 : TEXCOORD8;
    float4 v_Meta2 : TEXCOORD9;
    float4 v_Meta3 : TEXCOORD10;
    float4 v_ClipDist : TEXCOORD11;
    float2 v_ClipRoundAA : TEXCOORD12;
};

struct SPIRV_Cross_Output
{
    float4 _entryPointOutput_Position : TEXCOORD0;
    float4 _entryPointOutput_TexCoord : TEXCOORD1;
    float4 _entryPointOutput_Fill : TEXCOORD2;
    float4 _entryPointOutput_Border : TEXCOORD3;
    float4 _entryPointOutput_FillCoord : TEXCOORD4;
    float4 _entryPointOutput_BorderCoord : TEXCOORD5;
    float4 _entryPointOutput_Meta1 : TEXCOORD6;
    float4 _entryPointOutput_Meta2 : TEXCOORD7;
    float4 _entryPointOutput_Meta3 : TEXCOORD8;
    float4 _entryPointOutput_Pos : TEXCOORD9;
    float4 _entryPointOutput_ClipMeta : TEXCOORD10;
};

void vert_main()
{
    float4 _557 = abs(v_FillA);
    float4 _562 = v_FillA * 3.0518509447574615478515625e-05f.xxxx;
    bool4 _564 = any(bool4(_557.x > 1.5f.xxxx.x, _557.y > 1.5f.xxxx.y, _557.z > 1.5f.xxxx.z, _557.w > 1.5f.xxxx.w)).xxxx;
    float4 _565 = float4(_564.x ? _562.x : v_FillA.x, _564.y ? _562.y : v_FillA.y, _564.z ? _562.z : v_FillA.z, _564.w ? _562.w : v_FillA.w);
    float4 _569 = abs(v_FillB);
    float4 _574 = v_FillB * 3.0518509447574615478515625e-05f.xxxx;
    bool4 _576 = any(bool4(_569.x > 1.5f.xxxx.x, _569.y > 1.5f.xxxx.y, _569.z > 1.5f.xxxx.z, _569.w > 1.5f.xxxx.w)).xxxx;
    float4 _577 = float4(_576.x ? _574.x : v_FillB.x, _576.y ? _574.y : v_FillB.y, _576.z ? _574.z : v_FillB.z, _576.w ? _574.w : v_FillB.w);
    float4 _581 = abs(v_BorderA);
    float4 _586 = v_BorderA * 3.0518509447574615478515625e-05f.xxxx;
    bool4 _588 = any(bool4(_581.x > 1.5f.xxxx.x, _581.y > 1.5f.xxxx.y, _581.z > 1.5f.xxxx.z, _581.w > 1.5f.xxxx.w)).xxxx;
    float4 _589 = float4(_588.x ? _586.x : v_BorderA.x, _588.y ? _586.y : v_BorderA.y, _588.z ? _586.z : v_BorderA.z, _588.w ? _586.w : v_BorderA.w);
    float4 _593 = abs(v_BorderB);
    float4 _598 = v_BorderB * 3.0518509447574615478515625e-05f.xxxx;
    bool4 _600 = any(bool4(_593.x > 1.5f.xxxx.x, _593.y > 1.5f.xxxx.y, _593.z > 1.5f.xxxx.z, _593.w > 1.5f.xxxx.w)).xxxx;
    float4 _601 = float4(_600.x ? _598.x : v_BorderB.x, _600.y ? _598.y : v_BorderB.y, _600.z ? _598.z : v_BorderB.z, _600.w ? _598.w : v_BorderB.w);
    float4 _424 = abs(_565);
    float4 _426 = abs(_577);
    float _644 = (floor((_424.x * 2047.0f) + 0.5f) * 2048.0f) + floor((_424.y * 2047.0f) + 0.5f);
    float4 _632 = float4(_644, (floor((_424.z * 2047.0f) + 0.5f) * 2048.0f) + floor((_424.w * 2047.0f) + 0.5f), (floor((_426.x * 2047.0f) + 0.5f) * 2048.0f) + floor((_426.y * 2047.0f) + 0.5f), (floor((_426.z * 2047.0f) + 0.5f) * 2048.0f) + floor((_426.w * 2047.0f) + 0.5f));
    float4 _430 = abs(_589);
    float4 _432 = abs(_601);
    float _723 = (floor((_430.x * 2047.0f) + 0.5f) * 2048.0f) + floor((_430.y * 2047.0f) + 0.5f);
    float4 _711 = float4(_723, (floor((_430.z * 2047.0f) + 0.5f) * 2048.0f) + floor((_430.w * 2047.0f) + 0.5f), (floor((_432.x * 2047.0f) + 0.5f) * 2048.0f) + floor((_432.y * 2047.0f) + 0.5f), (floor((_432.z * 2047.0f) + 0.5f) * 2048.0f) + floor((_432.w * 2047.0f) + 0.5f));
    float4 _926;
    if (_565.x < 0.0f)
    {
        float4 _897 = _632;
        _897.x = (-1.0f) - _644;
        _926 = _897;
    }
    else
    {
        _926 = _632;
    }
    float4 _927;
    if (_565.z < 0.0f)
    {
        float4 _901 = _926;
        _901.y = (-1.0f) - _926.y;
        _927 = _901;
    }
    else
    {
        _927 = _926;
    }
    float4 _928;
    if (_577.x < 0.0f)
    {
        float4 _905 = _927;
        _905.z = (-1.0f) - _927.z;
        _928 = _905;
    }
    else
    {
        _928 = _927;
    }
    float4 _941;
    if (_577.z < 0.0f)
    {
        float4 _909 = _928;
        _909.w = (-1.0f) - _928.w;
        _941 = _909;
    }
    else
    {
        _941 = _928;
    }
    float4 _933;
    if (_589.x < 0.0f)
    {
        float4 _913 = _711;
        _913.x = (-1.0f) - _723;
        _933 = _913;
    }
    else
    {
        _933 = _711;
    }
    float4 _934;
    if (_589.z < 0.0f)
    {
        float4 _917 = _933;
        _917.y = (-1.0f) - _933.y;
        _934 = _917;
    }
    else
    {
        _934 = _933;
    }
    float4 _935;
    if (_601.x < 0.0f)
    {
        float4 _921 = _934;
        _921.z = (-1.0f) - _934.z;
        _935 = _921;
    }
    else
    {
        _935 = _934;
    }
    float4 _936;
    if (_601.z < 0.0f)
    {
        float4 _925 = _935;
        _925.w = (-1.0f) - _935.w;
        _936 = _925;
    }
    else
    {
        _936 = _935;
    }
    _entryPointOutput_Position = mul(v_Position, _108_view_projection);
    _entryPointOutput_TexCoord = v_TexCoord;
    _entryPointOutput_Fill = _941;
    _entryPointOutput_Border = _936;
    _entryPointOutput_FillCoord = v_FillCoord;
    _entryPointOutput_BorderCoord = v_BorderCoord;
    _entryPointOutput_Meta1 = v_Meta1;
    _entryPointOutput_Meta2 = v_Meta2;
    _entryPointOutput_Meta3 = v_Meta3;
    _entryPointOutput_Pos = float4(v_Position.xy, v_ClipDist.xy);
    _entryPointOutput_ClipMeta = float4(v_ClipDist.zw, v_ClipRoundAA);
}

SPIRV_Cross_Output main(SPIRV_Cross_Input stage_input)
{
    v_Position = stage_input.v_Position;
    v_TexCoord = stage_input.v_TexCoord;
    v_FillA = stage_input.v_FillA;
    v_FillB = stage_input.v_FillB;
    v_BorderA = stage_input.v_BorderA;
    v_BorderB = stage_input.v_BorderB;
    v_FillCoord = stage_input.v_FillCoord;
    v_BorderCoord = stage_input.v_BorderCoord;
    v_Meta1 = stage_input.v_Meta1;
    v_Meta2 = stage_input.v_Meta2;
    v_Meta3 = stage_input.v_Meta3;
    v_ClipDist = stage_input.v_ClipDist;
    v_ClipRoundAA = stage_input.v_ClipRoundAA;
    vert_main();
    SPIRV_Cross_Output stage_output;
    stage_output._entryPointOutput_Position = _entryPointOutput_Position;
    stage_output._entryPointOutput_TexCoord = _entryPointOutput_TexCoord;
    stage_output._entryPointOutput_Fill = _entryPointOutput_Fill;
    stage_output._entryPointOutput_Border = _entryPointOutput_Border;
    stage_output._entryPointOutput_FillCoord = _entryPointOutput_FillCoord;
    stage_output._entryPointOutput_BorderCoord = _entryPointOutput_BorderCoord;
    stage_output._entryPointOutput_Meta1 = _entryPointOutput_Meta1;
    stage_output._entryPointOutput_Meta2 = _entryPointOutput_Meta2;
    stage_output._entryPointOutput_Meta3 = _entryPointOutput_Meta3;
    stage_output._entryPointOutput_Pos = _entryPointOutput_Pos;
    stage_output._entryPointOutput_ClipMeta = _entryPointOutput_ClipMeta;
    return stage_output;
}
