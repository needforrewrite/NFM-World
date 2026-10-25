static float2 _43143;
static float _46115;
static float _47722;

cbuffer _Global : register(b0)
{
    column_major float4x4 _8606_view_projection : packoffset(c0);
    float2 _8606_half_viewport : packoffset(c4);
    float _8606_dither_scale : packoffset(c4.z);
    float _8606_dither_mode : packoffset(c4.w);
    float2 _8606_ramp_texel : packoffset(c5);
    float2 _8606_band_tex_size : packoffset(c5.z);
    float2 _8606_band_texel : packoffset(c6);
    float2 _8606_curve_texel : packoffset(c6.z);
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

static float4 p_TexCoord;
static float4 p_Fill;
static float4 p_Border;
static float4 p_FillCoord;
static float4 p_BorderCoord;
static float4 p_Meta1;
static float4 p_Meta2;
static float4 p_Meta3;
static float4 p_Pos;
static float4 p_ClipMeta;
static float4 _entryPointOutput;

struct SPIRV_Cross_Input
{
    float4 p_TexCoord : TEXCOORD1;
    float4 p_Fill : TEXCOORD2;
    float4 p_Border : TEXCOORD3;
    float4 p_FillCoord : TEXCOORD4;
    float4 p_BorderCoord : TEXCOORD5;
    float4 p_Meta1 : TEXCOORD6;
    float4 p_Meta2 : TEXCOORD7;
    float4 p_Meta3 : TEXCOORD8;
    float4 p_Pos : TEXCOORD9;
    float4 p_ClipMeta : TEXCOORD10;
};

struct SPIRV_Cross_Output
{
    float4 _entryPointOutput : SV_Target0;
};

float mod(float x, float y)
{
    return x - y * floor(x / y);
}

float2 mod(float2 x, float2 y)
{
    return x - y * floor(x / y);
}

float3 mod(float3 x, float3 y)
{
    return x - y * floor(x / y);
}

float4 mod(float4 x, float4 y)
{
    return x - y * floor(x / y);
}

void frag_main()
{
    float4 _47662;
    do
    {
        float _12863 = abs(p_Meta1.y);
        float _12867 = (p_Meta1.y < 0.0f) ? 0.5f : 0.0f;
        float _14563 = floor(p_TexCoord.w * 0.0625f);
        float _14568 = p_TexCoord.w - (_14563 * 16.0f);
        float _42685;
        float _42687;
        if (_14568 >= 16.0f)
        {
            _42687 = _14568 - 16.0f;
            _42685 = _14563 + 1.0f;
        }
        else
        {
            float _42686;
            float _42688;
            if (_14568 < 0.0f)
            {
                _42688 = _14568 + 16.0f;
                _42686 = _14563 - 1.0f;
            }
            else
            {
                _42688 = _14568;
                _42686 = _14563;
            }
            _42687 = _42688;
            _42685 = _42686;
        }
        float _14598 = floor(_42685 * 0.0625f);
        float _14603 = _42685 - (_14598 * 16.0f);
        float _42689;
        float _42691;
        if (_14603 >= 16.0f)
        {
            _42691 = _14603 - 16.0f;
            _42689 = _14598 + 1.0f;
        }
        else
        {
            float _42690;
            float _42692;
            if (_14603 < 0.0f)
            {
                _42692 = _14603 + 16.0f;
                _42690 = _14598 - 1.0f;
            }
            else
            {
                _42692 = _14603;
                _42690 = _14598;
            }
            _42691 = _42692;
            _42689 = _42690;
        }
        float _14633 = floor(_42689 * 0.25f);
        float _14638 = _42689 - (_14633 * 4.0f);
        float _42693;
        float _42695;
        if (_14638 >= 4.0f)
        {
            _42695 = _14638 - 4.0f;
            _42693 = _14633 + 1.0f;
        }
        else
        {
            float _42694;
            float _42696;
            if (_14638 < 0.0f)
            {
                _42696 = _14638 + 4.0f;
                _42694 = _14633 - 1.0f;
            }
            else
            {
                _42696 = _14638;
                _42694 = _14633;
            }
            _42695 = _42696;
            _42693 = _42694;
        }
        float2 _51240 = float2(_42691, _42695);
        float _14668 = floor(_42693 * 0.0625f);
        float _14673 = _42693 - (_14668 * 16.0f);
        float _42697;
        float _42699;
        if (_14673 >= 16.0f)
        {
            _42699 = _14673 - 16.0f;
            _42697 = _14668 + 1.0f;
        }
        else
        {
            float _42698;
            float _42700;
            if (_14673 < 0.0f)
            {
                _42700 = _14673 + 16.0f;
                _42698 = _14668 - 1.0f;
            }
            else
            {
                _42700 = _14673;
                _42698 = _14668;
            }
            _42699 = _42700;
            _42697 = _42698;
        }
        float _14703 = floor(_42697 * 0.25f);
        float _14708 = _42697 - (_14703 * 4.0f);
        float _42701;
        float _42703;
        if (_14708 >= 4.0f)
        {
            _42703 = _14708 - 4.0f;
            _42701 = _14703 + 1.0f;
        }
        else
        {
            float _42702;
            float _42704;
            if (_14708 < 0.0f)
            {
                _42704 = _14708 + 4.0f;
                _42702 = _14703 - 1.0f;
            }
            else
            {
                _42704 = _14708;
                _42702 = _14703;
            }
            _42703 = _42704;
            _42701 = _42702;
        }
        float2 _51241 = float2(_42699, _42703);
        float _14738 = floor(_42701 * 0.25f);
        float _14743 = _42701 - (_14738 * 4.0f);
        float _42705;
        float _42707;
        if (_14743 >= 4.0f)
        {
            _42707 = _14743 - 4.0f;
            _42705 = _14738 + 1.0f;
        }
        else
        {
            float _42706;
            float _42708;
            if (_14743 < 0.0f)
            {
                _42708 = _14743 + 4.0f;
                _42706 = _14738 - 1.0f;
            }
            else
            {
                _42708 = _14743;
                _42706 = _14738;
            }
            _42707 = _42708;
            _42705 = _42706;
        }
        float _14773 = floor(_42705 * 0.25f);
        float _14778 = _42705 - (_14773 * 4.0f);
        float _42709;
        float _42711;
        if (_14778 >= 4.0f)
        {
            _42711 = _14778 - 4.0f;
            _42709 = _14773 + 1.0f;
        }
        else
        {
            float _42710;
            float _42712;
            if (_14778 < 0.0f)
            {
                _42712 = _14778 + 4.0f;
                _42710 = _14773 - 1.0f;
            }
            else
            {
                _42712 = _14778;
                _42710 = _14773;
            }
            _42711 = _42712;
            _42709 = _42710;
        }
        float2 _14810 = ddx(p_Pos.xy);
        float2 _14812 = ddy(p_Pos.xy);
        float _14819 = dot(_14810, _14810) + dot(_14812, _14812);
        float _14831 = abs((_14810.x * _14812.y) - (_14810.y * _14812.x));
        float _14846 = sqrt(0.5f * (_14819 + sqrt(max((_14819 * _14819) - ((4.0f * _14831) * _14831), 0.0f))));
        float _14849 = max(_14846, 9.9999999600419720025001879548654e-13f);
        float _14850 = _14831 / _14849;
        float2 _12912 = p_ClipMeta.z.xx - min(p_Pos.zw, p_ClipMeta.xy);
        float _12925 = (length(max(_12912, 0.0f.xx)) + min(max(_12912.x, _12912.y), 0.0f)) - p_ClipMeta.z;
        float _14859 = ddx(_12925);
        float _14861 = ddy(_12925);
        float _14865 = abs(_14859);
        float _14868 = abs(_14861);
        float _14874 = clamp(max(_14865, _14868) / _14849, 0.0f, 1.0f);
        bool _14876 = _12867 > 0.0f;
        float _14906 = _14846 * (_14876 ? 1.41421353816986083984375f : 1.0f);
        float _12932 = clamp(_14876 ? min(_14865 + _14868, _14846 * (_14874 + sqrt(1.0f - (_14874 * _14874)))) : length(float2(_14859, _14861)), _14850, _14906) * p_ClipMeta.w;
        float _12936 = 1.0f - _12867;
        if (_12925 >= (_12932 * _12936))
        {
            discard;
        }
        float _12948 = 1.0f - smoothstep(0.0f, 1.0f, clamp((_12925 / _12932) + _12867, 0.0f, 1.0f));
        if ((_42687 >= 8.5f) && (_42687 < 9.5f))
        {
            float4 _14914 = TextureTex.Sample(TextureSampler, p_TexCoord.xy);
            float _14950 = floor(p_Fill.x * 0.00048828125f);
            float _14955 = p_Fill.x - (_14950 * 2048.0f);
            float _47654;
            float _47656;
            if (_14955 >= 2048.0f)
            {
                _47656 = _14955 - 2048.0f;
                _47654 = _14950 + 1.0f;
            }
            else
            {
                float _47655;
                float _47657;
                if (_14955 < 0.0f)
                {
                    _47657 = _14955 + 2048.0f;
                    _47655 = _14950 - 1.0f;
                }
                else
                {
                    _47657 = _14955;
                    _47655 = _14950;
                }
                _47656 = _47657;
                _47654 = _47655;
            }
            float _14998 = floor(p_Fill.y * 0.00048828125f);
            float _15003 = p_Fill.y - (_14998 * 2048.0f);
            float _47658;
            float _47660;
            if (_15003 >= 2048.0f)
            {
                _47660 = _15003 - 2048.0f;
                _47658 = _14998 + 1.0f;
            }
            else
            {
                float _47659;
                float _47661;
                if (_15003 < 0.0f)
                {
                    _47661 = _15003 + 2048.0f;
                    _47659 = _14998 - 1.0f;
                }
                else
                {
                    _47661 = _15003;
                    _47659 = _14998;
                }
                _47660 = _47661;
                _47658 = _47659;
            }
            _47662 = (_14914 * float4(float2(_47654, _47656) * 0.000488519784994423389434814453125f.xx, float2(_47658, _47660) * 0.000488519784994423389434814453125f.xx)) * _12948;
            break;
        }
        float2 _12969 = p_TexCoord.xy;
        bool _12976 = (_42687 >= 12.5f) && (_42687 < 14.5f);
        float _12978 = step(13.5f, _42687);
        float _43339;
        float _43465;
        float _43537;
        bool _43633;
        float _43790;
        float2 _43947;
        float _44113;
        float _44272;
        float _44433;
        float _44650;
        float _44741;
        float2 _44843;
        float2 _44962;
        float2 _45081;
        float2 _45200;
        float _45319;
        float4 _45438;
        float4 _45559;
        float4 _45687;
        float4 _45810;
        bool _45936;
        float2 _50476;
        if (_12976)
        {
            float _12987 = p_Meta2.y - 1.0f;
            float2 _12999 = (_12969 * p_BorderCoord.xy) + p_BorderCoord.zw;
            float _13012 = p_Meta2.x + clamp(floor(_12999.y), 0.0f, _12987);
            float4 _15068 = BandTex.Sample(BandSampler, (float2(_13012 - (_8606_band_tex_size.x * floor(_13012 / _8606_band_tex_size.x)), floor(_13012 * _8606_band_texel.x)) + 0.5f.xx) * _8606_band_texel);
            float _13015 = _15068.x;
            float _13017 = _15068.y;
            float _43334;
            float _43337;
            _43337 = 0.0f;
            _43334 = 0.0f;
            for (int _43331 = 0; _43331 < 16; )
            {
                float _13025 = float(_43331);
                float _13026 = _13017 + _13025;
                float4 _15111 = BandTex.Sample(BandSampler, (float2(_13026 - (_8606_band_tex_size.x * floor(_13026 / _8606_band_tex_size.x)), floor(_13026 * _8606_band_texel.x)) + 0.5f.xx) * _8606_band_texel);
                float4 _13036 = CurveTex.Sample(CurveSampler, (_15111.xy + 0.5f.xx) * _8606_curve_texel) - float4(p_TexCoord.xyxy);
                float2 _13046 = CurveTex.Sample(CurveSampler, (float2(_15111.x + 1.0f, _15111.y) + 0.5f.xx) * _8606_curve_texel).xy - _12969;
                float _13053 = _13036.y;
                float _15157 = step(_13053, 0.0f);
                float _15158 = 1.0f - _15157;
                float _15160 = step(_13036.w, 0.0f);
                float _15161 = 1.0f - _15160;
                float _15163 = step(_13046.y, 0.0f);
                float _15164 = 1.0f - _15163;
                float _15173 = _15158 * _15160;
                float _15178 = _15157 * _15161;
                float _15192 = _15173 * _15164;
                float2 _13060 = float2(clamp((_15163 * ((_15173 + _15178) + (_15158 * _15161))) + _15192, 0.0f, 1.0f), clamp((((_15178 * _15163) + ((_15157 * _15160) * _15164)) + _15192) + (_15178 * _15164), 0.0f, 1.0f)) * step(_13025 + 0.5f, _13015);
                float2 _15238 = _13036.xy;
                float2 _15240 = _13036.zw;
                float2 _15244 = (_15238 - (_15240 * 2.0f)) + _13046;
                float2 _15249 = _15238 - _15240;
                float _15335 = abs(_15244.y);
                float _15253 = 1.0f / (_15244.y + (((step(0.0f, _15244.y) * 2.0f) - 1.0f) * max(9.999999717180685365747194737196e-10f - _15335, 0.0f)));
                float _15255 = _15249.y;
                float _15270 = sqrt(max((_15255 * _15255) - (_15244.y * _13053), 0.0f));
                float _15274 = step(_15335, 9.9999997473787516355514526367188e-05f);
                float _15278 = _13053 * (0.5f / (_15255 + (((step(0.0f, _15255) * 2.0f) - 1.0f) * max(9.999999717180685365747194737196e-10f - abs(_15255), 0.0f))));
                float _15287 = lerp((_15255 - _15270) * _15253, _15278, _15274);
                float _15296 = lerp((_15255 + _15270) * _15253, _15278, _15274);
                float _15303 = _15249.x * 2.0f;
                float _15308 = _13036.x;
                float2 _13066 = float2((((_15244.x * _15287) - _15303) * _15287) + _15308, (((_15244.x * _15296) - _15303) * _15296) + _15308) * p_Meta2.z;
                float _13068 = _13060.x;
                float _13070 = _13066.x;
                float _13075 = _13060.y;
                float _13077 = _13066.y;
                _43337 = max(max(_43337, _13068 * clamp(1.0f - (abs(_13070) * 2.0f), 0.0f, 1.0f)), _13075 * clamp(1.0f - (abs(_13077) * 2.0f), 0.0f, 1.0f));
                _43334 += ((_13068 * clamp(_13070 + 0.5f, 0.0f, 1.0f)) - (_13075 * clamp(_13077 + 0.5f, 0.0f, 1.0f)));
                _43331++;
                continue;
            }
            float _13114 = (p_Meta2.x + p_Meta2.y) + clamp(floor(_12999.x), 0.0f, _12987);
            float4 _15398 = BandTex.Sample(BandSampler, (float2(_13114 - (_8606_band_tex_size.x * floor(_13114 / _8606_band_tex_size.x)), floor(_13114 * _8606_band_texel.x)) + 0.5f.xx) * _8606_band_texel);
            float _13117 = _15398.x;
            float _13119 = _15398.y;
            float _43335;
            float _43338;
            _43338 = 0.0f;
            _43335 = 0.0f;
            for (int _43332 = 0; _43332 < 16; )
            {
                float _13127 = float(_43332);
                float _13128 = _13119 + _13127;
                float4 _15441 = BandTex.Sample(BandSampler, (float2(_13128 - (_8606_band_tex_size.x * floor(_13128 / _8606_band_tex_size.x)), floor(_13128 * _8606_band_texel.x)) + 0.5f.xx) * _8606_band_texel);
                float4 _13158 = float4(CurveTex.Sample(CurveSampler, (_15441.xy + 0.5f.xx) * _8606_curve_texel).yxwz) - float4(p_TexCoord.yxyx);
                float2 _13163 = CurveTex.Sample(CurveSampler, (float2(_15441.x + 1.0f, _15441.y) + 0.5f.xx) * _8606_curve_texel).yx - p_TexCoord.yx;
                float _13170 = _13158.y;
                float _15487 = step(_13170, 0.0f);
                float _15488 = 1.0f - _15487;
                float _15490 = step(_13158.w, 0.0f);
                float _15491 = 1.0f - _15490;
                float _15493 = step(_13163.y, 0.0f);
                float _15494 = 1.0f - _15493;
                float _15503 = _15488 * _15490;
                float _15508 = _15487 * _15491;
                float _15522 = _15503 * _15494;
                float2 _13177 = float2(clamp((_15493 * ((_15503 + _15508) + (_15488 * _15491))) + _15522, 0.0f, 1.0f), clamp((((_15508 * _15493) + ((_15487 * _15490) * _15494)) + _15522) + (_15508 * _15494), 0.0f, 1.0f)) * step(_13127 + 0.5f, _13117);
                float2 _15568 = _13158.xy;
                float2 _15570 = _13158.zw;
                float2 _15574 = (_15568 - (_15570 * 2.0f)) + _13163;
                float2 _15579 = _15568 - _15570;
                float _15581 = _15574.y;
                float _15665 = abs(_15581);
                float _15583 = 1.0f / (_15581 + (((step(0.0f, _15581) * 2.0f) - 1.0f) * max(9.999999717180685365747194737196e-10f - _15665, 0.0f)));
                float _15585 = _15579.y;
                float _15600 = sqrt(max((_15585 * _15585) - (_15581 * _13170), 0.0f));
                float _15604 = step(_15665, 9.9999997473787516355514526367188e-05f);
                float _15608 = _13170 * (0.5f / (_15585 + (((step(0.0f, _15585) * 2.0f) - 1.0f) * max(9.999999717180685365747194737196e-10f - abs(_15585), 0.0f))));
                float _15617 = lerp((_15585 - _15600) * _15583, _15608, _15604);
                float _15626 = lerp((_15585 + _15600) * _15583, _15608, _15604);
                float _15628 = _15574.x;
                float _15633 = _15579.x * 2.0f;
                float _15638 = _13158.x;
                float2 _13183 = float2((((_15628 * _15617) - _15633) * _15617) + _15638, (((_15628 * _15626) - _15633) * _15626) + _15638) * p_Meta2.w;
                float _13185 = _13177.x;
                float _13187 = _13183.x;
                float _13192 = _13177.y;
                float _13194 = _13183.y;
                _43338 = max(max(_43338, _13185 * clamp(1.0f - (abs(_13187) * 2.0f), 0.0f, 1.0f)), _13192 * clamp(1.0f - (abs(_13194) * 2.0f), 0.0f, 1.0f));
                _43335 += ((_13185 * clamp(_13187 + 0.5f, 0.0f, 1.0f)) - (_13192 * clamp(_13194 + 0.5f, 0.0f, 1.0f)));
                _43332++;
                continue;
            }
            float _15700 = abs(_43334);
            float _13231 = lerp(_43334, ((step(0.0f, _43334) * 2.0f) - 1.0f) * abs(_15700 - (2.0f * floor((_15700 + 1.0f) * 0.5f))), _12978);
            float _15723 = abs(_43335);
            float _13236 = lerp(_43335, ((step(0.0f, _43335) * 2.0f) - 1.0f) * abs(_15723 - (2.0f * floor((_15723 + 1.0f) * 0.5f))), _12978);
            _50476 = 0.0f.xx;
            _45936 = false;
            _45810 = 0.0f.xxxx;
            _45687 = float4(0.0f, 0.0f, 1.0f, 0.0f);
            _45559 = 0.0f.xxxx;
            _45438 = float4(0.0f, 0.0f, 1.0f, 0.0f);
            _45319 = 0.0f;
            _45200 = float2(1.0f, 0.0f);
            _45081 = float2(1.0f, 0.0f);
            _44962 = 0.0f.xx;
            _44843 = 0.0f.xx;
            _44741 = 1.0f;
            _44650 = 1.0f;
            _44433 = sqrt(clamp(max(abs((_13231 * _43337) + (_13236 * _43338)) / max(_43337 + _43338, 9.9999997473787516355514526367188e-05f), min(abs(_13231), abs(_13236))), 0.0f, 1.0f));
            _44272 = 0.0f;
            _44113 = 0.0f;
            _43947 = float2(1.0f, 0.0f);
            _43790 = 0.0f;
            _43633 = false;
            _43537 = _42711;
            _43465 = 0.0f;
            _43339 = p_TexCoord.z;
        }
        else
        {
            float _43342;
            float _43468;
            float _43540;
            bool _43636;
            float _43793;
            float2 _43950;
            float _44116;
            float _44275;
            float _44653;
            float _44744;
            float2 _44846;
            float2 _44965;
            float2 _45084;
            float2 _45203;
            float _45322;
            float4 _45441;
            float4 _45562;
            float4 _45690;
            float4 _45813;
            bool _45939;
            float2 _50479;
            if (_42687 < 0.5f)
            {
                float _44654;
                float _44745;
                float2 _44847;
                float2 _44966;
                float2 _45085;
                float2 _45204;
                float _45323;
                if (_42711 >= 0.5f)
                {
                    float _13270 = max(p_Meta1.z, 9.9999999747524270787835121154785e-07f);
                    float _13277 = atan2(p_TexCoord.y, p_TexCoord.x) * _13270;
                    float _15817 = floor(p_Meta2.y * 0.00048828125f);
                    float _15822 = p_Meta2.y - (_15817 * 2048.0f);
                    float _43327;
                    float _43329;
                    if (_15822 >= 2048.0f)
                    {
                        _43329 = _15822 - 2048.0f;
                        _43327 = _15817 + 1.0f;
                    }
                    else
                    {
                        float _43328;
                        float _43330;
                        if (_15822 < 0.0f)
                        {
                            _43330 = _15822 + 2048.0f;
                            _43328 = _15817 - 1.0f;
                        }
                        else
                        {
                            _43330 = _15822;
                            _43328 = _15817;
                        }
                        _43329 = _43330;
                        _43327 = _43328;
                    }
                    float _15756 = _43327 * 0.0002442598924972116947174072265625f;
                    float _15765 = frac(((_13277 / p_Meta2.x) - (_43329 * 0.000488519784994423389434814453125f)) + 0.5f) - 0.5f;
                    float _15791 = (abs(_15765) - _15756) * p_Meta2.x;
                    float _13285 = (_13277 - (min(frac(_15765 - _15756), frac(_15765 + _15756)) * p_Meta2.x)) / _13270;
                    float _13286 = sin(_13285);
                    float _13292 = cos(_13285);
                    float2 _51266 = float2(_13292, _13286);
                    float _13297 = (_13277 + (min(frac(_15756 - _15765), frac((_43327 * (-0.0002442598924972116947174072265625f)) - _15765)) * p_Meta2.x)) / _13270;
                    float _13298 = sin(_13297);
                    float _13304 = cos(_13297);
                    float2 _51267 = float2(_13304, _13298);
                    float _15876 = min(length(_12969 - (_51266 * clamp(dot(_12969, _51266), 0.0f, 1000000.0f))), length(_12969 - (_51267 * clamp(dot(_12969, _51267), 0.0f, 1000000.0f))));
                    float _13314 = p_Meta1.x * 0.5f;
                    float _13315 = _13270 - _13314;
                    _45323 = ((_43327 * 0.000488519784994423389434814453125f) * p_Meta2.x) + _13314;
                    _45204 = float2(-_13298, _13304);
                    _45085 = float2(-_13286, _13292);
                    _44966 = _51267 * _13315;
                    _44847 = _51266 * _13315;
                    _44745 = _15791;
                    _44654 = (_15791 >= 0.0f) ? _15876 : (-_15876);
                }
                else
                {
                    _45323 = 0.0f;
                    _45204 = float2(1.0f, 0.0f);
                    _45085 = float2(1.0f, 0.0f);
                    _44966 = 0.0f.xx;
                    _44847 = 0.0f.xx;
                    _44745 = 1.0f;
                    _44654 = 1.0f;
                }
                _50479 = 0.0f.xx;
                _45939 = false;
                _45813 = 0.0f.xxxx;
                _45690 = float4(0.0f, 0.0f, 1.0f, 0.0f);
                _45562 = 0.0f.xxxx;
                _45441 = float4(0.0f, 0.0f, 1.0f, 0.0f);
                _45322 = _45323;
                _45203 = _45204;
                _45084 = _45085;
                _44965 = _44966;
                _44846 = _44847;
                _44744 = _44745;
                _44653 = _44654;
                _44275 = 0.0f;
                _44116 = 0.0f;
                _43950 = float2(1.0f, 0.0f);
                _43793 = 0.0f;
                _43636 = false;
                _43540 = _42711;
                _43468 = length(_12969) - p_Meta1.z;
                _43342 = p_TexCoord.z;
            }
            else
            {
                float _43346;
                float _43472;
                float _43544;
                bool _43640;
                float _43797;
                float2 _43954;
                float _44120;
                float _44279;
                float _44655;
                float _44746;
                float2 _44848;
                float2 _44967;
                float2 _45086;
                float2 _45205;
                float _45324;
                float4 _45445;
                float4 _45566;
                float4 _45694;
                float4 _45817;
                bool _45943;
                float2 _50483;
                if (_42687 < 1.5f)
                {
                    float _44656;
                    float _44747;
                    float2 _44849;
                    float2 _44968;
                    float2 _45087;
                    float2 _45206;
                    float _45325;
                    float4 _45446;
                    float4 _45567;
                    float4 _45695;
                    float4 _45818;
                    float4 _50414;
                    float2 _50484;
                    if (_42711 >= 0.5f)
                    {
                        float _13347 = min(p_Meta1.z, p_Meta1.w);
                        float _15941 = floor(p_Meta2.x * 0.00048828125f);
                        float _15946 = p_Meta2.x - (_15941 * 2048.0f);
                        float _43108;
                        float _43110;
                        if (_15946 >= 2048.0f)
                        {
                            _43110 = _15946 - 2048.0f;
                            _43108 = _15941 + 1.0f;
                        }
                        else
                        {
                            float _43109;
                            float _43111;
                            if (_15946 < 0.0f)
                            {
                                _43111 = _15946 + 2048.0f;
                                _43109 = _15941 - 1.0f;
                            }
                            else
                            {
                                _43111 = _15946;
                                _43109 = _15941;
                            }
                            _43110 = _43111;
                            _43108 = _43109;
                        }
                        float _15976 = floor(p_Meta2.y * 0.00048828125f);
                        float _15981 = p_Meta2.y - (_15976 * 2048.0f);
                        float _43114;
                        float _43116;
                        if (_15981 >= 2048.0f)
                        {
                            _43116 = _15981 - 2048.0f;
                            _43114 = _15976 + 1.0f;
                        }
                        else
                        {
                            float _43115;
                            float _43117;
                            if (_15981 < 0.0f)
                            {
                                _43117 = _15981 + 2048.0f;
                                _43115 = _15976 - 1.0f;
                            }
                            else
                            {
                                _43117 = _15981;
                                _43115 = _15976;
                            }
                            _43116 = _43117;
                            _43114 = _43115;
                        }
                        float _16212;
                        float _16221;
                        float _16227;
                        float _16232;
                        float _16238;
                        float _16254;
                        float4 _13366 = (float4(_43108, _43110, _43114, _43116) * 0.000488519784994423389434814453125f.xxxx) * _13347;
                        float _13374 = _14846 * _12863;
                        float _16074 = _13366.x;
                        float _16167 = min(1.5f * p_Meta1.x, _13347 * 0.5f);
                        float _16168 = max(_16074, _16167);
                        float _16079 = _13366.y;
                        float _16176 = max(_16079, _16167);
                        float _16084 = _13366.z;
                        float _16184 = max(_16084, _16167);
                        float _16089 = _13366.w;
                        float _16192 = max(_16089, _16167);
                        float _43123;
                        do
                        {
                            _16212 = 2.0f * p_Meta1.w;
                            _16221 = 2.0f * p_Meta1.z;
                            _16227 = (_16221 - _16184) - _16168;
                            _16232 = _16227 + (1.57079637050628662109375f * _16168);
                            float _16235 = _16232 + ((_16212 - _16168) - _16176);
                            _16238 = 1.57079637050628662109375f * _16176;
                            float _16239 = _16235 + _16238;
                            float _16250 = ((_16239 + _16221) - _16176) - _16192;
                            _16254 = 1.57079637050628662109375f * _16192;
                            float _16255 = _16250 + _16254;
                            bool _16269 = p_TexCoord.x > 0.0f;
                            bool _16272 = p_TexCoord.y > 0.0f;
                            float _16286 = _16269 ? (_16272 ? _16176 : _16168) : (_16272 ? _16192 : _16184);
                            float _16293 = p_Meta1.z - _16286;
                            float _16301 = p_Meta1.w - _16286;
                            float _16306 = abs(p_TexCoord.x);
                            float _16314 = abs(p_TexCoord.y);
                            if ((_16306 > _16293) && (_16314 > _16301))
                            {
                                float2 _16324 = _12969 - float2(sign(p_TexCoord.x) * _16293, sign(p_TexCoord.y) * _16301);
                                float _43120;
                                float2 _50337;
                                if (_16269)
                                {
                                    bool _16331 = p_TexCoord.y < 0.0f;
                                    bool2 _51268 = _16331.xx;
                                    _50337 = float2(_51268.x ? float2(0.0f, -1.0f).x : float2(1.0f, 0.0f).x, _51268.y ? float2(0.0f, -1.0f).y : float2(1.0f, 0.0f).y);
                                    _43120 = _16331 ? _16227 : _16235;
                                }
                                else
                                {
                                    bool2 _51271 = _16272.xx;
                                    _50337 = float2(_51271.x ? float2(0.0f, 1.0f).x : float2(-1.0f, 0.0f).x, _51271.y ? float2(0.0f, 1.0f).y : float2(-1.0f, 0.0f).y);
                                    _43120 = _16272 ? _16250 : (((_16255 + _16212) - _16192) - _16184);
                                }
                                _43123 = _43120 + (_16286 * atan2((_50337.x * _16324.y) - (_50337.y * _16324.x), dot(_50337, _16324)));
                                break;
                            }
                            if ((_16306 - p_Meta1.z) > (_16314 - p_Meta1.w))
                            {
                                _43123 = _16269 ? (((_16232 + p_TexCoord.y) + p_Meta1.w) - _16168) : (((_16255 + p_Meta1.w) - _16192) - p_TexCoord.y);
                                break;
                            }
                            _43123 = _16272 ? (((_16239 + p_Meta1.z) - _16176) - p_TexCoord.x) : ((p_TexCoord.x + p_Meta1.z) - _16184);
                            break;
                        } while(false);
                        float _16511 = floor(p_Meta2.w * 0.00048828125f);
                        float _16516 = p_Meta2.w - (_16511 * 2048.0f);
                        float _43124;
                        float _43126;
                        if (_16516 >= 2048.0f)
                        {
                            _43126 = _16516 - 2048.0f;
                            _43124 = _16511 + 1.0f;
                        }
                        else
                        {
                            float _43125;
                            float _43127;
                            if (_16516 < 0.0f)
                            {
                                _43127 = _16516 + 2048.0f;
                                _43125 = _16511 - 1.0f;
                            }
                            else
                            {
                                _43127 = _16516;
                                _43125 = _16511;
                            }
                            _43126 = _43127;
                            _43124 = _43125;
                        }
                        float _16450 = _43124 * 0.0002442598924972116947174072265625f;
                        float _16459 = frac(((_43123 / p_Meta2.z) - (_43126 * 0.000488519784994423389434814453125f)) + 0.5f) - 0.5f;
                        float _16485 = (abs(_16459) - _16450) * p_Meta2.z;
                        float _16491 = _43123 - (min(frac(_16459 - _16450), frac(_16459 + _16450)) * p_Meta2.z);
                        float _16497 = _43123 + (min(frac(_16450 - _16459), frac((_43124 * (-0.0002442598924972116947174072265625f)) - _16459)) * p_Meta2.z);
                        float _16101 = p_Meta1.x * 0.5f;
                        float _16598 = ((_16232 + _16212) - _16168) - _16176;
                        float _16603 = _16598 + _16238;
                        float _16614 = ((_16603 + _16221) - _16176) - _16192;
                        float _16619 = _16614 + _16254;
                        float _16630 = ((_16619 + _16212) - _16192) - _16184;
                        float _16635 = _16630 + (1.57079637050628662109375f * _16184);
                        float _16639 = max(_16635, 9.9999999747524270787835121154785e-07f);
                        float _16644 = _16491 - (floor(_16491 / _16639) * _16635);
                        float2 _43128;
                        float _43160;
                        float2 _43167;
                        float2 _43184;
                        float2 _50340;
                        float2 _50347;
                        if (_16644 < _16227)
                        {
                            float _16656 = (_16184 - p_Meta1.z) + _16644;
                            _50347 = 0.0f.xx;
                            _50340 = float2(_16656, -p_Meta1.w);
                            _43184 = float2(_16656, _16101 - p_Meta1.w);
                            _43167 = 0.0f.xx;
                            _43160 = -1.0f;
                            _43128 = float2(1.0f, 0.0f);
                        }
                        else
                        {
                            float2 _43129;
                            float _43161;
                            float2 _43168;
                            float2 _43185;
                            float2 _50341;
                            float2 _50348;
                            if (_16644 < _16232)
                            {
                                float _16697 = (_16644 - _16227) / max(_16168, 9.9999999747524270787835121154785e-07f);
                                _50348 = float2(_16168, _16074);
                                _50341 = float2(p_Meta1.z - _16168, _16168 - p_Meta1.w);
                                _43185 = _43143;
                                _43168 = float2(sin(_16697), (-1.0f) * cos(_16697));
                                _43161 = _16697;
                                _43129 = _43143;
                            }
                            else
                            {
                                float2 _43155;
                                float _43162;
                                float2 _43169;
                                float2 _43210;
                                float2 _50342;
                                float2 _50349;
                                if (_16644 < _16598)
                                {
                                    float _16716 = (_16168 - p_Meta1.w) + (_16644 - _16232);
                                    _50349 = 0.0f.xx;
                                    _50342 = float2(p_Meta1.z, _16716);
                                    _43210 = float2(p_Meta1.z - _16101, _16716);
                                    _43169 = 0.0f.xx;
                                    _43162 = -1.0f;
                                    _43155 = float2(0.0f, 1.0f);
                                }
                                else
                                {
                                    float2 _43156;
                                    float _43163;
                                    float2 _43170;
                                    float2 _43211;
                                    float2 _50343;
                                    float2 _50350;
                                    if (_16644 < _16603)
                                    {
                                        float _16752 = (_16644 - _16598) / max(_16176, 9.9999999747524270787835121154785e-07f);
                                        _50350 = float2(_16176, _16079);
                                        _50343 = float2(p_Meta1.z - _16176, p_Meta1.w - _16176);
                                        _43211 = _43143;
                                        _43170 = float2(cos(_16752), sin(_16752));
                                        _43163 = _16752;
                                        _43156 = _43143;
                                    }
                                    else
                                    {
                                        float2 _43157;
                                        float _43164;
                                        float2 _43171;
                                        float2 _43212;
                                        float2 _50344;
                                        float2 _50351;
                                        if (_16644 < _16614)
                                        {
                                            float _16768 = (p_Meta1.z - _16176) - (_16644 - _16603);
                                            _50351 = 0.0f.xx;
                                            _50344 = float2(_16768, p_Meta1.w);
                                            _43212 = float2(_16768, p_Meta1.w - _16101);
                                            _43171 = 0.0f.xx;
                                            _43164 = -1.0f;
                                            _43157 = float2(-1.0f, 0.0f);
                                        }
                                        else
                                        {
                                            float2 _43158;
                                            float _43165;
                                            float2 _43172;
                                            float2 _43213;
                                            float2 _50345;
                                            float2 _50352;
                                            if (_16644 < _16619)
                                            {
                                                float _16807 = (_16644 - _16614) / max(_16192, 9.9999999747524270787835121154785e-07f);
                                                _50352 = float2(_16192, _16089);
                                                _50345 = float2(_16192 - p_Meta1.z, p_Meta1.w - _16192);
                                                _43213 = _43143;
                                                _43172 = float2(-sin(_16807), cos(_16807));
                                                _43165 = _16807;
                                                _43158 = _43143;
                                            }
                                            else
                                            {
                                                bool _16813 = _16644 < _16630;
                                                float _43166;
                                                float2 _43173;
                                                float2 _43214;
                                                float2 _50346;
                                                float2 _50353;
                                                if (_16813)
                                                {
                                                    float _16826 = (p_Meta1.w - _16192) - (_16644 - _16619);
                                                    _50353 = 0.0f.xx;
                                                    _50346 = float2(-p_Meta1.z, _16826);
                                                    _43214 = float2(_16101 - p_Meta1.z, _16826);
                                                    _43173 = 0.0f.xx;
                                                    _43166 = -1.0f;
                                                }
                                                else
                                                {
                                                    float _16861 = (_16644 - _16630) / max(_16184, 9.9999999747524270787835121154785e-07f);
                                                    _50353 = float2(_16184, _16084);
                                                    _50346 = float2(_16184 - p_Meta1.z, _16184 - p_Meta1.w);
                                                    _43214 = _43143;
                                                    _43173 = float2((-1.0f) * cos(_16861), (-1.0f) * sin(_16861));
                                                    _43166 = _16861;
                                                }
                                                bool2 _51274 = _16813.xx;
                                                _50352 = _50353;
                                                _50345 = _50346;
                                                _43213 = _43214;
                                                _43172 = _43173;
                                                _43165 = _43166;
                                                _43158 = float2(_51274.x ? float2(0.0f, -1.0f).x : _43143.x, _51274.y ? float2(0.0f, -1.0f).y : _43143.y);
                                            }
                                            _50351 = _50352;
                                            _50344 = _50345;
                                            _43212 = _43213;
                                            _43171 = _43172;
                                            _43164 = _43165;
                                            _43157 = _43158;
                                        }
                                        _50350 = _50351;
                                        _50343 = _50344;
                                        _43211 = _43212;
                                        _43170 = _43171;
                                        _43163 = _43164;
                                        _43156 = _43157;
                                    }
                                    _50349 = _50350;
                                    _50342 = _50343;
                                    _43210 = _43211;
                                    _43169 = _43170;
                                    _43162 = _43163;
                                    _43155 = _43156;
                                }
                                _50348 = _50349;
                                _50341 = _50342;
                                _43185 = _43210;
                                _43168 = _43169;
                                _43161 = _43162;
                                _43129 = _43155;
                            }
                            _50347 = _50348;
                            _50340 = _50341;
                            _43184 = _43185;
                            _43167 = _43168;
                            _43160 = _43161;
                            _43128 = _43129;
                        }
                        float _16873 = p_Meta1.x * (-1.0f);
                        bool _16875 = _43160 >= 0.0f;
                        float2 _43179;
                        float2 _43183;
                        float2 _43215;
                        float4 _43216;
                        float _43217;
                        float4 _43218;
                        if (_16875)
                        {
                            float2 _17009 = float2(-_43167.y, _43167.x);
                            float _17045 = _43160 - 0.785398185253143310546875f;
                            float _17047 = sin(_17045);
                            float _17049 = cos(_17045);
                            float _17052 = (_17045 >= 0.0f) ? 1.0f : (-1.0f);
                            float _17055 = _17052 * _17045;
                            float _17062 = max(_50347.y - _16101, 0.0f);
                            float _17071 = max(_50347.x - max(_50347.y, _16101), 0.0f) * 1.41421353816986083984375f;
                            float _17082 = (_17062 * _17062) - (((_17071 * _17071) * _17047) * _17047);
                            float _17085 = sqrt(max(_17082, 0.0f));
                            float _17090 = (_17071 * _17049) + _17085;
                            float2 _43174;
                            float2 _43175;
                            float4 _43176;
                            float _43177;
                            float4 _43178;
                            if (((_17062 > 9.9999997473787516355514526367188e-05f) && (_17082 >= 0.0f)) && ((_17090 * (_17052 * _17047)) <= (_17062 * 0.707106769084930419921875f)))
                            {
                                _43178 = 0.0f.xxxx;
                                _43177 = 0.0f;
                                _43176 = float4(0.0f, 0.0f, 1.0f, 0.0f);
                                _43175 = _50340 + (_43167 * _17090);
                                _43174 = ((_43167 * _17085) + (_17009 * (_17071 * _17047))) / _17062.xx;
                            }
                            else
                            {
                                float _17129 = 0.785398185253143310546875f - _17055;
                                float _17134 = cos(_17129);
                                float _17158 = 0.785398185253143310546875f + _17055;
                                float2 _17174 = (_43167 * cos(_17158)) - (_17009 * (_17052 * sin(_17158)));
                                float2 _17191 = _50340 + (((_43167 * _17049) - (_17009 * _17047)) * _17071);
                                _43178 = float4(_17191, _17062, _17052 * (-1.57079637050628662109375f));
                                _43177 = -_17052;
                                _43176 = float4(_17191 + (_17174 * _17062), float2(-_17174.y, _17174.x) * 1.0f);
                                _43175 = _50340 + (_43167 * ((_50347.x - _16101) / max(_17134, 9.9999997473787516355514526367188e-05f)));
                                _43174 = (_43167 * _17134) + (_17009 * (_17052 * sin(_17129)));
                            }
                            _43218 = _43178;
                            _43217 = _43177;
                            _43216 = _43176;
                            _43215 = float2(-_43174.y, _43174.x) * 1.0f;
                            _43183 = _43175;
                            _43179 = _17009;
                        }
                        else
                        {
                            _43218 = 0.0f.xxxx;
                            _43217 = 0.0f;
                            _43216 = float4(0.0f, 0.0f, 1.0f, 0.0f);
                            _43215 = _43128;
                            _43183 = _43184;
                            _43179 = _43128;
                        }
                        float _17350 = _16497 - (floor(_16497 / _16639) * _16635);
                        float2 _43219;
                        float _43259;
                        float2 _43266;
                        float2 _43283;
                        float2 _50389;
                        float2 _50396;
                        if (_17350 < _16227)
                        {
                            float _17362 = (_16184 - p_Meta1.z) + _17350;
                            _50396 = 0.0f.xx;
                            _50389 = float2(_17362, -p_Meta1.w);
                            _43283 = float2(_17362, _16101 - p_Meta1.w);
                            _43266 = 0.0f.xx;
                            _43259 = -1.0f;
                            _43219 = float2(1.0f, 0.0f);
                        }
                        else
                        {
                            float2 _43220;
                            float _43260;
                            float2 _43267;
                            float2 _43284;
                            float2 _50390;
                            float2 _50397;
                            if (_17350 < _16232)
                            {
                                float _17403 = (_17350 - _16227) / max(_16168, 9.9999999747524270787835121154785e-07f);
                                _50397 = float2(_16168, _16074);
                                _50390 = float2(p_Meta1.z - _16168, _16168 - p_Meta1.w);
                                _43284 = _43143;
                                _43267 = float2(sin(_17403), (-1.0f) * cos(_17403));
                                _43260 = _17403;
                                _43220 = _43143;
                            }
                            else
                            {
                                float2 _43254;
                                float _43261;
                                float2 _43268;
                                float2 _43318;
                                float2 _50391;
                                float2 _50398;
                                if (_17350 < _16598)
                                {
                                    float _17422 = (_16168 - p_Meta1.w) + (_17350 - _16232);
                                    _50398 = 0.0f.xx;
                                    _50391 = float2(p_Meta1.z, _17422);
                                    _43318 = float2(p_Meta1.z - _16101, _17422);
                                    _43268 = 0.0f.xx;
                                    _43261 = -1.0f;
                                    _43254 = float2(0.0f, 1.0f);
                                }
                                else
                                {
                                    float2 _43255;
                                    float _43262;
                                    float2 _43269;
                                    float2 _43319;
                                    float2 _50392;
                                    float2 _50399;
                                    if (_17350 < _16603)
                                    {
                                        float _17458 = (_17350 - _16598) / max(_16176, 9.9999999747524270787835121154785e-07f);
                                        _50399 = float2(_16176, _16079);
                                        _50392 = float2(p_Meta1.z - _16176, p_Meta1.w - _16176);
                                        _43319 = _43143;
                                        _43269 = float2(cos(_17458), sin(_17458));
                                        _43262 = _17458;
                                        _43255 = _43143;
                                    }
                                    else
                                    {
                                        float2 _43256;
                                        float _43263;
                                        float2 _43270;
                                        float2 _43320;
                                        float2 _50393;
                                        float2 _50400;
                                        if (_17350 < _16614)
                                        {
                                            float _17474 = (p_Meta1.z - _16176) - (_17350 - _16603);
                                            _50400 = 0.0f.xx;
                                            _50393 = float2(_17474, p_Meta1.w);
                                            _43320 = float2(_17474, p_Meta1.w - _16101);
                                            _43270 = 0.0f.xx;
                                            _43263 = -1.0f;
                                            _43256 = float2(-1.0f, 0.0f);
                                        }
                                        else
                                        {
                                            float2 _43257;
                                            float _43264;
                                            float2 _43271;
                                            float2 _43321;
                                            float2 _50394;
                                            float2 _50401;
                                            if (_17350 < _16619)
                                            {
                                                float _17513 = (_17350 - _16614) / max(_16192, 9.9999999747524270787835121154785e-07f);
                                                _50401 = float2(_16192, _16089);
                                                _50394 = float2(_16192 - p_Meta1.z, p_Meta1.w - _16192);
                                                _43321 = _43143;
                                                _43271 = float2(-sin(_17513), cos(_17513));
                                                _43264 = _17513;
                                                _43257 = _43143;
                                            }
                                            else
                                            {
                                                bool _17519 = _17350 < _16630;
                                                float _43265;
                                                float2 _43272;
                                                float2 _43322;
                                                float2 _50395;
                                                float2 _50402;
                                                if (_17519)
                                                {
                                                    float _17532 = (p_Meta1.w - _16192) - (_17350 - _16619);
                                                    _50402 = 0.0f.xx;
                                                    _50395 = float2(-p_Meta1.z, _17532);
                                                    _43322 = float2(_16101 - p_Meta1.z, _17532);
                                                    _43272 = 0.0f.xx;
                                                    _43265 = -1.0f;
                                                }
                                                else
                                                {
                                                    float _17567 = (_17350 - _16630) / max(_16184, 9.9999999747524270787835121154785e-07f);
                                                    _50402 = float2(_16184, _16084);
                                                    _50395 = float2(_16184 - p_Meta1.z, _16184 - p_Meta1.w);
                                                    _43322 = _43143;
                                                    _43272 = float2((-1.0f) * cos(_17567), (-1.0f) * sin(_17567));
                                                    _43265 = _17567;
                                                }
                                                bool2 _51277 = _17519.xx;
                                                _50401 = _50402;
                                                _50394 = _50395;
                                                _43321 = _43322;
                                                _43271 = _43272;
                                                _43264 = _43265;
                                                _43257 = float2(_51277.x ? float2(0.0f, -1.0f).x : _43143.x, _51277.y ? float2(0.0f, -1.0f).y : _43143.y);
                                            }
                                            _50400 = _50401;
                                            _50393 = _50394;
                                            _43320 = _43321;
                                            _43270 = _43271;
                                            _43263 = _43264;
                                            _43256 = _43257;
                                        }
                                        _50399 = _50400;
                                        _50392 = _50393;
                                        _43319 = _43320;
                                        _43269 = _43270;
                                        _43262 = _43263;
                                        _43255 = _43256;
                                    }
                                    _50398 = _50399;
                                    _50391 = _50392;
                                    _43318 = _43319;
                                    _43268 = _43269;
                                    _43261 = _43262;
                                    _43254 = _43255;
                                }
                                _50397 = _50398;
                                _50390 = _50391;
                                _43284 = _43318;
                                _43267 = _43268;
                                _43260 = _43261;
                                _43220 = _43254;
                            }
                            _50396 = _50397;
                            _50389 = _50390;
                            _43283 = _43284;
                            _43266 = _43267;
                            _43259 = _43260;
                            _43219 = _43220;
                        }
                        bool _17581 = _43259 >= 0.0f;
                        float2 _43278;
                        float2 _43282;
                        float2 _43323;
                        float4 _43324;
                        float _43325;
                        float4 _43326;
                        if (_17581)
                        {
                            float2 _17715 = float2(-_43266.y, _43266.x);
                            float _17751 = _43259 - 0.785398185253143310546875f;
                            float _17753 = sin(_17751);
                            float _17755 = cos(_17751);
                            float _17758 = (_17751 >= 0.0f) ? 1.0f : (-1.0f);
                            float _17761 = _17758 * _17751;
                            float _17768 = max(_50396.y - _16101, 0.0f);
                            float _17777 = max(_50396.x - max(_50396.y, _16101), 0.0f) * 1.41421353816986083984375f;
                            float _17788 = (_17768 * _17768) - (((_17777 * _17777) * _17753) * _17753);
                            float _17791 = sqrt(max(_17788, 0.0f));
                            float _17796 = (_17777 * _17755) + _17791;
                            float2 _43273;
                            float2 _43274;
                            float4 _43275;
                            float _43276;
                            float4 _43277;
                            if (((_17768 > 9.9999997473787516355514526367188e-05f) && (_17788 >= 0.0f)) && ((_17796 * (_17758 * _17753)) <= (_17768 * 0.707106769084930419921875f)))
                            {
                                _43277 = 0.0f.xxxx;
                                _43276 = 0.0f;
                                _43275 = float4(0.0f, 0.0f, 1.0f, 0.0f);
                                _43274 = _50389 + (_43266 * _17796);
                                _43273 = ((_43266 * _17791) + (_17715 * (_17777 * _17753))) / _17768.xx;
                            }
                            else
                            {
                                float _17835 = 0.785398185253143310546875f - _17761;
                                float _17840 = cos(_17835);
                                float _17864 = 0.785398185253143310546875f + _17761;
                                float2 _17880 = (_43266 * cos(_17864)) - (_17715 * (_17758 * sin(_17864)));
                                float2 _17897 = _50389 + (((_43266 * _17755) - (_17715 * _17753)) * _17777);
                                _43277 = float4(_17897, _17768, _17758 * (-1.57079637050628662109375f));
                                _43276 = -_17758;
                                _43275 = float4(_17897 + (_17880 * _17768), float2(-_17880.y, _17880.x) * 1.0f);
                                _43274 = _50389 + (_43266 * ((_50396.x - _16101) / max(_17840, 9.9999997473787516355514526367188e-05f)));
                                _43273 = (_43266 * _17840) + (_17715 * (_17758 * sin(_17835)));
                            }
                            _43326 = _43277;
                            _43325 = _43276;
                            _43324 = _43275;
                            _43323 = float2(-_43273.y, _43273.x) * 1.0f;
                            _43282 = _43274;
                            _43278 = _17715;
                        }
                        else
                        {
                            _43326 = 0.0f.xxxx;
                            _43325 = 0.0f;
                            _43324 = float4(0.0f, 0.0f, 1.0f, 0.0f);
                            _43323 = _43219;
                            _43282 = _43283;
                            _43278 = _43219;
                        }
                        float2 _16146 = float2(_43179.y, -_43179.x);
                        float2 _16152 = float2(_43278.y, -_43278.x);
                        float2 _17995 = _12969 - (_50340 + (_16146 * ((_16875 ? 0.0f : _16873) - _13374)));
                        float2 _18012 = _12969 - (_50389 + (_16152 * ((_17581 ? 0.0f : _16873) - _13374)));
                        float _17982 = min(length(_17995 - (_16146 * clamp(dot(_17995, _16146), 0.0f, 1000000.0f))), length(_18012 - (_16152 * clamp(dot(_18012, _16152), 0.0f, 1000000.0f))));
                        _50484 = float2(_43217, _43325);
                        _50414 = _13366;
                        _45818 = _43326;
                        _45695 = _43324;
                        _45567 = _43218;
                        _45446 = _43216;
                        _45325 = ((_43124 * 0.000488519784994423389434814453125f) * p_Meta2.z) + _16101;
                        _45206 = _43323;
                        _45087 = _43215;
                        _44968 = _43282;
                        _44849 = _43183;
                        _44747 = _16485;
                        _44656 = (_16485 >= 0.0f) ? _17982 : (-_17982);
                    }
                    else
                    {
                        _50484 = 0.0f.xx;
                        _50414 = p_Meta2;
                        _45818 = 0.0f.xxxx;
                        _45695 = float4(0.0f, 0.0f, 1.0f, 0.0f);
                        _45567 = 0.0f.xxxx;
                        _45446 = float4(0.0f, 0.0f, 1.0f, 0.0f);
                        _45325 = 0.0f;
                        _45206 = float2(1.0f, 0.0f);
                        _45087 = float2(1.0f, 0.0f);
                        _44968 = 0.0f.xx;
                        _44849 = 0.0f.xx;
                        _44747 = 1.0f;
                        _44656 = 1.0f;
                    }
                    bool2 _18034 = (p_TexCoord.x > 0.0f).xx;
                    float2 _18035 = float2(_18034.x ? _50414.xy.x : _50414.zw.x, _18034.y ? _50414.xy.y : _50414.zw.y);
                    float _18047 = (p_TexCoord.y > 0.0f) ? _18035.y : _18035.x;
                    float2 _18056 = (abs(_12969) - float2(p_Meta1.z, p_Meta1.w)) + _18047.xx;
                    _50483 = _50484;
                    _45943 = false;
                    _45817 = _45818;
                    _45694 = _45695;
                    _45566 = _45567;
                    _45445 = _45446;
                    _45324 = _45325;
                    _45205 = _45206;
                    _45086 = _45087;
                    _44967 = _44968;
                    _44848 = _44849;
                    _44746 = _44747;
                    _44655 = _44656;
                    _44279 = 0.0f;
                    _44120 = 0.0f;
                    _43954 = float2(1.0f, 0.0f);
                    _43797 = 0.0f;
                    _43640 = false;
                    _43544 = _42711;
                    _43472 = (min(max(_18056.x, _18056.y), 0.0f) + length(max(_18056, 0.0f.xx))) - _18047;
                    _43346 = p_TexCoord.z;
                }
                else
                {
                    float _43376;
                    float _43473;
                    float _43574;
                    bool _43670;
                    float _43827;
                    float2 _43984;
                    float _44150;
                    float _44309;
                    float _44657;
                    float _44748;
                    float2 _44850;
                    float2 _44969;
                    float2 _45088;
                    float2 _45207;
                    float _45326;
                    float4 _45447;
                    float4 _45568;
                    float4 _45696;
                    float4 _45819;
                    bool _45973;
                    float2 _50485;
                    if (_42687 < 2.5f)
                    {
                        bool _13408 = p_Meta2.z == p_Meta1.z;
                        float _43475;
                        if (_13408)
                        {
                            float2 _13412 = float2(p_Meta1.w, 0.0f);
                            _43475 = length(_12969 - (_13412 * clamp(dot(_12969, _13412) / dot(_13412, _13412), 0.0f, 1.0f)));
                        }
                        else
                        {
                            float _18318 = floor(0.0f);
                            float _18323 = _18318 * (-8.0f);
                            float _43089;
                            float _43091;
                            if (_18323 >= 8.0f)
                            {
                                _43091 = _18323 - 8.0f;
                                _43089 = _18318 + 1.0f;
                            }
                            else
                            {
                                float _43090;
                                float _43092;
                                if (_18323 < 0.0f)
                                {
                                    _43092 = _18323 + 8.0f;
                                    _43090 = _18318 - 1.0f;
                                }
                                else
                                {
                                    _43092 = _18323;
                                    _43090 = _18318;
                                }
                                _43091 = _43092;
                                _43089 = _43090;
                            }
                            float _18126 = abs(p_TexCoord.y);
                            float _18129 = -p_TexCoord.x;
                            float _18133 = p_TexCoord.x - p_Meta1.w;
                            float _18143 = (_43091 >= 2.5f) ? (-1000000.0f) : ((_43091 >= 1.5f) ? (_18129 - p_Meta1.z) : _18129);
                            float _18153 = (_43089 >= 2.5f) ? (-1000000.0f) : ((_43089 >= 1.5f) ? (_18133 - p_Meta2.z) : _18133);
                            float _18156 = max(_18143, _18153);
                            float _18167 = (p_Meta1.z - p_Meta2.z) / p_Meta1.w;
                            float _43093;
                            if (abs(_18167) >= 1.0f)
                            {
                                float2 _18176 = float2(p_Meta1.w, 0.0f);
                                bool2 _18177 = (p_Meta1.z >= p_Meta2.z).xx;
                                _43093 = length(_12969 - float2(_18177.x ? 0.0f.xx.x : _18176.x, _18177.y ? 0.0f.xx.y : _18176.y)) - max(p_Meta1.z, p_Meta2.z);
                            }
                            else
                            {
                                float _18192 = sqrt(1.0f - (_18167 * _18167));
                                float _18202 = ((_18167 * p_TexCoord.x) + (_18192 * _18126)) - p_Meta1.z;
                                float _18210 = (_18192 * p_TexCoord.x) - (_18167 * _18126);
                                float _43094;
                                if (((_18143 > _18153) ? _43091 : _43089) < 0.5f)
                                {
                                    float _43095;
                                    if ((_18210 < 0.0f) && (_43091 < 2.5f))
                                    {
                                        _43095 = length(_12969) - p_Meta1.z;
                                    }
                                    else
                                    {
                                        float _43096;
                                        if ((_18210 > (_18192 * p_Meta1.w)) && (_43089 < 2.5f))
                                        {
                                            _43096 = length(_12969 - float2(p_Meta1.w, 0.0f)) - p_Meta2.z;
                                        }
                                        else
                                        {
                                            _43096 = _18202;
                                        }
                                        _43095 = _43096;
                                    }
                                    _43094 = _43095;
                                }
                                else
                                {
                                    _43094 = min(max(_18156, _18202), 0.0f) + length(max(float2(_18156, _18202), 0.0f.xx));
                                }
                                _43093 = _43094;
                            }
                            float _43097;
                            if (_43091 >= 3.5f)
                            {
                                _43097 = max(_43093, p_TexCoord.x - (p_Meta1.z * abs(0.0f)));
                            }
                            else
                            {
                                _43097 = _43093;
                            }
                            float _43098;
                            if (_43089 >= 3.5f)
                            {
                                _43098 = max(_43097, (_12969 - float2(p_Meta1.w, 0.0f)).x - (p_Meta2.z * abs(0.0f)));
                            }
                            else
                            {
                                _43098 = _43097;
                            }
                            _43475 = _43098;
                        }
                        bool _13425 = _42711 >= 0.5f;
                        float _43828;
                        float2 _43985;
                        float _44151;
                        if (_13425)
                        {
                            _44151 = p_TexCoord.y;
                            _43985 = p_Meta2.xy;
                            _43828 = p_TexCoord.x;
                        }
                        else
                        {
                            _44151 = 0.0f;
                            _43985 = float2(1.0f, 0.0f);
                            _43828 = 0.0f;
                        }
                        _50485 = 0.0f.xx;
                        _45973 = false;
                        _45819 = 0.0f.xxxx;
                        _45696 = float4(0.0f, 0.0f, 1.0f, 0.0f);
                        _45568 = 0.0f.xxxx;
                        _45447 = float4(0.0f, 0.0f, 1.0f, 0.0f);
                        _45326 = 0.0f;
                        _45207 = float2(1.0f, 0.0f);
                        _45088 = float2(1.0f, 0.0f);
                        _44969 = 0.0f.xx;
                        _44850 = 0.0f.xx;
                        _44748 = 1.0f;
                        _44657 = 1.0f;
                        _44309 = _13425 ? p_Meta1.z : 0.0f;
                        _44150 = _44151;
                        _43984 = _43985;
                        _43827 = _43828;
                        _43670 = _13425;
                        _43574 = _42711;
                        _43473 = _43475;
                        _43376 = _13408 ? p_TexCoord.z : 0.0f;
                    }
                    else
                    {
                        float _43379;
                        float _43476;
                        float _43576;
                        bool _43681;
                        float _43838;
                        float2 _43995;
                        float _44161;
                        float _44320;
                        float _44668;
                        float _44759;
                        float2 _44861;
                        float2 _44980;
                        float2 _45099;
                        float2 _45218;
                        float _45337;
                        float4 _45458;
                        float4 _45579;
                        float4 _45707;
                        float4 _45830;
                        bool _45984;
                        float2 _50496;
                        if (_42687 < 4.5f)
                        {
                            bool _13441 = _42687 < 3.5f;
                            float _43478;
                            if (_13441)
                            {
                                float2 _18349 = abs(_12969);
                                float2 _18356 = _18349 - (float2(-0.866025388240814208984375f, 0.5f) * (2.0f * min(dot(float2(-0.866025388240814208984375f, 0.5f), _18349), 0.0f)));
                                float2 _18367 = _18356 - float2(clamp(_18356.x, (-0.57735025882720947265625f) * p_Meta1.z, 0.57735025882720947265625f * p_Meta1.z), p_Meta1.z);
                                _43478 = length(_18367) * sign(_18367.y);
                            }
                            else
                            {
                                float _18380 = abs(p_TexCoord.x) - p_Meta1.z;
                                float _18386 = p_TexCoord.y + (p_Meta1.z * 0.57735025882720947265625f);
                                float _18392 = 1.73205077648162841796875f * _18386;
                                float2 _50216;
                                if ((_18380 + _18392) > 0.0f)
                                {
                                    _50216 = float2(_18380 - _18392, ((-1.73205077648162841796875f) * _18380) - _18386) * 0.5f.xx;
                                }
                                else
                                {
                                    _50216 = float2(_18380, _18386);
                                }
                                float2 _48162 = _50216;
                                _48162.x = _50216.x - clamp(_50216.x, (-2.0f) * p_Meta1.z, 0.0f);
                                _43478 = (-length(_48162)) * sign(_50216.y);
                            }
                            float _44669;
                            float _44760;
                            float2 _44862;
                            float2 _44981;
                            float2 _45100;
                            float2 _45219;
                            float _45338;
                            float4 _45459;
                            float4 _45580;
                            float4 _45708;
                            float4 _45831;
                            float2 _50497;
                            if (_42711 >= 0.5f)
                            {
                                float _13458 = p_Meta1.z * 0.57735025882720947265625f;
                                float _13459 = _13441 ? p_Meta1.z : _13458;
                                float _13464 = _13441 ? _13458 : p_Meta1.z;
                                float _13466 = _13441 ? 1.0471975803375244140625f : 2.094395160675048828125f;
                                float _13470 = _14846 * _12863;
                                float _18495 = _13459 + p_TexCoord.z;
                                float _18596 = max(p_TexCoord.z, min(1.5f * p_Meta1.x, _18495 * 0.5f));
                                float _18503 = _18495 - _18596;
                                float _18512 = (_13459 > 9.9999999747524270787835121154785e-07f) ? ((_13464 * _18503) / _13459) : _13464;
                                float _18620 = floor(((atan2(p_TexCoord.y, p_TexCoord.x) - 0.52359879016876220703125f) / _13466) + 0.5f);
                                float _18625 = 0.52359879016876220703125f + (_18620 * _13466);
                                float _18627 = sin(_18625);
                                float _18630 = cos(_18625);
                                float2 _51258 = float2(_18630, _18627);
                                float _18642 = (_18630 * p_TexCoord.y) - (_18627 * p_TexCoord.x);
                                float _18647 = clamp(_18642, -_18512, _18512);
                                float _18650 = 2.0f * _18512;
                                float _18654 = _18650 + (_18596 * _13466);
                                float _18659 = ((_18620 * _18654) + _18647) + _18512;
                                float _18662 = _18642 - _18647;
                                float _43052;
                                if (abs(_18662) > 0.0f)
                                {
                                    float2 _18677 = (_51258 * _18503) + (float2(-_18627, _18630) * (sign(_18662) * _18512));
                                    _43052 = _18659 + (_18596 * atan2((_18630 * (p_TexCoord.y - _18677.y)) - (_18627 * (p_TexCoord.x - _18677.x)), dot(_12969 - _18677, _51258)));
                                }
                                else
                                {
                                    _43052 = _18659;
                                }
                                float _18794 = floor(p_Meta2.y * 0.00048828125f);
                                float _18799 = p_Meta2.y - (_18794 * 2048.0f);
                                float _43053;
                                float _43055;
                                if (_18799 >= 2048.0f)
                                {
                                    _43055 = _18799 - 2048.0f;
                                    _43053 = _18794 + 1.0f;
                                }
                                else
                                {
                                    float _43054;
                                    float _43056;
                                    if (_18799 < 0.0f)
                                    {
                                        _43056 = _18799 + 2048.0f;
                                        _43054 = _18794 - 1.0f;
                                    }
                                    else
                                    {
                                        _43056 = _18799;
                                        _43054 = _18794;
                                    }
                                    _43055 = _43056;
                                    _43053 = _43054;
                                }
                                float _18733 = _43053 * 0.0002442598924972116947174072265625f;
                                float _18742 = frac(((_43052 / p_Meta2.x) - (_43055 * 0.000488519784994423389434814453125f)) + 0.5f) - 0.5f;
                                float _18768 = (abs(_18742) - _18733) * p_Meta2.x;
                                float _18774 = _43052 - (min(frac(_18742 - _18733), frac(_18742 + _18733)) * p_Meta2.x);
                                float _18780 = _43052 + (min(frac(_18733 - _18742), frac((_43053 * (-0.0002442598924972116947174072265625f)) - _18742)) * p_Meta2.x);
                                float _18523 = p_Meta1.x * 0.5f;
                                float _18856 = floor(_18774 / _18654);
                                float _18861 = _18774 - (_18856 * _18654);
                                float _18866 = 0.52359879016876220703125f + (_18856 * _13466);
                                float _18867 = sin(_18866);
                                float _18874 = cos(_18866);
                                float2 _51259 = float2(_18874, _18867);
                                float2 _18946 = float2(-_18867, _18874);
                                float2 _43062;
                                float2 _43064;
                                float _43066;
                                float2 _43068;
                                float2 _43069;
                                float4 _43070;
                                float _43071;
                                float4 _43072;
                                if (_18861 <= _18650)
                                {
                                    float2 _18891 = (_51259 * _18503) + (_18946 * (_18861 - _18512));
                                    _43072 = 0.0f.xxxx;
                                    _43071 = 0.0f;
                                    _43070 = float4(0.0f, 0.0f, 1.0f, 0.0f);
                                    _43069 = _18946;
                                    _43068 = _18891 + (_51259 * (_18596 - _18523));
                                    _43066 = _18596 - p_Meta1.x;
                                    _43064 = _18946;
                                    _43062 = _18891;
                                }
                                else
                                {
                                    float2 _18912 = (_51259 * _18503) + (_18946 * _18512);
                                    float _18919 = (_18861 - _18650) / max(_18596, 9.9999999747524270787835121154785e-07f);
                                    float _18952 = sin(_18919);
                                    float _18954 = cos(_18919);
                                    float _18963 = (_18874 * _18954) - (_18867 * _18952);
                                    float _18972 = (_18874 * _18952) + (_18867 * _18954);
                                    float2 _18973 = float2(_18963, _18972);
                                    float2 _18981 = float2(-_18972, _18963);
                                    float _19010 = _13466 * 0.5f;
                                    float _19017 = _18919 - _19010;
                                    float _19019 = sin(_19017);
                                    float _19021 = cos(_19017);
                                    float _19024 = (_19017 >= 0.0f) ? 1.0f : (-1.0f);
                                    float _19027 = _19024 * _19017;
                                    float _19034 = max(p_TexCoord.z - _18523, 0.0f);
                                    float _19043 = max(_18596 - max(p_TexCoord.z, _18523), 0.0f) / max(cos(_19010), 9.9999997473787516355514526367188e-05f);
                                    float _19054 = (_19034 * _19034) - (((_19043 * _19043) * _19019) * _19019);
                                    float _19057 = sqrt(max(_19054, 0.0f));
                                    float _19062 = (_19043 * _19021) + _19057;
                                    float2 _43057;
                                    float2 _43058;
                                    float4 _43059;
                                    float _43060;
                                    float4 _43061;
                                    if (((_19034 > 9.9999997473787516355514526367188e-05f) && (_19054 >= 0.0f)) && ((_19062 * (_19024 * _19019)) <= (_19034 * sin(_19010))))
                                    {
                                        _43061 = 0.0f.xxxx;
                                        _43060 = 0.0f;
                                        _43059 = float4(0.0f, 0.0f, 1.0f, 0.0f);
                                        _43058 = _18912 + (_18973 * _19062);
                                        _43057 = ((_18973 * _19057) + (_18981 * (_19043 * _19019))) / _19034.xx;
                                    }
                                    else
                                    {
                                        float _19101 = _19010 - _19027;
                                        float _19106 = cos(_19101);
                                        float _19130 = _19010 + _19027;
                                        float2 _19146 = (_18973 * cos(_19130)) - (_18981 * (_19024 * sin(_19130)));
                                        float2 _19163 = _18912 + (((_18973 * _19021) - (_18981 * _19019)) * _19043);
                                        float2 _19168 = _19163 + (_19146 * _19034);
                                        float _19176 = -_19024;
                                        _43061 = float4(_19163, _19034, _19176 * _13466);
                                        _43060 = _19176;
                                        _43059 = float4(_19168, float2(-_19146.y, _19146.x) * 1.0f);
                                        _43058 = _18912 + (_18973 * ((_18596 - _18523) / max(_19106, 9.9999997473787516355514526367188e-05f)));
                                        _43057 = (_18973 * _19106) + (_18981 * (_19024 * sin(_19101)));
                                    }
                                    _43072 = _43061;
                                    _43071 = _43060;
                                    _43070 = _43059;
                                    _43069 = float2(-_43057.y, _43057.x) * 1.0f;
                                    _43068 = _43058;
                                    _43066 = 0.0f;
                                    _43064 = _18981;
                                    _43062 = _18912;
                                }
                                float _19251 = floor(_18780 / _18654);
                                float _19256 = _18780 - (_19251 * _18654);
                                float _19261 = 0.52359879016876220703125f + (_19251 * _13466);
                                float _19262 = sin(_19261);
                                float _19269 = cos(_19261);
                                float2 _51260 = float2(_19269, _19262);
                                float2 _19341 = float2(-_19262, _19269);
                                float2 _43078;
                                float2 _43080;
                                float _43082;
                                float2 _43084;
                                float2 _43085;
                                float4 _43086;
                                float _43087;
                                float4 _43088;
                                if (_19256 <= _18650)
                                {
                                    float2 _19286 = (_51260 * _18503) + (_19341 * (_19256 - _18512));
                                    _43088 = 0.0f.xxxx;
                                    _43087 = 0.0f;
                                    _43086 = float4(0.0f, 0.0f, 1.0f, 0.0f);
                                    _43085 = _19341;
                                    _43084 = _19286 + (_51260 * (_18596 - _18523));
                                    _43082 = _18596 - p_Meta1.x;
                                    _43080 = _19341;
                                    _43078 = _19286;
                                }
                                else
                                {
                                    float2 _19307 = (_51260 * _18503) + (_19341 * _18512);
                                    float _19314 = (_19256 - _18650) / max(_18596, 9.9999999747524270787835121154785e-07f);
                                    float _19347 = sin(_19314);
                                    float _19349 = cos(_19314);
                                    float _19358 = (_19269 * _19349) - (_19262 * _19347);
                                    float _19367 = (_19269 * _19347) + (_19262 * _19349);
                                    float2 _19368 = float2(_19358, _19367);
                                    float2 _19376 = float2(-_19367, _19358);
                                    float _19405 = _13466 * 0.5f;
                                    float _19412 = _19314 - _19405;
                                    float _19414 = sin(_19412);
                                    float _19416 = cos(_19412);
                                    float _19419 = (_19412 >= 0.0f) ? 1.0f : (-1.0f);
                                    float _19422 = _19419 * _19412;
                                    float _19429 = max(p_TexCoord.z - _18523, 0.0f);
                                    float _19438 = max(_18596 - max(p_TexCoord.z, _18523), 0.0f) / max(cos(_19405), 9.9999997473787516355514526367188e-05f);
                                    float _19449 = (_19429 * _19429) - (((_19438 * _19438) * _19414) * _19414);
                                    float _19452 = sqrt(max(_19449, 0.0f));
                                    float _19457 = (_19438 * _19416) + _19452;
                                    float2 _43073;
                                    float2 _43074;
                                    float4 _43075;
                                    float _43076;
                                    float4 _43077;
                                    if (((_19429 > 9.9999997473787516355514526367188e-05f) && (_19449 >= 0.0f)) && ((_19457 * (_19419 * _19414)) <= (_19429 * sin(_19405))))
                                    {
                                        _43077 = 0.0f.xxxx;
                                        _43076 = 0.0f;
                                        _43075 = float4(0.0f, 0.0f, 1.0f, 0.0f);
                                        _43074 = _19307 + (_19368 * _19457);
                                        _43073 = ((_19368 * _19452) + (_19376 * (_19438 * _19414))) / _19429.xx;
                                    }
                                    else
                                    {
                                        float _19496 = _19405 - _19422;
                                        float _19501 = cos(_19496);
                                        float _19525 = _19405 + _19422;
                                        float2 _19541 = (_19368 * cos(_19525)) - (_19376 * (_19419 * sin(_19525)));
                                        float2 _19558 = _19307 + (((_19368 * _19416) - (_19376 * _19414)) * _19438);
                                        float2 _19563 = _19558 + (_19541 * _19429);
                                        float _19571 = -_19419;
                                        _43077 = float4(_19558, _19429, _19571 * _13466);
                                        _43076 = _19571;
                                        _43075 = float4(_19563, float2(-_19541.y, _19541.x) * 1.0f);
                                        _43074 = _19307 + (_19368 * ((_18596 - _18523) / max(_19501, 9.9999997473787516355514526367188e-05f)));
                                        _43073 = (_19368 * _19501) + (_19376 * (_19419 * sin(_19496)));
                                    }
                                    _43088 = _43077;
                                    _43087 = _43076;
                                    _43086 = _43075;
                                    _43085 = float2(-_43073.y, _43073.x) * 1.0f;
                                    _43084 = _43074;
                                    _43082 = 0.0f;
                                    _43080 = _19376;
                                    _43078 = _19307;
                                }
                                float2 _18574 = float2(_43064.y, -_43064.x);
                                float2 _18580 = float2(_43080.y, -_43080.x);
                                float2 _19656 = _12969 - (_43062 + (_18574 * (_43066 - _13470)));
                                float2 _19673 = _12969 - (_43078 + (_18580 * (_43082 - _13470)));
                                float _19643 = min(length(_19656 - (_18574 * clamp(dot(_19656, _18574), 0.0f, 1000000.0f))), length(_19673 - (_18580 * clamp(dot(_19673, _18580), 0.0f, 1000000.0f))));
                                _50497 = float2(_43071, _43087);
                                _45831 = _43088;
                                _45708 = _43086;
                                _45580 = _43072;
                                _45459 = _43070;
                                _45338 = ((_43053 * 0.000488519784994423389434814453125f) * p_Meta2.x) + _18523;
                                _45219 = _43085;
                                _45100 = _43069;
                                _44981 = _43084;
                                _44862 = _43068;
                                _44760 = _18768;
                                _44669 = (_18768 >= 0.0f) ? _19643 : (-_19643);
                            }
                            else
                            {
                                _50497 = 0.0f.xx;
                                _45831 = 0.0f.xxxx;
                                _45708 = float4(0.0f, 0.0f, 1.0f, 0.0f);
                                _45580 = 0.0f.xxxx;
                                _45459 = float4(0.0f, 0.0f, 1.0f, 0.0f);
                                _45338 = 0.0f;
                                _45219 = float2(1.0f, 0.0f);
                                _45100 = float2(1.0f, 0.0f);
                                _44981 = 0.0f.xx;
                                _44862 = 0.0f.xx;
                                _44760 = 1.0f;
                                _44669 = 1.0f;
                            }
                            _50496 = _50497;
                            _45984 = false;
                            _45830 = _45831;
                            _45707 = _45708;
                            _45579 = _45580;
                            _45458 = _45459;
                            _45337 = _45338;
                            _45218 = _45219;
                            _45099 = _45100;
                            _44980 = _44981;
                            _44861 = _44862;
                            _44759 = _44760;
                            _44668 = _44669;
                            _44320 = 0.0f;
                            _44161 = 0.0f;
                            _43995 = float2(1.0f, 0.0f);
                            _43838 = 0.0f;
                            _43681 = false;
                            _43576 = _42711;
                            _43476 = _43478;
                            _43379 = p_TexCoord.z;
                        }
                        else
                        {
                            float _43388;
                            float _43486;
                            float _43585;
                            bool _43692;
                            float _43849;
                            float2 _44006;
                            float _44172;
                            float _44331;
                            float _44672;
                            float _44763;
                            float2 _44865;
                            float2 _44984;
                            float2 _45103;
                            float2 _45222;
                            float _45341;
                            float4 _45462;
                            float4 _45583;
                            float4 _45711;
                            float4 _45834;
                            bool _45995;
                            float2 _50500;
                            if (_42687 < 5.5f)
                            {
                                float _43487;
                                float _44673;
                                float _44764;
                                float2 _44866;
                                float2 _44985;
                                float2 _45104;
                                float2 _45223;
                                float _45342;
                                float4 _45463;
                                float4 _45584;
                                float4 _45712;
                                float4 _45835;
                                float2 _50501;
                                if (_42711 >= 0.5f)
                                {
                                    float2 _19703 = p_Meta2.zw - p_Meta2.xy;
                                    float2 _19706 = -p_Meta2.zw;
                                    float2 _19712 = _12969 - p_Meta2.xy;
                                    float2 _19715 = _12969 - p_Meta2.zw;
                                    float2 _19727 = _12969 - (p_Meta2.xy * clamp(dot(_12969, p_Meta2.xy) / dot(p_Meta2.xy, p_Meta2.xy), 0.0f, 1.0f));
                                    float2 _19739 = _19712 - (_19703 * clamp(dot(_19712, _19703) / dot(_19703, _19703), 0.0f, 1.0f));
                                    float2 _19751 = _19715 - (_19706 * clamp(dot(_19715, _19706) / dot(_19706, _19706), 0.0f, 1.0f));
                                    float _19755 = _19706.y;
                                    float _19760 = _19706.x;
                                    float _19763 = sign((p_Meta2.x * _19755) - (p_Meta2.y * _19760));
                                    float2 _19816 = min(min(float2(dot(_19727, _19727), _19763 * ((p_TexCoord.x * p_Meta2.y) - (p_TexCoord.y * p_Meta2.x))), float2(dot(_19739, _19739), _19763 * ((_19712.x * _19703.y) - (_19712.y * _19703.x)))), float2(dot(_19751, _19751), _19763 * ((_19715.x * _19755) - (_19715.y * _19760))));
                                    float _13511 = _14846 * _12863;
                                    float _19929 = (((p_Meta2.x * (p_Meta2.w - p_Meta2.y)) - (p_Meta2.y * (p_Meta2.z - p_Meta2.x))) >= 0.0f) ? 1.0f : (-1.0f);
                                    float2 _19941 = -normalize(p_Meta2.xy);
                                    float2 _19943 = float2(-_19941.y, _19941.x) * _19929;
                                    float2 _19946 = -normalize(_19703);
                                    float2 _19948 = float2(-_19946.y, _19946.x) * _19929;
                                    float2 _19951 = -normalize(_19706);
                                    float2 _19953 = float2(-_19951.y, _19951.x) * _19929;
                                    float _20115 = max(p_TexCoord.z, min(1.5f * p_Meta1.x, p_TexCoord.z + ((abs((p_Meta2.x * p_Meta2.w) - (p_Meta2.y * p_Meta2.z)) / max((length(p_Meta2.xy) + length(p_Meta2.zw)) + length(_19703), 9.9999999747524270787835121154785e-07f)) * 0.5f)));
                                    float _19987 = _20115 - p_TexCoord.z;
                                    float2 _20131 = (-(_19953 + _19943)) * (_19987 / max(1.0f + dot(_19953, _19943), 0.001000000047497451305389404296875f));
                                    float2 _19997 = p_Meta2.xy + ((-(_19943 + _19948)) * (_19987 / max(1.0f + dot(_19943, _19948), 0.001000000047497451305389404296875f)));
                                    float2 _20003 = p_Meta2.zw + ((-(_19948 + _19953)) * (_19987 / max(1.0f + dot(_19948, _19953), 0.001000000047497451305389404296875f)));
                                    float2 _20199 = _19997 - _20131;
                                    float2 _20202 = _20003 - _19997;
                                    float2 _20205 = _20131 - _20003;
                                    float _20207 = length(_20199);
                                    float _20209 = length(_20202);
                                    float _20211 = length(_20205);
                                    float2 _20215 = _20199 / _20207.xx;
                                    float2 _20219 = _20202 / _20209.xx;
                                    float2 _20223 = _20205 / _20211.xx;
                                    float _20226 = _20215.x;
                                    float _20228 = _20219.y;
                                    float _20231 = _20215.y;
                                    float _20233 = _20219.x;
                                    float _20239 = atan2((_20226 * _20228) - (_20231 * _20233), dot(_20215, _20219));
                                    float _20240 = _19929 * _20239;
                                    float _20245 = _20223.y;
                                    float _20250 = _20223.x;
                                    float _20256 = atan2((_20233 * _20245) - (_20228 * _20250), dot(_20219, _20223));
                                    float2 _20260 = _12969 - _20131;
                                    float _20262 = dot(_20260, _20215);
                                    float2 _20265 = _12969 - _19997;
                                    float _20267 = dot(_20265, _20219);
                                    float2 _20270 = _12969 - _20003;
                                    float _20272 = dot(_20270, _20223);
                                    float2 _20281 = _20260 - (_20215 * clamp(_20262, 0.0f, _20207));
                                    float2 _20290 = _20265 - (_20219 * clamp(_20267, 0.0f, _20209));
                                    float2 _20299 = _20270 - (_20223 * clamp(_20272, 0.0f, _20211));
                                    float _20302 = dot(_20281, _20281);
                                    float _20305 = dot(_20290, _20290);
                                    float _20308 = dot(_20299, _20299);
                                    float _42985;
                                    float _42987;
                                    float _42989;
                                    float2 _42991;
                                    float2 _42993;
                                    if ((_20302 <= _20305) && (_20302 <= _20308))
                                    {
                                        _42993 = _20131;
                                        _42991 = _20215;
                                        _42989 = 0.0f;
                                        _42987 = _20207;
                                        _42985 = _20262;
                                    }
                                    else
                                    {
                                        bool _20324 = _20305 <= _20308;
                                        float _42990;
                                        if (_20324)
                                        {
                                            _42990 = _20207 + (_20115 * _20240);
                                        }
                                        else
                                        {
                                            _42990 = (_20207 + _20209) + (_20115 * (_20240 + (_19929 * _20256)));
                                        }
                                        bool2 _51283 = _20324.xx;
                                        _42993 = float2(_51283.x ? _19997.x : _20003.x, _51283.y ? _19997.y : _20003.y);
                                        _42991 = float2(_51283.x ? _20219.x : _20223.x, _51283.y ? _20219.y : _20223.y);
                                        _42989 = _42990;
                                        _42987 = _20324 ? _20209 : _20211;
                                        _42985 = _20324 ? _20267 : _20272;
                                    }
                                    float _20353 = clamp(_42985, 0.0f, _42987);
                                    float _20356 = _42989 + _20353;
                                    float _42995;
                                    if (abs(_42985 - _20353) > 0.0f)
                                    {
                                        float2 _20366 = -_42991;
                                        float2 _20368 = float2(-_20366.y, _20366.x) * _19929;
                                        float2 _20375 = _12969 - (_42993 + (_42991 * _20353));
                                        _42995 = _20356 + ((_20115 * _19929) * atan2((_20368.x * _20375.y) - (_20368.y * _20375.x), dot(_20368, _20375)));
                                    }
                                    else
                                    {
                                        _42995 = _20356;
                                    }
                                    float _20486 = floor(p_Meta1.w * 0.00048828125f);
                                    float _20491 = p_Meta1.w - (_20486 * 2048.0f);
                                    float _42996;
                                    float _42998;
                                    if (_20491 >= 2048.0f)
                                    {
                                        _42998 = _20491 - 2048.0f;
                                        _42996 = _20486 + 1.0f;
                                    }
                                    else
                                    {
                                        float _42997;
                                        float _42999;
                                        if (_20491 < 0.0f)
                                        {
                                            _42999 = _20491 + 2048.0f;
                                            _42997 = _20486 - 1.0f;
                                        }
                                        else
                                        {
                                            _42999 = _20491;
                                            _42997 = _20486;
                                        }
                                        _42998 = _42999;
                                        _42996 = _42997;
                                    }
                                    float _20601;
                                    float _20620;
                                    float _20639;
                                    float _20650;
                                    float _20654;
                                    float _20425 = _42996 * 0.0002442598924972116947174072265625f;
                                    float _20434 = frac(((_42995 / p_Meta1.z) - (_42998 * 0.000488519784994423389434814453125f)) + 0.5f) - 0.5f;
                                    float _20460 = (abs(_20434) - _20425) * p_Meta1.z;
                                    float _20466 = _42995 - (min(frac(_20434 - _20425), frac(_20434 + _20425)) * p_Meta1.z);
                                    float _20472 = _42995 + (min(frac(_20425 - _20434), frac((_42996 * (-0.0002442598924972116947174072265625f)) - _20434)) * p_Meta1.z);
                                    float _20014 = p_Meta1.x * 0.5f;
                                    float2 _43013;
                                    float2 _43015;
                                    float _43017;
                                    float2 _43019;
                                    float2 _43020;
                                    float4 _43021;
                                    float _43022;
                                    float4 _43023;
                                    do
                                    {
                                        float _20585 = _20115 * _19929;
                                        _20601 = _20585 * _20239;
                                        _20620 = _20585 * _20256;
                                        _20639 = _20585 * atan2((_20250 * _20231) - (_20245 * _20226), dot(_20223, _20215));
                                        _20650 = ((((_20207 + _20209) + _20211) + _20601) + _20620) + _20639;
                                        _20654 = max(_20650, 9.9999999747524270787835121154785e-07f);
                                        float _20659 = _20466 - (floor(_20466 / _20654) * _20650);
                                        if (_20659 < _20207)
                                        {
                                            float2 _20671 = _20131 + (_20215 * _20659);
                                            float2 _20680 = -_20215;
                                            _43023 = 0.0f.xxxx;
                                            _43022 = 0.0f;
                                            _43021 = float4(0.0f, 0.0f, 1.0f, 0.0f);
                                            _43020 = _20215;
                                            _43019 = _20671 + ((float2(-_20680.y, _20680.x) * _19929) * (_20115 - _20014));
                                            _43017 = _20115 - p_Meta1.x;
                                            _43015 = _20215;
                                            _43013 = _20671;
                                            break;
                                        }
                                        float _20692 = _20659 - _20207;
                                        float _43000;
                                        float2 _43002;
                                        float2 _43004;
                                        float _43006;
                                        if (_20692 >= _20601)
                                        {
                                            float _20699 = _20692 - _20601;
                                            if (_20699 < _20209)
                                            {
                                                float2 _20708 = _19997 + (_20219 * _20699);
                                                float2 _20717 = -_20219;
                                                _43023 = 0.0f.xxxx;
                                                _43022 = 0.0f;
                                                _43021 = float4(0.0f, 0.0f, 1.0f, 0.0f);
                                                _43020 = _20219;
                                                _43019 = _20708 + ((float2(-_20717.y, _20717.x) * _19929) * (_20115 - _20014));
                                                _43017 = _20115 - p_Meta1.x;
                                                _43015 = _20219;
                                                _43013 = _20708;
                                                break;
                                            }
                                            float _20729 = _20699 - _20209;
                                            bool _20735 = _20729 >= _20620;
                                            float _43001;
                                            if (_20735)
                                            {
                                                float _20739 = _20729 - _20620;
                                                if (_20739 < _20211)
                                                {
                                                    float2 _20748 = _20003 + (_20223 * _20739);
                                                    float2 _20757 = -_20223;
                                                    _43023 = 0.0f.xxxx;
                                                    _43022 = 0.0f;
                                                    _43021 = float4(0.0f, 0.0f, 1.0f, 0.0f);
                                                    _43020 = _20223;
                                                    _43019 = _20748 + ((float2(-_20757.y, _20757.x) * _19929) * (_20115 - _20014));
                                                    _43017 = _20115 - p_Meta1.x;
                                                    _43015 = _20223;
                                                    _43013 = _20748;
                                                    break;
                                                }
                                                _43001 = _20739 - _20211;
                                            }
                                            else
                                            {
                                                _43001 = _20729;
                                            }
                                            bool2 _51290 = _20735.xx;
                                            _43006 = _20735 ? _20639 : _20620;
                                            _43004 = float2(_51290.x ? _20131.x : _20003.x, _51290.y ? _20131.y : _20003.y);
                                            _43002 = float2(_51290.x ? _20223.x : _20219.x, _51290.y ? _20223.y : _20219.y);
                                            _43000 = _43001;
                                        }
                                        else
                                        {
                                            _43006 = _20601;
                                            _43004 = _19997;
                                            _43002 = _20215;
                                            _43000 = _20692;
                                        }
                                        float _20777 = max(_20115, 9.9999999747524270787835121154785e-07f);
                                        float _20778 = _43000 / _20777;
                                        float2 _20781 = -_43002;
                                        float2 _20783 = float2(-_20781.y, _20781.x) * _19929;
                                        float _20786 = _19929 * _20778;
                                        float _20848 = sin(_20786);
                                        float _20850 = cos(_20786);
                                        float _20852 = _20783.x;
                                        float _20856 = _20783.y;
                                        float _20859 = (_20852 * _20850) - (_20856 * _20848);
                                        float _20868 = (_20852 * _20848) + (_20856 * _20850);
                                        float2 _20869 = float2(_20859, _20868);
                                        float2 _20877 = float2(-_20868, _20859);
                                        float _20796 = _43006 / _20777;
                                        float _20906 = _20796 * 0.5f;
                                        float _20913 = _20778 - _20906;
                                        float _20915 = sin(_20913);
                                        float _20917 = cos(_20913);
                                        float _20920 = (_20913 >= 0.0f) ? 1.0f : (-1.0f);
                                        float _20923 = _20920 * _20913;
                                        float _20930 = max(p_TexCoord.z - _20014, 0.0f);
                                        float _20939 = max(_20115 - max(p_TexCoord.z, _20014), 0.0f) / max(cos(_20906), 9.9999997473787516355514526367188e-05f);
                                        float _20950 = (_20930 * _20930) - (((_20939 * _20939) * _20915) * _20915);
                                        float _20953 = sqrt(max(_20950, 0.0f));
                                        float _20958 = (_20939 * _20917) + _20953;
                                        float2 _43008;
                                        float2 _43009;
                                        float4 _43010;
                                        float _43011;
                                        float4 _43012;
                                        if (((_20930 > 9.9999997473787516355514526367188e-05f) && (_20950 >= 0.0f)) && ((_20958 * (_20920 * _20915)) <= (_20930 * sin(_20906))))
                                        {
                                            _43012 = 0.0f.xxxx;
                                            _43011 = 0.0f;
                                            _43010 = float4(0.0f, 0.0f, 1.0f, 0.0f);
                                            _43009 = _43004 + (_20869 * _20958);
                                            _43008 = ((_20869 * _20953) + (_20877 * ((_19929 * _20939) * _20915))) / _20930.xx;
                                        }
                                        else
                                        {
                                            float _20997 = _20906 - _20923;
                                            float _21002 = cos(_20997);
                                            float _21019 = _19929 * _20920;
                                            float _21026 = _20906 + _20923;
                                            float2 _21042 = (_20869 * cos(_21026)) - (_20877 * (_21019 * sin(_21026)));
                                            float2 _21059 = _43004 + (((_20869 * _20917) - (_20877 * (_19929 * _20915))) * _20939);
                                            float _21072 = -_20920;
                                            _43012 = float4(_21059, _20930, (_21072 * _19929) * _20796);
                                            _43011 = _21072;
                                            _43010 = float4(_21059 + (_21042 * _20930), float2(-_21042.y, _21042.x) * _19929);
                                            _43009 = _43004 + (_20869 * ((_20115 - _20014) / max(_21002, 9.9999997473787516355514526367188e-05f)));
                                            _43008 = (_20869 * _21002) + (_20877 * (_21019 * sin(_20997)));
                                        }
                                        _43023 = _43012;
                                        _43022 = _43011;
                                        _43021 = _43010;
                                        _43020 = float2(-_43008.y, _43008.x) * _19929;
                                        _43019 = _43009;
                                        _43017 = 0.0f;
                                        _43015 = _20877 * _19929;
                                        _43013 = _43004;
                                        break;
                                    } while(false);
                                    float2 _43037;
                                    float2 _43039;
                                    float _43041;
                                    float2 _43043;
                                    float2 _43044;
                                    float4 _43045;
                                    float _43046;
                                    float4 _43047;
                                    do
                                    {
                                        float _21258 = _20472 - (floor(_20472 / _20654) * _20650);
                                        if (_21258 < _20207)
                                        {
                                            float2 _21270 = _20131 + (_20215 * _21258);
                                            float2 _21279 = -_20215;
                                            _43047 = 0.0f.xxxx;
                                            _43046 = 0.0f;
                                            _43045 = float4(0.0f, 0.0f, 1.0f, 0.0f);
                                            _43044 = _20215;
                                            _43043 = _21270 + ((float2(-_21279.y, _21279.x) * _19929) * (_20115 - _20014));
                                            _43041 = _20115 - p_Meta1.x;
                                            _43039 = _20215;
                                            _43037 = _21270;
                                            break;
                                        }
                                        float _21291 = _21258 - _20207;
                                        float _43024;
                                        float2 _43026;
                                        float2 _43028;
                                        float _43030;
                                        if (_21291 >= _20601)
                                        {
                                            float _21298 = _21291 - _20601;
                                            if (_21298 < _20209)
                                            {
                                                float2 _21307 = _19997 + (_20219 * _21298);
                                                float2 _21316 = -_20219;
                                                _43047 = 0.0f.xxxx;
                                                _43046 = 0.0f;
                                                _43045 = float4(0.0f, 0.0f, 1.0f, 0.0f);
                                                _43044 = _20219;
                                                _43043 = _21307 + ((float2(-_21316.y, _21316.x) * _19929) * (_20115 - _20014));
                                                _43041 = _20115 - p_Meta1.x;
                                                _43039 = _20219;
                                                _43037 = _21307;
                                                break;
                                            }
                                            float _21328 = _21298 - _20209;
                                            bool _21334 = _21328 >= _20620;
                                            float _43025;
                                            if (_21334)
                                            {
                                                float _21338 = _21328 - _20620;
                                                if (_21338 < _20211)
                                                {
                                                    float2 _21347 = _20003 + (_20223 * _21338);
                                                    float2 _21356 = -_20223;
                                                    _43047 = 0.0f.xxxx;
                                                    _43046 = 0.0f;
                                                    _43045 = float4(0.0f, 0.0f, 1.0f, 0.0f);
                                                    _43044 = _20223;
                                                    _43043 = _21347 + ((float2(-_21356.y, _21356.x) * _19929) * (_20115 - _20014));
                                                    _43041 = _20115 - p_Meta1.x;
                                                    _43039 = _20223;
                                                    _43037 = _21347;
                                                    break;
                                                }
                                                _43025 = _21338 - _20211;
                                            }
                                            else
                                            {
                                                _43025 = _21328;
                                            }
                                            bool2 _51295 = _21334.xx;
                                            _43030 = _21334 ? _20639 : _20620;
                                            _43028 = float2(_51295.x ? _20131.x : _20003.x, _51295.y ? _20131.y : _20003.y);
                                            _43026 = float2(_51295.x ? _20223.x : _20219.x, _51295.y ? _20223.y : _20219.y);
                                            _43024 = _43025;
                                        }
                                        else
                                        {
                                            _43030 = _20601;
                                            _43028 = _19997;
                                            _43026 = _20215;
                                            _43024 = _21291;
                                        }
                                        float _21376 = max(_20115, 9.9999999747524270787835121154785e-07f);
                                        float _21377 = _43024 / _21376;
                                        float2 _21380 = -_43026;
                                        float2 _21382 = float2(-_21380.y, _21380.x) * _19929;
                                        float _21385 = _19929 * _21377;
                                        float _21447 = sin(_21385);
                                        float _21449 = cos(_21385);
                                        float _21451 = _21382.x;
                                        float _21455 = _21382.y;
                                        float _21458 = (_21451 * _21449) - (_21455 * _21447);
                                        float _21467 = (_21451 * _21447) + (_21455 * _21449);
                                        float2 _21468 = float2(_21458, _21467);
                                        float2 _21476 = float2(-_21467, _21458);
                                        float _21395 = _43030 / _21376;
                                        float _21505 = _21395 * 0.5f;
                                        float _21512 = _21377 - _21505;
                                        float _21514 = sin(_21512);
                                        float _21516 = cos(_21512);
                                        float _21519 = (_21512 >= 0.0f) ? 1.0f : (-1.0f);
                                        float _21522 = _21519 * _21512;
                                        float _21529 = max(p_TexCoord.z - _20014, 0.0f);
                                        float _21538 = max(_20115 - max(p_TexCoord.z, _20014), 0.0f) / max(cos(_21505), 9.9999997473787516355514526367188e-05f);
                                        float _21549 = (_21529 * _21529) - (((_21538 * _21538) * _21514) * _21514);
                                        float _21552 = sqrt(max(_21549, 0.0f));
                                        float _21557 = (_21538 * _21516) + _21552;
                                        float2 _43032;
                                        float2 _43033;
                                        float4 _43034;
                                        float _43035;
                                        float4 _43036;
                                        if (((_21529 > 9.9999997473787516355514526367188e-05f) && (_21549 >= 0.0f)) && ((_21557 * (_21519 * _21514)) <= (_21529 * sin(_21505))))
                                        {
                                            _43036 = 0.0f.xxxx;
                                            _43035 = 0.0f;
                                            _43034 = float4(0.0f, 0.0f, 1.0f, 0.0f);
                                            _43033 = _43028 + (_21468 * _21557);
                                            _43032 = ((_21468 * _21552) + (_21476 * ((_19929 * _21538) * _21514))) / _21529.xx;
                                        }
                                        else
                                        {
                                            float _21596 = _21505 - _21522;
                                            float _21601 = cos(_21596);
                                            float _21618 = _19929 * _21519;
                                            float _21625 = _21505 + _21522;
                                            float2 _21641 = (_21468 * cos(_21625)) - (_21476 * (_21618 * sin(_21625)));
                                            float2 _21658 = _43028 + (((_21468 * _21516) - (_21476 * (_19929 * _21514))) * _21538);
                                            float _21671 = -_21519;
                                            _43036 = float4(_21658, _21529, (_21671 * _19929) * _21395);
                                            _43035 = _21671;
                                            _43034 = float4(_21658 + (_21641 * _21529), float2(-_21641.y, _21641.x) * _19929);
                                            _43033 = _43028 + (_21468 * ((_20115 - _20014) / max(_21601, 9.9999997473787516355514526367188e-05f)));
                                            _43032 = (_21468 * _21601) + (_21476 * (_21618 * sin(_21596)));
                                        }
                                        _43047 = _43036;
                                        _43046 = _43035;
                                        _43045 = _43034;
                                        _43044 = float2(-_43032.y, _43032.x) * _19929;
                                        _43043 = _43033;
                                        _43041 = 0.0f;
                                        _43039 = _21476 * _19929;
                                        _43037 = _43028;
                                        break;
                                    } while(false);
                                    float2 _20067 = float2(_43015.y, -_43015.x) * _19929;
                                    float2 _20075 = float2(_43039.y, -_43039.x) * _19929;
                                    float2 _21756 = _12969 - (_43013 + (_20067 * (_43017 - _13511)));
                                    float2 _21773 = _12969 - (_43037 + (_20075 * (_43041 - _13511)));
                                    float _21743 = min(length(_21756 - (_20067 * clamp(dot(_21756, _20067), 0.0f, 1000000.0f))), length(_21773 - (_20075 * clamp(dot(_21773, _20075), 0.0f, 1000000.0f))));
                                    _50501 = float2(_43022, _43046);
                                    _45835 = _43047;
                                    _45712 = _43045;
                                    _45584 = _43023;
                                    _45463 = _43021;
                                    _45342 = ((_42996 * 0.000488519784994423389434814453125f) * p_Meta1.z) + _20014;
                                    _45223 = _43044;
                                    _45104 = _43020;
                                    _44985 = _43043;
                                    _44866 = _43019;
                                    _44764 = _20460;
                                    _44673 = (_20460 >= 0.0f) ? _21743 : (-_21743);
                                    _43487 = (-sqrt(_19816.x)) * sign(_19816.y);
                                }
                                else
                                {
                                    float2 _21800 = p_Meta2.xy - p_Meta1.zw;
                                    float2 _21803 = p_Meta2.zw - p_Meta2.xy;
                                    float2 _21806 = p_Meta1.zw - p_Meta2.zw;
                                    float2 _21809 = _12969 - p_Meta1.zw;
                                    float2 _21812 = _12969 - p_Meta2.xy;
                                    float2 _21815 = _12969 - p_Meta2.zw;
                                    float2 _21827 = _21809 - (_21800 * clamp(dot(_21809, _21800) / dot(_21800, _21800), 0.0f, 1.0f));
                                    float2 _21839 = _21812 - (_21803 * clamp(dot(_21812, _21803) / dot(_21803, _21803), 0.0f, 1.0f));
                                    float2 _21851 = _21815 - (_21806 * clamp(dot(_21815, _21806) / dot(_21806, _21806), 0.0f, 1.0f));
                                    float _21853 = _21800.x;
                                    float _21855 = _21806.y;
                                    float _21858 = _21800.y;
                                    float _21860 = _21806.x;
                                    float _21863 = sign((_21853 * _21855) - (_21858 * _21860));
                                    float2 _21916 = min(min(float2(dot(_21827, _21827), _21863 * ((_21809.x * _21858) - (_21809.y * _21853))), float2(dot(_21839, _21839), _21863 * ((_21812.x * _21803.y) - (_21812.y * _21803.x)))), float2(dot(_21851, _21851), _21863 * ((_21815.x * _21855) - (_21815.y * _21860))));
                                    _50501 = 0.0f.xx;
                                    _45835 = 0.0f.xxxx;
                                    _45712 = float4(0.0f, 0.0f, 1.0f, 0.0f);
                                    _45584 = 0.0f.xxxx;
                                    _45463 = float4(0.0f, 0.0f, 1.0f, 0.0f);
                                    _45342 = 0.0f;
                                    _45223 = float2(1.0f, 0.0f);
                                    _45104 = float2(1.0f, 0.0f);
                                    _44985 = 0.0f.xx;
                                    _44866 = 0.0f.xx;
                                    _44764 = 1.0f;
                                    _44673 = 1.0f;
                                    _43487 = (-sqrt(_21916.x)) * sign(_21916.y);
                                }
                                _50500 = _50501;
                                _45995 = false;
                                _45834 = _45835;
                                _45711 = _45712;
                                _45583 = _45584;
                                _45462 = _45463;
                                _45341 = _45342;
                                _45222 = _45223;
                                _45103 = _45104;
                                _44984 = _44985;
                                _44865 = _44866;
                                _44763 = _44764;
                                _44672 = _44673;
                                _44331 = 0.0f;
                                _44172 = 0.0f;
                                _44006 = float2(1.0f, 0.0f);
                                _43849 = 0.0f;
                                _43692 = false;
                                _43585 = _42711;
                                _43486 = _43487;
                                _43388 = p_TexCoord.z;
                            }
                            else
                            {
                                float _43403;
                                float _43501;
                                float _43600;
                                bool _43707;
                                float _43864;
                                float2 _44021;
                                float _44187;
                                float _44346;
                                float _44674;
                                float _44765;
                                bool _46010;
                                if (_42687 < 6.5f)
                                {
                                    bool _21946;
                                    float2 _13556 = float2(p_Meta1.z, p_Meta1.w);
                                    float _42943;
                                    float2 _42944;
                                    float2 _50048;
                                    do
                                    {
                                        float _22010 = max(p_Meta1.z, p_Meta1.w);
                                        float2 _22015 = max(_22010, 1.0000000031710768509710513471353e-30f).xx;
                                        float2 _22016 = abs(_12969) / _22015;
                                        float2 _22021 = _13556 / _22015;
                                        float2 _42935;
                                        if (_22021.y > _22021.x)
                                        {
                                            _50048 = _22021.yx;
                                            _42935 = _22016.yx;
                                        }
                                        else
                                        {
                                            _50048 = _22021;
                                            _42935 = _22016;
                                        }
                                        _21946 = _22010 <= 0.0f;
                                        if (_21946)
                                        {
                                            _42944 = _42935;
                                            _42943 = length(_12969);
                                            break;
                                        }
                                        if (_50048.y <= 1.0000000116860974230803549289703e-07f)
                                        {
                                            _42944 = _42935;
                                            _42943 = length(float2(_42935.x - clamp(_42935.x, 0.0f, _50048.x), _42935.y)) * _22010;
                                            break;
                                        }
                                        float _22100;
                                        float2 _42942;
                                        do
                                        {
                                            float _22067 = _50048.x * _50048.x;
                                            float _22072 = _50048.y * _50048.y;
                                            float _22077 = _50048.x * _42935.x;
                                            float _22082 = _50048.y * _42935.y;
                                            float _22087 = _42935.x / _50048.x;
                                            float _22092 = _42935.y / _50048.y;
                                            float _22095 = _22087 * _22087;
                                            float _22098 = _22092 * _22092;
                                            _22100 = (_22095 + _22098) - 1.0f;
                                            if (_42935.y <= (9.9999997473787516355514526367188e-06f * _50048.y))
                                            {
                                                float _22115 = _22067 - _22072;
                                                if (_22077 >= _22115)
                                                {
                                                    _42942 = float2(_50048.x, 0.0f);
                                                    break;
                                                }
                                                float _22129 = (_22067 * _42935.x) / _22115;
                                                _42942 = float2(_22129, _50048.y * sqrt(clamp(1.0f - ((_22129 * _22129) / _22067), 0.0f, 1.0f)));
                                                break;
                                            }
                                            if (_42935.x <= (9.9999997473787516355514526367188e-06f * _50048.x))
                                            {
                                                _42942 = float2(0.0f, _50048.y);
                                                break;
                                            }
                                            float _22162 = max(_22077 - _22067, _22082 - _22072);
                                            float _22168 = length(float2(_22077, _22082)) - _22072;
                                            float _22172 = 1.0f - _22098;
                                            float _22176 = 1.0f - _22095;
                                            float _22186 = (_22172 > 0.0f) ? ((_22077 / sqrt(max(_22172, 1.0000000031710768509710513471353e-30f))) - _22067) : 1000000015047466219876688855040.0f;
                                            float _22196 = (_22176 > 0.0f) ? ((_22082 / sqrt(max(_22176, 1.0000000031710768509710513471353e-30f))) - _22072) : 1000000015047466219876688855040.0f;
                                            float _42936;
                                            float _42941;
                                            if (_22100 < 0.0f)
                                            {
                                                float _22201 = min(_22168, 0.0f);
                                                _42941 = _22201;
                                                _42936 = min(max(_22162, max(_22186, _22196)), _22201);
                                            }
                                            else
                                            {
                                                float _22211 = max(_22162, 0.0f);
                                                _42941 = max(min(_22168, min(_22186, _22196)), _22211);
                                                _42936 = _22211;
                                            }
                                            float _42938;
                                            _42938 = _42936;
                                            for (int _42937 = 0; _42937 < 8; )
                                            {
                                                float _22229 = 1.0f / (_22067 + _42938);
                                                float _22233 = 1.0f / (_22072 + _42938);
                                                float _22236 = _22077 * _22229;
                                                float _22239 = _22082 * _22233;
                                                float _22242 = _22236 * _22236;
                                                float _22245 = _22239 * _22239;
                                                float _22247 = sqrt(_22242 + _22245);
                                                _42938 = clamp(_42938 + ((((_22247 - 1.0f) * _22247) * _22247) / max((_22242 * _22229) + (_22245 * _22233), 1.0000000031710768509710513471353e-30f)), _42936, _42941);
                                                _42937++;
                                                continue;
                                            }
                                            float2 _22287 = float2(_22077 / (_22067 + _42938), _22082 / (_22072 + _42938));
                                            _42942 = (_22287 / max(length(_22287), 1.0000000031710768509710513471353e-30f).xx) * _50048;
                                            break;
                                        } while(false);
                                        _42944 = _42942;
                                        _42943 = (length(_42942 - _42935) * _22010) * ((_22100 < 0.0f) ? (-1.0f) : 1.0f);
                                        break;
                                    } while(false);
                                    bool _13565 = _42711 >= 0.5f;
                                    float _44675;
                                    if (_13565)
                                    {
                                        bool _13572 = _42711 >= 1.5f;
                                        float _42984;
                                        do
                                        {
                                            bool2 _22438 = (float(p_Meta1.w > p_Meta1.z) > 0.5f).xx;
                                            float2 _22439 = float2(_22438.x ? p_TexCoord.yx.x : _12969.x, _22438.y ? p_TexCoord.yx.y : _12969.y);
                                            float2 _22443 = _13556.yx;
                                            float2 _22446 = float2(_22438.x ? _22443.x : _13556.x, _22438.y ? _22443.y : _13556.y);
                                            if (_21946 || (_50048.y <= 1.0000000116860974230803549289703e-07f))
                                            {
                                                _42984 = 1.0f;
                                                break;
                                            }
                                            float _22815 = _22446.y;
                                            float _22818 = _22815 * _22815;
                                            float _22820 = _22446.x;
                                            float _22821 = _22818 / _22820;
                                            float _22840 = (max(_22821, min(1.5f * p_Meta1.x, min(3.0f * _22821, 0.949999988079071044921875f * _22815))) * _22820) / max(_22815, 1.0000000031710768509710513471353e-30f);
                                            float _22854 = _22820 * _22820;
                                            float _22860 = _22854 - _22818;
                                            float _22863 = clamp(((_22840 * _22840) - _22818) / max(_22860, 9.9999999600419720025001879548654e-13f), 0.0f, 1.0f);
                                            float _22865 = sqrt(_22863);
                                            float _22868 = sqrt(1.0f - _22863);
                                            float _22877 = atan2(_22820 * _22865, _22815 * _22868);
                                            float _22887 = max(_22820, 1.0000000031710768509710513471353e-30f);
                                            float _22923 = clamp(sqrt(_22815 / _22887), 0.0f, 1.0f) * 63.0f;
                                            float2 _22924 = float2(clamp(atan2(_22865, _22868) * 0.636619746685028076171875f, 0.0f, 1.0f) * 255.0f, _22923);
                                            float2 _22926 = floor(_22924);
                                            float2 _22930 = min(_22926 + 1.0f.xx, float2(255.0f, 63.0f));
                                            float2 _22933 = _22924 - _22926;
                                            float4 _23006 = ArcTex.Sample(ArcSampler, (_22926 + 0.5f.xx) * float2(0.00390625f, 0.015625f));
                                            float4 _23049 = ArcTex.Sample(ArcSampler, (float2(_22930.x, _22926.y) + 0.5f.xx) * float2(0.00390625f, 0.015625f));
                                            float2 _22944 = _22933.x.xx;
                                            float4 _23092 = ArcTex.Sample(ArcSampler, (float2(_22926.x, _22930.y) + 0.5f.xx) * float2(0.00390625f, 0.015625f));
                                            float4 _23135 = ArcTex.Sample(ArcSampler, (_22930 + 0.5f.xx) * float2(0.00390625f, 0.015625f));
                                            float _22892 = p_Meta2.z * lerp(lerp(float2(_47722, (floor((_23006.z * 255.0f) + 0.5f) * 256.0f) + floor((_23006.w * 255.0f) + 0.5f)) * 1.525902189314365386962890625e-05f.xx, float2(_47722, (floor((_23049.z * 255.0f) + 0.5f) * 256.0f) + floor((_23049.w * 255.0f) + 0.5f)) * 1.525902189314365386962890625e-05f.xx, _22944), lerp(float2(_47722, (floor((_23092.z * 255.0f) + 0.5f) * 256.0f) + floor((_23092.w * 255.0f) + 0.5f)) * 1.525902189314365386962890625e-05f.xx, float2(_47722, (floor((_23135.z * 255.0f) + 0.5f) * 256.0f) + floor((_23135.w * 255.0f) + 0.5f)) * 1.525902189314365386962890625e-05f.xx, _22944), _22933.y.xx).y;
                                            float _22905 = (_22820 - _22821) * _22868;
                                            float2 _22463 = abs(_22439);
                                            bool _22472 = _22877 > 9.9999999747524270787835121154785e-07f;
                                            bool _22476 = _22472 && (atan2(_22463.y, _22463.x - _22905) < _22877);
                                            float _42950;
                                            float _42960;
                                            if (_22476)
                                            {
                                                float2 _22482 = _22463 - float2(_22905, 0.0f);
                                                float _22484 = _22482.x;
                                                float _22495 = _22482.y;
                                                float _22505 = ((_22484 * _22484) / _22854) + ((_22495 * _22495) / _22818);
                                                float _22516 = ((2.0f * _22905) * _22484) / _22854;
                                                float _22543 = (sqrt(max((_22516 * _22516) - ((4.0f * _22505) * (((_22905 * _22905) / _22854) - 1.0f)), 0.0f)) - _22516) / max(2.0f * _22505, 1.0000000031710768509710513471353e-30f);
                                                float2 _23154 = float2(clamp(atan2((_22543 * _22495) / _22815, (_22905 + (_22543 * _22484)) / _22820) * 0.636619746685028076171875f, 0.0f, 1.0f) * 255.0f, clamp(sqrt(_50048.y), 0.0f, 1.0f) * 63.0f);
                                                float2 _23156 = floor(_23154);
                                                float2 _23160 = min(_23156 + 1.0f.xx, float2(255.0f, 63.0f));
                                                float2 _23163 = _23154 - _23156;
                                                float4 _23236 = ArcTex.Sample(ArcSampler, (_23156 + 0.5f.xx) * float2(0.00390625f, 0.015625f));
                                                float4 _23279 = ArcTex.Sample(ArcSampler, (float2(_23160.x, _23156.y) + 0.5f.xx) * float2(0.00390625f, 0.015625f));
                                                float2 _23174 = _23163.x.xx;
                                                float4 _23322 = ArcTex.Sample(ArcSampler, (float2(_23156.x, _23160.y) + 0.5f.xx) * float2(0.00390625f, 0.015625f));
                                                float4 _23365 = ArcTex.Sample(ArcSampler, (_23160 + 0.5f.xx) * float2(0.00390625f, 0.015625f));
                                                _42960 = 0.0f;
                                                _42950 = p_Meta2.z * lerp(lerp(float2(_47722, (floor((_23236.z * 255.0f) + 0.5f) * 256.0f) + floor((_23236.w * 255.0f) + 0.5f)) * 1.525902189314365386962890625e-05f.xx, float2(_47722, (floor((_23279.z * 255.0f) + 0.5f) * 256.0f) + floor((_23279.w * 255.0f) + 0.5f)) * 1.525902189314365386962890625e-05f.xx, _23174), lerp(float2(_47722, (floor((_23322.z * 255.0f) + 0.5f) * 256.0f) + floor((_23322.w * 255.0f) + 0.5f)) * 1.525902189314365386962890625e-05f.xx, float2(_47722, (floor((_23365.z * 255.0f) + 0.5f) * 256.0f) + floor((_23365.w * 255.0f) + 0.5f)) * 1.525902189314365386962890625e-05f.xx, _23174), _23163.y.xx).y;
                                            }
                                            else
                                            {
                                                float2 _22578 = normalize(_42944 / _50048);
                                                float _22581 = _22578.y;
                                                float2 _23384 = float2(clamp(atan2(_22581, _22578.x) * 0.636619746685028076171875f, 0.0f, 1.0f) * 255.0f, clamp(sqrt(_50048.y), 0.0f, 1.0f) * 63.0f);
                                                float2 _23386 = floor(_23384);
                                                float2 _23390 = min(_23386 + 1.0f.xx, float2(255.0f, 63.0f));
                                                float2 _23393 = _23384 - _23386;
                                                float4 _23466 = ArcTex.Sample(ArcSampler, (_23386 + 0.5f.xx) * float2(0.00390625f, 0.015625f));
                                                float4 _23509 = ArcTex.Sample(ArcSampler, (float2(_23390.x, _23386.y) + 0.5f.xx) * float2(0.00390625f, 0.015625f));
                                                float2 _23404 = _23393.x.xx;
                                                float4 _23552 = ArcTex.Sample(ArcSampler, (float2(_23386.x, _23390.y) + 0.5f.xx) * float2(0.00390625f, 0.015625f));
                                                float4 _23595 = ArcTex.Sample(ArcSampler, (_23390 + 0.5f.xx) * float2(0.00390625f, 0.015625f));
                                                _42960 = _22581 * _22815;
                                                _42950 = p_Meta2.z * lerp(lerp(float2(_47722, (floor((_23466.z * 255.0f) + 0.5f) * 256.0f) + floor((_23466.w * 255.0f) + 0.5f)) * 1.525902189314365386962890625e-05f.xx, float2(_47722, (floor((_23509.z * 255.0f) + 0.5f) * 256.0f) + floor((_23509.w * 255.0f) + 0.5f)) * 1.525902189314365386962890625e-05f.xx, _23404), lerp(float2(_47722, (floor((_23552.z * 255.0f) + 0.5f) * 256.0f) + floor((_23552.w * 255.0f) + 0.5f)) * 1.525902189314365386962890625e-05f.xx, float2(_47722, (floor((_23595.z * 255.0f) + 0.5f) * 256.0f) + floor((_23595.w * 255.0f) + 0.5f)) * 1.525902189314365386962890625e-05f.xx, _23404), _23393.y.xx).y;
                                            }
                                            float _22599 = _22439.x;
                                            float _22602 = _22439.y;
                                            bool _22603 = _22602 >= 0.0f;
                                            float _22606 = 4.0f * p_Meta2.z;
                                            float _22614 = 2.0f * p_Meta2.z;
                                            float _22622 = (_22599 >= 0.0f) ? (_22603 ? _42950 : (_22606 - _42950)) : (_22603 ? (_22614 - _42950) : (_22614 + _42950));
                                            float _23675 = floor(p_Meta2.y * 0.00048828125f);
                                            float _23680 = p_Meta2.y - (_23675 * 2048.0f);
                                            bool _23683 = _23680 >= 2048.0f;
                                            float _42951;
                                            float _42953;
                                            if (_23683)
                                            {
                                                _42953 = _23680 - 2048.0f;
                                                _42951 = _23675 + 1.0f;
                                            }
                                            else
                                            {
                                                float _42952;
                                                float _42954;
                                                if (_23680 < 0.0f)
                                                {
                                                    _42954 = _23680 + 2048.0f;
                                                    _42952 = _23675 - 1.0f;
                                                }
                                                else
                                                {
                                                    _42954 = _23680;
                                                    _42952 = _23675;
                                                }
                                                _42953 = _42954;
                                                _42951 = _42952;
                                            }
                                            float _23614 = _42951 * 0.0002442598924972116947174072265625f;
                                            float _23623 = frac(((_22622 / p_Meta2.x) - (_42953 * 0.000488519784994423389434814453125f)) + 0.5f) - 0.5f;
                                            float _23655 = _22622 - (min(frac(_23623 - _23614), frac(_23623 + _23614)) * p_Meta2.x);
                                            float _23661 = _22622 + (min(frac(_23614 - _23623), frac((_42951 * (-0.0002442598924972116947174072265625f)) - _23623)) * p_Meta2.x);
                                            float _23734 = max(_22606, 9.9999999747524270787835121154785e-07f);
                                            float _23739 = _23655 - (floor(_23655 / _23734) * _22606);
                                            float _23742 = max(p_Meta2.z, 9.9999999747524270787835121154785e-07f);
                                            float _23745 = min(floor(_23739 / _23742), 3.0f);
                                            float _23750 = _23739 - (_23745 * p_Meta2.z);
                                            bool _23752 = _23745 < 0.5f;
                                            float _23756 = (_23752 || (_23745 > 2.5f)) ? 1.0f : (-1.0f);
                                            float _23759 = (_23745 < 1.5f) ? 1.0f : (-1.0f);
                                            float _23775 = (((_23752 || ((_23745 > 1.5f) && (_23745 < 2.5f))) ? 1.0f : (-1.0f)) > 0.0f) ? _23750 : (p_Meta2.z - _23750);
                                            float2 _23951 = float2(clamp(sqrt(clamp(_23775 / _23742, 0.0f, 1.0f)), 0.0f, 1.0f) * 255.0f, _22923);
                                            float2 _23953 = floor(_23951);
                                            float2 _23957 = min(_23953 + 1.0f.xx, float2(255.0f, 63.0f));
                                            float2 _23960 = _23951 - _23953;
                                            float2 _23998 = (_23953 + 0.5f.xx) * float2(0.00390625f, 0.015625f);
                                            float4 _24033 = ArcTex.Sample(ArcSampler, _23998);
                                            float2 _24041 = (float2(_23957.x, _23953.y) + 0.5f.xx) * float2(0.00390625f, 0.015625f);
                                            float4 _24076 = ArcTex.Sample(ArcSampler, _24041);
                                            float2 _23971 = _23960.x.xx;
                                            float2 _24084 = (float2(_23953.x, _23957.y) + 0.5f.xx) * float2(0.00390625f, 0.015625f);
                                            float4 _24119 = ArcTex.Sample(ArcSampler, _24084);
                                            float2 _24127 = (_23957 + 0.5f.xx) * float2(0.00390625f, 0.015625f);
                                            float4 _24162 = ArcTex.Sample(ArcSampler, _24127);
                                            float2 _23989 = _23960.y.xx;
                                            float _23792 = sin(lerp(lerp(float2((floor((_24033.x * 255.0f) + 0.5f) * 256.0f) + floor((_24033.y * 255.0f) + 0.5f), _47722) * 1.525902189314365386962890625e-05f.xx, float2((floor((_24076.x * 255.0f) + 0.5f) * 256.0f) + floor((_24076.y * 255.0f) + 0.5f), _47722) * 1.525902189314365386962890625e-05f.xx, _23971), lerp(float2((floor((_24119.x * 255.0f) + 0.5f) * 256.0f) + floor((_24119.y * 255.0f) + 0.5f), _47722) * 1.525902189314365386962890625e-05f.xx, float2((floor((_24162.x * 255.0f) + 0.5f) * 256.0f) + floor((_24162.y * 255.0f) + 0.5f), _47722) * 1.525902189314365386962890625e-05f.xx, _23971), _23989).x * 1.57079637050628662109375f);
                                            float4 _24263 = ArcTex.Sample(ArcSampler, _23998);
                                            float4 _24306 = ArcTex.Sample(ArcSampler, _24041);
                                            float4 _24349 = ArcTex.Sample(ArcSampler, _24084);
                                            float4 _24392 = ArcTex.Sample(ArcSampler, _24127);
                                            float _23810 = cos(lerp(lerp(float2((floor((_24263.x * 255.0f) + 0.5f) * 256.0f) + floor((_24263.y * 255.0f) + 0.5f), _47722) * 1.525902189314365386962890625e-05f.xx, float2((floor((_24306.x * 255.0f) + 0.5f) * 256.0f) + floor((_24306.y * 255.0f) + 0.5f), _47722) * 1.525902189314365386962890625e-05f.xx, _23971), lerp(float2((floor((_24349.x * 255.0f) + 0.5f) * 256.0f) + floor((_24349.y * 255.0f) + 0.5f), _47722) * 1.525902189314365386962890625e-05f.xx, float2((floor((_24392.x * 255.0f) + 0.5f) * 256.0f) + floor((_24392.y * 255.0f) + 0.5f), _47722) * 1.525902189314365386962890625e-05f.xx, _23971), _23989).x * 1.57079637050628662109375f);
                                            float2 _23826 = float2((_23756 * _22820) * _23810, (_23759 * _22815) * _23792);
                                            float2 _23858 = (-float2((_23756 * _22815) * _23810, (_23759 * _22820) * _23792)) / max(length(float2(_22820 * _23792, _22815 * _23810)), 1.0000000031710768509710513471353e-30f).xx;
                                            float _23862 = p_Meta1.x + (_14846 * _12863);
                                            float _42955;
                                            float2 _50071;
                                            if (_22472 && (_23775 < _22892))
                                            {
                                                float2 _23875 = float2(_23756 * _22905, 0.0f) - _23826;
                                                float _23878 = max(length(_23875), 1.0000000031710768509710513471353e-30f);
                                                _50071 = _23875 / _23878.xx;
                                                _42955 = _23878;
                                            }
                                            else
                                            {
                                                _50071 = _23858;
                                                _42955 = _23862;
                                            }
                                            float _23886 = p_Meta1.x * 0.5f;
                                            float _23891 = _23886 / max(dot(_50071, _23858), 0.25f);
                                            float2 _23896 = _23826 + (_50071 * _23891);
                                            float2 _23899 = _22439 - _23826;
                                            float _23902 = dot(_23899, _50071);
                                            float _23909 = -_23862;
                                            float _23912 = max(_23902 - _42955, _23909 - _23902);
                                            float _23921 = dot(_23899, float2(_50071.y, -_50071.x));
                                            float _24429 = _23661 - (floor(_23661 / _23734) * _22606);
                                            float _24435 = min(floor(_24429 / _23742), 3.0f);
                                            float _24440 = _24429 - (_24435 * p_Meta2.z);
                                            bool _24442 = _24435 < 0.5f;
                                            float _24446 = (_24442 || (_24435 > 2.5f)) ? 1.0f : (-1.0f);
                                            float _24449 = (_24435 < 1.5f) ? 1.0f : (-1.0f);
                                            float _24465 = (((_24442 || ((_24435 > 1.5f) && (_24435 < 2.5f))) ? 1.0f : (-1.0f)) > 0.0f) ? _24440 : (p_Meta2.z - _24440);
                                            float2 _24641 = float2(clamp(sqrt(clamp(_24465 / _23742, 0.0f, 1.0f)), 0.0f, 1.0f) * 255.0f, _22923);
                                            float2 _24643 = floor(_24641);
                                            float2 _24647 = min(_24643 + 1.0f.xx, float2(255.0f, 63.0f));
                                            float2 _24650 = _24641 - _24643;
                                            float2 _24688 = (_24643 + 0.5f.xx) * float2(0.00390625f, 0.015625f);
                                            float4 _24723 = ArcTex.Sample(ArcSampler, _24688);
                                            float2 _24731 = (float2(_24647.x, _24643.y) + 0.5f.xx) * float2(0.00390625f, 0.015625f);
                                            float4 _24766 = ArcTex.Sample(ArcSampler, _24731);
                                            float2 _24661 = _24650.x.xx;
                                            float2 _24774 = (float2(_24643.x, _24647.y) + 0.5f.xx) * float2(0.00390625f, 0.015625f);
                                            float4 _24809 = ArcTex.Sample(ArcSampler, _24774);
                                            float2 _24817 = (_24647 + 0.5f.xx) * float2(0.00390625f, 0.015625f);
                                            float4 _24852 = ArcTex.Sample(ArcSampler, _24817);
                                            float2 _24679 = _24650.y.xx;
                                            float _24482 = sin(lerp(lerp(float2((floor((_24723.x * 255.0f) + 0.5f) * 256.0f) + floor((_24723.y * 255.0f) + 0.5f), _47722) * 1.525902189314365386962890625e-05f.xx, float2((floor((_24766.x * 255.0f) + 0.5f) * 256.0f) + floor((_24766.y * 255.0f) + 0.5f), _47722) * 1.525902189314365386962890625e-05f.xx, _24661), lerp(float2((floor((_24809.x * 255.0f) + 0.5f) * 256.0f) + floor((_24809.y * 255.0f) + 0.5f), _47722) * 1.525902189314365386962890625e-05f.xx, float2((floor((_24852.x * 255.0f) + 0.5f) * 256.0f) + floor((_24852.y * 255.0f) + 0.5f), _47722) * 1.525902189314365386962890625e-05f.xx, _24661), _24679).x * 1.57079637050628662109375f);
                                            float4 _24953 = ArcTex.Sample(ArcSampler, _24688);
                                            float4 _24996 = ArcTex.Sample(ArcSampler, _24731);
                                            float4 _25039 = ArcTex.Sample(ArcSampler, _24774);
                                            float4 _25082 = ArcTex.Sample(ArcSampler, _24817);
                                            float _24500 = cos(lerp(lerp(float2((floor((_24953.x * 255.0f) + 0.5f) * 256.0f) + floor((_24953.y * 255.0f) + 0.5f), _47722) * 1.525902189314365386962890625e-05f.xx, float2((floor((_24996.x * 255.0f) + 0.5f) * 256.0f) + floor((_24996.y * 255.0f) + 0.5f), _47722) * 1.525902189314365386962890625e-05f.xx, _24661), lerp(float2((floor((_25039.x * 255.0f) + 0.5f) * 256.0f) + floor((_25039.y * 255.0f) + 0.5f), _47722) * 1.525902189314365386962890625e-05f.xx, float2((floor((_25082.x * 255.0f) + 0.5f) * 256.0f) + floor((_25082.y * 255.0f) + 0.5f), _47722) * 1.525902189314365386962890625e-05f.xx, _24661), _24679).x * 1.57079637050628662109375f);
                                            float2 _24516 = float2((_24446 * _22820) * _24500, (_24449 * _22815) * _24482);
                                            float2 _24548 = (-float2((_24446 * _22815) * _24500, (_24449 * _22820) * _24482)) / max(length(float2(_22820 * _24482, _22815 * _24500)), 1.0000000031710768509710513471353e-30f).xx;
                                            float _42956;
                                            float2 _50099;
                                            if (_22472 && (_24465 < _22892))
                                            {
                                                float2 _24565 = float2(_24446 * _22905, 0.0f) - _24516;
                                                float _24568 = max(length(_24565), 1.0000000031710768509710513471353e-30f);
                                                _50099 = _24565 / _24568.xx;
                                                _42956 = _24568;
                                            }
                                            else
                                            {
                                                _50099 = _24548;
                                                _42956 = _23862;
                                            }
                                            float _24581 = _23886 / max(dot(_50099, _24548), 0.25f);
                                            float2 _24586 = _24516 + (_50099 * _24581);
                                            float2 _24589 = _22439 - _24516;
                                            float _24592 = dot(_24589, _50099);
                                            float _24602 = max(_24592 - _42956, _23909 - _24592);
                                            float _24612 = (-1.0f) * dot(_24589, float2(_50099.y, -_50099.x));
                                            float2 _51252 = float2(_23891, _24581);
                                            float _22656 = min(length(max(float2(_23912, _23921), 0.0f.xx)) + min(max(_23912, _23921), 0.0f), length(max(float2(_24602, _24612), 0.0f.xx)) + min(max(_24602, _24612), 0.0f));
                                            float _22663 = (((abs(_23623) - _23614) * p_Meta2.x) >= 0.0f) ? _22656 : (-_22656);
                                            float _22685 = abs(_22599) - (_22860 / _22887);
                                            float _22690 = length(float2(max(_22685, 0.0f), _22602));
                                            float _22701 = sqrt((_42943 * _42943) + ((4.0f * abs(_22602)) * _42960));
                                            float _42972;
                                            if ((!_22476) && (_22701 < _23862))
                                            {
                                                float _22714 = _22606 - _22622;
                                                float _42962;
                                                float _42964;
                                                if (_23683)
                                                {
                                                    _42964 = _23680 - 2048.0f;
                                                    _42962 = _23675 + 1.0f;
                                                }
                                                else
                                                {
                                                    float _42963;
                                                    float _42965;
                                                    if (_23680 < 0.0f)
                                                    {
                                                        _42965 = _23680 + 2048.0f;
                                                        _42963 = _23675 - 1.0f;
                                                    }
                                                    else
                                                    {
                                                        _42965 = _23680;
                                                        _42963 = _23675;
                                                    }
                                                    _42964 = _42965;
                                                    _42962 = _42963;
                                                }
                                                float _25101 = _42962 * 0.0002442598924972116947174072265625f;
                                                float _25110 = frac(((_22714 / p_Meta2.x) - (_42964 * 0.000488519784994423389434814453125f)) + 0.5f) - 0.5f;
                                                float _25142 = _22714 - (min(frac(_25110 - _25101), frac(_25110 + _25101)) * p_Meta2.x);
                                                float _25148 = _22714 + (min(frac(_25101 - _25110), frac((_42962 * (-0.0002442598924972116947174072265625f)) - _25110)) * p_Meta2.x);
                                                float _25226 = _25142 - (floor(_25142 / _23734) * _22606);
                                                float _25232 = min(floor(_25226 / _23742), 3.0f);
                                                float _25237 = _25226 - (_25232 * p_Meta2.z);
                                                bool _25239 = _25232 < 0.5f;
                                                float _25243 = (_25239 || (_25232 > 2.5f)) ? 1.0f : (-1.0f);
                                                float _25246 = (_25232 < 1.5f) ? 1.0f : (-1.0f);
                                                float _25262 = (((_25239 || ((_25232 > 1.5f) && (_25232 < 2.5f))) ? 1.0f : (-1.0f)) > 0.0f) ? _25237 : (p_Meta2.z - _25237);
                                                float2 _25438 = float2(clamp(sqrt(clamp(_25262 / _23742, 0.0f, 1.0f)), 0.0f, 1.0f) * 255.0f, _22923);
                                                float2 _25440 = floor(_25438);
                                                float2 _25444 = min(_25440 + 1.0f.xx, float2(255.0f, 63.0f));
                                                float2 _25447 = _25438 - _25440;
                                                float2 _25485 = (_25440 + 0.5f.xx) * float2(0.00390625f, 0.015625f);
                                                float4 _25520 = ArcTex.Sample(ArcSampler, _25485);
                                                float2 _25528 = (float2(_25444.x, _25440.y) + 0.5f.xx) * float2(0.00390625f, 0.015625f);
                                                float4 _25563 = ArcTex.Sample(ArcSampler, _25528);
                                                float2 _25458 = _25447.x.xx;
                                                float2 _25571 = (float2(_25440.x, _25444.y) + 0.5f.xx) * float2(0.00390625f, 0.015625f);
                                                float4 _25606 = ArcTex.Sample(ArcSampler, _25571);
                                                float2 _25614 = (_25444 + 0.5f.xx) * float2(0.00390625f, 0.015625f);
                                                float4 _25649 = ArcTex.Sample(ArcSampler, _25614);
                                                float2 _25476 = _25447.y.xx;
                                                float _25279 = sin(lerp(lerp(float2((floor((_25520.x * 255.0f) + 0.5f) * 256.0f) + floor((_25520.y * 255.0f) + 0.5f), _47722) * 1.525902189314365386962890625e-05f.xx, float2((floor((_25563.x * 255.0f) + 0.5f) * 256.0f) + floor((_25563.y * 255.0f) + 0.5f), _47722) * 1.525902189314365386962890625e-05f.xx, _25458), lerp(float2((floor((_25606.x * 255.0f) + 0.5f) * 256.0f) + floor((_25606.y * 255.0f) + 0.5f), _47722) * 1.525902189314365386962890625e-05f.xx, float2((floor((_25649.x * 255.0f) + 0.5f) * 256.0f) + floor((_25649.y * 255.0f) + 0.5f), _47722) * 1.525902189314365386962890625e-05f.xx, _25458), _25476).x * 1.57079637050628662109375f);
                                                float4 _25750 = ArcTex.Sample(ArcSampler, _25485);
                                                float4 _25793 = ArcTex.Sample(ArcSampler, _25528);
                                                float4 _25836 = ArcTex.Sample(ArcSampler, _25571);
                                                float4 _25879 = ArcTex.Sample(ArcSampler, _25614);
                                                float _25297 = cos(lerp(lerp(float2((floor((_25750.x * 255.0f) + 0.5f) * 256.0f) + floor((_25750.y * 255.0f) + 0.5f), _47722) * 1.525902189314365386962890625e-05f.xx, float2((floor((_25793.x * 255.0f) + 0.5f) * 256.0f) + floor((_25793.y * 255.0f) + 0.5f), _47722) * 1.525902189314365386962890625e-05f.xx, _25458), lerp(float2((floor((_25836.x * 255.0f) + 0.5f) * 256.0f) + floor((_25836.y * 255.0f) + 0.5f), _47722) * 1.525902189314365386962890625e-05f.xx, float2((floor((_25879.x * 255.0f) + 0.5f) * 256.0f) + floor((_25879.y * 255.0f) + 0.5f), _47722) * 1.525902189314365386962890625e-05f.xx, _25458), _25476).x * 1.57079637050628662109375f);
                                                float2 _25313 = float2((_25243 * _22820) * _25297, (_25246 * _22815) * _25279);
                                                float2 _25345 = (-float2((_25243 * _22815) * _25297, (_25246 * _22820) * _25279)) / max(length(float2(_22820 * _25279, _22815 * _25297)), 1.0000000031710768509710513471353e-30f).xx;
                                                float _42966;
                                                float2 _50127;
                                                if (_22472 && (_25262 < _22892))
                                                {
                                                    float2 _25362 = float2(_25243 * _22905, 0.0f) - _25313;
                                                    float _25365 = max(length(_25362), 1.0000000031710768509710513471353e-30f);
                                                    _50127 = _25362 / _25365.xx;
                                                    _42966 = _25365;
                                                }
                                                else
                                                {
                                                    _50127 = _25345;
                                                    _42966 = _23862;
                                                }
                                                float _25378 = _23886 / max(dot(_50127, _25345), 0.25f);
                                                float2 _25386 = _22439 - _25313;
                                                float _25389 = dot(_25386, _50127);
                                                float _25399 = max(_25389 - _42966, _23909 - _25389);
                                                float _25408 = dot(_25386, float2(_50127.y, -_50127.x));
                                                float _25916 = _25148 - (floor(_25148 / _23734) * _22606);
                                                float _25922 = min(floor(_25916 / _23742), 3.0f);
                                                float _25927 = _25916 - (_25922 * p_Meta2.z);
                                                bool _25929 = _25922 < 0.5f;
                                                float _25933 = (_25929 || (_25922 > 2.5f)) ? 1.0f : (-1.0f);
                                                float _25936 = (_25922 < 1.5f) ? 1.0f : (-1.0f);
                                                float _25952 = (((_25929 || ((_25922 > 1.5f) && (_25922 < 2.5f))) ? 1.0f : (-1.0f)) > 0.0f) ? _25927 : (p_Meta2.z - _25927);
                                                float2 _26128 = float2(clamp(sqrt(clamp(_25952 / _23742, 0.0f, 1.0f)), 0.0f, 1.0f) * 255.0f, _22923);
                                                float2 _26130 = floor(_26128);
                                                float2 _26134 = min(_26130 + 1.0f.xx, float2(255.0f, 63.0f));
                                                float2 _26137 = _26128 - _26130;
                                                float2 _26175 = (_26130 + 0.5f.xx) * float2(0.00390625f, 0.015625f);
                                                float4 _26210 = ArcTex.Sample(ArcSampler, _26175);
                                                float2 _26218 = (float2(_26134.x, _26130.y) + 0.5f.xx) * float2(0.00390625f, 0.015625f);
                                                float4 _26253 = ArcTex.Sample(ArcSampler, _26218);
                                                float2 _26148 = _26137.x.xx;
                                                float2 _26261 = (float2(_26130.x, _26134.y) + 0.5f.xx) * float2(0.00390625f, 0.015625f);
                                                float4 _26296 = ArcTex.Sample(ArcSampler, _26261);
                                                float2 _26304 = (_26134 + 0.5f.xx) * float2(0.00390625f, 0.015625f);
                                                float4 _26339 = ArcTex.Sample(ArcSampler, _26304);
                                                float2 _26166 = _26137.y.xx;
                                                float _25969 = sin(lerp(lerp(float2((floor((_26210.x * 255.0f) + 0.5f) * 256.0f) + floor((_26210.y * 255.0f) + 0.5f), _47722) * 1.525902189314365386962890625e-05f.xx, float2((floor((_26253.x * 255.0f) + 0.5f) * 256.0f) + floor((_26253.y * 255.0f) + 0.5f), _47722) * 1.525902189314365386962890625e-05f.xx, _26148), lerp(float2((floor((_26296.x * 255.0f) + 0.5f) * 256.0f) + floor((_26296.y * 255.0f) + 0.5f), _47722) * 1.525902189314365386962890625e-05f.xx, float2((floor((_26339.x * 255.0f) + 0.5f) * 256.0f) + floor((_26339.y * 255.0f) + 0.5f), _47722) * 1.525902189314365386962890625e-05f.xx, _26148), _26166).x * 1.57079637050628662109375f);
                                                float4 _26440 = ArcTex.Sample(ArcSampler, _26175);
                                                float4 _26483 = ArcTex.Sample(ArcSampler, _26218);
                                                float4 _26526 = ArcTex.Sample(ArcSampler, _26261);
                                                float4 _26569 = ArcTex.Sample(ArcSampler, _26304);
                                                float _25987 = cos(lerp(lerp(float2((floor((_26440.x * 255.0f) + 0.5f) * 256.0f) + floor((_26440.y * 255.0f) + 0.5f), _47722) * 1.525902189314365386962890625e-05f.xx, float2((floor((_26483.x * 255.0f) + 0.5f) * 256.0f) + floor((_26483.y * 255.0f) + 0.5f), _47722) * 1.525902189314365386962890625e-05f.xx, _26148), lerp(float2((floor((_26526.x * 255.0f) + 0.5f) * 256.0f) + floor((_26526.y * 255.0f) + 0.5f), _47722) * 1.525902189314365386962890625e-05f.xx, float2((floor((_26569.x * 255.0f) + 0.5f) * 256.0f) + floor((_26569.y * 255.0f) + 0.5f), _47722) * 1.525902189314365386962890625e-05f.xx, _26148), _26166).x * 1.57079637050628662109375f);
                                                float2 _26003 = float2((_25933 * _22820) * _25987, (_25936 * _22815) * _25969);
                                                float2 _26035 = (-float2((_25933 * _22815) * _25987, (_25936 * _22820) * _25969)) / max(length(float2(_22820 * _25969, _22815 * _25987)), 1.0000000031710768509710513471353e-30f).xx;
                                                float _42967;
                                                float2 _50182;
                                                if (_22472 && (_25952 < _22892))
                                                {
                                                    float2 _26052 = float2(_25933 * _22905, 0.0f) - _26003;
                                                    float _26055 = max(length(_26052), 1.0000000031710768509710513471353e-30f);
                                                    _50182 = _26052 / _26055.xx;
                                                    _42967 = _26055;
                                                }
                                                else
                                                {
                                                    _50182 = _26035;
                                                    _42967 = _23862;
                                                }
                                                float _26068 = _23886 / max(dot(_50182, _26035), 0.25f);
                                                float2 _26076 = _22439 - _26003;
                                                float _26079 = dot(_26076, _50182);
                                                float _26089 = max(_26079 - _42967, _23909 - _26079);
                                                float _26099 = (-1.0f) * dot(_26076, float2(_50182.y, -_50182.x));
                                                float _22747 = min(length(max(float2(_25399, _25408), 0.0f.xx)) + min(max(_25399, _25408), 0.0f), length(max(float2(_26089, _26099), 0.0f.xx)) + min(max(_26089, _26099), 0.0f));
                                                float _22754 = (((abs(_25110) - _25101) * p_Meta2.x) >= 0.0f) ? _22747 : (-_22747);
                                                if (_13572)
                                                {
                                                    float2 _26575 = _23886.xx;
                                                    float2 _26576 = max(_51252, _26575);
                                                    float2 _26607 = max(float2(_25378, _26068), _26575);
                                                    _42984 = min(max(abs(_42943 + _23886) - _23886, min(_22663, min(length(_22439 - _23896) - _26576.x, length(_22439 - _24586) - _26576.y))), max(max(abs(_23886 - _22701) - _23886, min(_22754, min(length(_22439 - (_25313 + (_50127 * _25378))) - _26607.x, length(_22439 - (_26003 + (_50182 * _26068))) - _26607.y))), _22685));
                                                    break;
                                                }
                                                _42972 = min(max(_22663, -_22690), max(_22754, _22690));
                                            }
                                            else
                                            {
                                                _42972 = _22663;
                                            }
                                            float2 _26638 = max(_51252, _23886.xx);
                                            _42984 = _13572 ? max(abs(_42943 + _23886) - _23886, min(_42972, min(length(_22439 - _23896) - _26638.x, length(_22439 - _24586) - _26638.y))) : _42972;
                                            break;
                                        } while(false);
                                        _44675 = _42984;
                                    }
                                    else
                                    {
                                        _44675 = 1.0f;
                                    }
                                    _46010 = _13565;
                                    _44765 = 1.0f;
                                    _44674 = _44675;
                                    _44346 = 0.0f;
                                    _44187 = 0.0f;
                                    _44021 = float2(1.0f, 0.0f);
                                    _43864 = 0.0f;
                                    _43707 = false;
                                    _43600 = _42711;
                                    _43501 = _42943;
                                    _43403 = p_TexCoord.z;
                                }
                                else
                                {
                                    float _43421;
                                    float _43514;
                                    float _43613;
                                    bool _43725;
                                    float _43882;
                                    float2 _44039;
                                    float _44205;
                                    float _44364;
                                    float _44681;
                                    float _44783;
                                    bool _46017;
                                    if (_42687 < 7.5f)
                                    {
                                        float _26667 = abs(p_TexCoord.x);
                                        float2 _48773 = _12969;
                                        _48773.x = _26667;
                                        bool _13601 = _42711 >= 0.5f;
                                        float _43883;
                                        float2 _44040;
                                        float _44206;
                                        if (_13601)
                                        {
                                            _44206 = length(_12969) - p_Meta1.z;
                                            _44040 = float2(p_Meta1.w, p_Meta2.w);
                                            _43883 = (atan2(p_TexCoord.x, p_TexCoord.y) + atan2(p_Meta2.x, p_Meta2.y)) * p_Meta1.z;
                                        }
                                        else
                                        {
                                            _44206 = 0.0f;
                                            _44040 = float2(1.0f, 0.0f);
                                            _43883 = 0.0f;
                                        }
                                        _46017 = false;
                                        _44783 = 1.0f;
                                        _44681 = 1.0f;
                                        _44364 = _13601 ? p_Meta2.z : 0.0f;
                                        _44205 = _44206;
                                        _44039 = _44040;
                                        _43882 = _43883;
                                        _43725 = _13601;
                                        _43613 = _42711;
                                        _43514 = (((p_Meta2.y * _26667) > (p_Meta2.x * p_TexCoord.y)) ? length(_48773 - (p_Meta2.xy * p_Meta1.z)) : abs(length(_48773) - p_Meta1.z)) - p_Meta2.z;
                                        _43421 = p_TexCoord.z;
                                    }
                                    else
                                    {
                                        float _43423;
                                        float _43516;
                                        float _43615;
                                        bool _43727;
                                        float _43884;
                                        float2 _44041;
                                        float _44207;
                                        float _44366;
                                        float _44683;
                                        float _44785;
                                        bool _46019;
                                        if (_42687 < 8.5f)
                                        {
                                            float2 _48782 = _12969;
                                            _48782.x = abs(p_TexCoord.x);
                                            float2 _26715 = mul(_48782, float2x2(float2(p_Meta2.xy), float2(-p_Meta2.y, p_Meta2.x)));
                                            float _26724 = _26715.x;
                                            bool _13641 = _42711 >= 0.5f;
                                            float _43885;
                                            float2 _44042;
                                            float _44208;
                                            if (_13641)
                                            {
                                                _44208 = length(_12969) - p_Meta1.z;
                                                _44042 = float2(p_Meta1.w, p_Meta2.w);
                                                _43885 = (atan2(p_TexCoord.x, p_TexCoord.y) + atan2(p_Meta2.y, p_Meta2.x)) * p_Meta1.z;
                                            }
                                            else
                                            {
                                                _44208 = 0.0f;
                                                _44042 = float2(1.0f, 0.0f);
                                                _43885 = 0.0f;
                                            }
                                            _46019 = false;
                                            _44785 = 1.0f;
                                            _44683 = 1.0f;
                                            _44366 = _13641 ? p_Meta2.z : 0.0f;
                                            _44207 = _44208;
                                            _44041 = _44042;
                                            _43884 = _43885;
                                            _43727 = _13641;
                                            _43615 = _42711;
                                            _43516 = max(abs(length(_26715) - p_Meta1.z) - p_Meta2.z, (p_Meta2.y > 0.0f) ? (length(float2(_26724, max(0.0f, abs(p_Meta1.z - _26715.y) - p_Meta2.z))) * sign(_26724)) : (-1000000.0f));
                                            _43423 = p_TexCoord.z;
                                        }
                                        else
                                        {
                                            float _43425;
                                            float _43518;
                                            float _43617;
                                            float _44685;
                                            float _44787;
                                            bool _46021;
                                            if (_42687 > 11.5f)
                                            {
                                                float2 _13675 = float2(p_Meta1.z, p_Meta1.w);
                                                bool _13679 = _42711 >= 0.5f;
                                                float4 _50045;
                                                if (_13679)
                                                {
                                                    float _26753 = floor(p_Meta2.x * 0.00048828125f);
                                                    float _26758 = p_Meta2.x - (_26753 * 2048.0f);
                                                    float _42803;
                                                    float _42805;
                                                    if (_26758 >= 2048.0f)
                                                    {
                                                        _42805 = _26758 - 2048.0f;
                                                        _42803 = _26753 + 1.0f;
                                                    }
                                                    else
                                                    {
                                                        float _42804;
                                                        float _42806;
                                                        if (_26758 < 0.0f)
                                                        {
                                                            _42806 = _26758 + 2048.0f;
                                                            _42804 = _26753 - 1.0f;
                                                        }
                                                        else
                                                        {
                                                            _42806 = _26758;
                                                            _42804 = _26753;
                                                        }
                                                        _42805 = _42806;
                                                        _42803 = _42804;
                                                    }
                                                    float _26788 = floor(p_Meta2.y * 0.00048828125f);
                                                    float _26793 = p_Meta2.y - (_26788 * 2048.0f);
                                                    float _42809;
                                                    float _42811;
                                                    if (_26793 >= 2048.0f)
                                                    {
                                                        _42811 = _26793 - 2048.0f;
                                                        _42809 = _26788 + 1.0f;
                                                    }
                                                    else
                                                    {
                                                        float _42810;
                                                        float _42812;
                                                        if (_26793 < 0.0f)
                                                        {
                                                            _42812 = _26793 + 2048.0f;
                                                            _42810 = _26788 - 1.0f;
                                                        }
                                                        else
                                                        {
                                                            _42812 = _26793;
                                                            _42810 = _26788;
                                                        }
                                                        _42811 = _42812;
                                                        _42809 = _42810;
                                                    }
                                                    _50045 = (float4(_42803, _42805, _42809, _42811) * 0.000488519784994423389434814453125f.xxxx) * min(p_Meta1.z, p_Meta1.w);
                                                }
                                                else
                                                {
                                                    _50045 = p_Meta2;
                                                }
                                                bool2 _26830 = (p_TexCoord.x > 0.0f).xx;
                                                float2 _26831 = float2(_26830.x ? _50045.xy.x : _50045.zw.x, _26830.y ? _50045.xy.y : _50045.zw.y);
                                                float2 _26843 = abs(_12969) - _13675;
                                                float2 _26850 = _26843.yx;
                                                bool2 _26852 = (_26843.y > _26843.x).xx;
                                                float2 _26853 = float2(_26852.x ? _26850.x : _26843.x, _26852.y ? _26850.y : _26843.y);
                                                float _26857 = _26853.y + ((p_TexCoord.y > 0.0f) ? _26831.y : _26831.x);
                                                float2 _48803 = _26853;
                                                _48803.y = _26857;
                                                float _26865 = _26853.x;
                                                float _42815;
                                                if ((_26857 < 0.0f) && ((_26857 - (_26865 * 0.4142135679721832275390625f)) < 0.0f))
                                                {
                                                    _42815 = _26865;
                                                }
                                                else
                                                {
                                                    float _42816;
                                                    if (_26865 < _26857)
                                                    {
                                                        _42816 = (_26865 + _26857) * 0.707106769084930419921875f;
                                                    }
                                                    else
                                                    {
                                                        _42816 = length(_48803);
                                                    }
                                                    _42815 = _42816;
                                                }
                                                float4 _26922 = (float4(p_TexCoord.x - p_TexCoord.y, p_TexCoord.x + p_TexCoord.y, (-p_TexCoord.x) - p_TexCoord.y, p_TexCoord.y - p_TexCoord.x) - (p_Meta1.z + p_Meta1.w).xxxx) + _50045;
                                                float _44686;
                                                float _44788;
                                                bool _46022;
                                                if (_13679)
                                                {
                                                    float _13715 = _14846 * _12863;
                                                    bool _13717 = _42711 >= 1.5f;
                                                    float _42912;
                                                    float _42913;
                                                    do
                                                    {
                                                        float _27097 = p_Meta1.x * 0.5f;
                                                        if (_13717)
                                                        {
                                                            float2 _27669;
                                                            float2 _27671;
                                                            float2 _27677;
                                                            float2 _27679;
                                                            float2 _27685;
                                                            float2 _27687;
                                                            float2 _27104 = max(_13675 - _27097.xx, 9.9999997473787516355514526367188e-05f.xx);
                                                            float _27111 = _27104.x;
                                                            float _27113 = _27104.y;
                                                            float4 _27116 = clamp(_50045 - (p_Meta1.x * 0.292893230915069580078125f).xxxx, 0.0f.xxxx, min(_27111, _27113).xxxx);
                                                            float _27296 = 2.0f * _27111;
                                                            float _27299 = 2.0f * _27113;
                                                            float _27308 = _27116.x;
                                                            float _27310 = _27116.y;
                                                            float _27312 = _27116.w;
                                                            float _27314 = _27116.z;
                                                            float4 _27315 = float4(_27308, _27310, _27312, _27314);
                                                            float4 _27329 = (float4(_27296, _27299, _27296, _27299) - _27315) - float4(_27314, _27308, _27310, _27312);
                                                            float4 _27341 = _27329 + (_27315 * 1.41421353816986083984375f);
                                                            float _27347 = _27341.x;
                                                            float _27352 = _27347 + _27341.y;
                                                            float _27360 = _27352 + _27341.z;
                                                            float4 _27361 = float4(0.0f, _27347, _27352, _27360);
                                                            float _27366 = _27360 + _27341.w;
                                                            float _42869;
                                                            do
                                                            {
                                                                float _27417 = (_27308 - _27310) * 0.5f;
                                                                float _27423 = (_27312 - _27310) * 0.5f;
                                                                float _42866;
                                                                if ((p_TexCoord.x >= ((_27314 - _27308) * 0.5f)) && (p_TexCoord.y <= _27417))
                                                                {
                                                                    _42866 = 0.0f;
                                                                }
                                                                else
                                                                {
                                                                    float _42867;
                                                                    if ((p_TexCoord.x >= _27423) && (p_TexCoord.y >= _27417))
                                                                    {
                                                                        _42867 = 1.0f;
                                                                    }
                                                                    else
                                                                    {
                                                                        _42867 = ((p_TexCoord.x <= _27423) && (p_TexCoord.y >= ((_27314 - _27312) * 0.5f))) ? 2.0f : 3.0f;
                                                                    }
                                                                    _42866 = _42867;
                                                                }
                                                                bool _27639 = _42866 < 1.5f;
                                                                float _27640 = _27639 ? 1.0f : (-1.0f);
                                                                bool _27642 = (((_42866 < 0.5f) || ((_42866 > 1.5f) && (_42866 < 2.5f))) ? 0.0f : 1.0f) < 0.5f;
                                                                float2 _27644 = float2(_27640, 0.0f);
                                                                float2 _27646 = float2(0.0f, _27640);
                                                                bool2 _27647 = _27642.xx;
                                                                float2 _27648 = float2(_27647.x ? _27644.x : _27646.x, _27647.y ? _27644.y : _27646.y);
                                                                float _27652 = _27648.x;
                                                                float _27654 = _27648.y;
                                                                float2 _27665 = float2(_27111 * (_27652 + _27654), _27113 * (_27654 - _27652));
                                                                _27669 = _27315.xy;
                                                                _27671 = _27315.zw;
                                                                bool2 _27672 = _27639.xx;
                                                                float2 _27673 = float2(_27672.x ? _27669.x : _27671.x, _27672.y ? _27669.y : _27671.y);
                                                                _27677 = _27329.xy;
                                                                _27679 = _27329.zw;
                                                                float2 _27681 = float2(_27672.x ? _27677.x : _27679.x, _27672.y ? _27677.y : _27679.y);
                                                                _27685 = _27361.xy;
                                                                _27687 = _27361.zw;
                                                                float2 _27689 = float2(_27672.x ? _27685.x : _27687.x, _27672.y ? _27685.y : _27687.y);
                                                                float _27696 = _27642 ? _27673.x : _27673.y;
                                                                float2 _27718 = float2(-_27654, _27652);
                                                                float2 _27488 = (_27648 + _27718) * 0.707106769084930419921875f;
                                                                float _27517 = (_27642 ? _27689.x : _27689.y) + (_27642 ? _27681.x : _27681.y);
                                                                float _27523 = _27517 + (_27696 * 1.41421353816986083984375f);
                                                                float2 _27529 = _12969 - (_27665 - (_27648 * _27696));
                                                                float _27531 = dot(_27529, _27648);
                                                                if (_27531 <= 0.0f)
                                                                {
                                                                    _42869 = _27517 + _27531;
                                                                    break;
                                                                }
                                                                float _27544 = dot(_27529, _27488);
                                                                if (_27544 <= 0.0f)
                                                                {
                                                                    _42869 = _27517;
                                                                    break;
                                                                }
                                                                float2 _27569 = _12969 - (_27665 + (_27718 * _27696));
                                                                if (dot(_27569, _27488) <= 0.0f)
                                                                {
                                                                    _42869 = _27517 + _27544;
                                                                    break;
                                                                }
                                                                float _27591 = dot(_27569, _27718);
                                                                if (_27591 <= 0.0f)
                                                                {
                                                                    _42869 = _27523;
                                                                    break;
                                                                }
                                                                _42869 = _27523 + _27591;
                                                                break;
                                                            } while(false);
                                                            float _27798 = floor(p_Meta2.w * 0.00048828125f);
                                                            float _27803 = p_Meta2.w - (_27798 * 2048.0f);
                                                            float _42870;
                                                            float _42872;
                                                            if (_27803 >= 2048.0f)
                                                            {
                                                                _42872 = _27803 - 2048.0f;
                                                                _42870 = _27798 + 1.0f;
                                                            }
                                                            else
                                                            {
                                                                float _42871;
                                                                float _42873;
                                                                if (_27803 < 0.0f)
                                                                {
                                                                    _42873 = _27803 + 2048.0f;
                                                                    _42871 = _27798 - 1.0f;
                                                                }
                                                                else
                                                                {
                                                                    _42873 = _27803;
                                                                    _42871 = _27798;
                                                                }
                                                                _42872 = _42873;
                                                                _42870 = _42871;
                                                            }
                                                            float _27737 = _42870 * 0.0002442598924972116947174072265625f;
                                                            float _27746 = frac(((_42869 / p_Meta2.z) - (_42872 * 0.000488519784994423389434814453125f)) + 0.5f) - 0.5f;
                                                            float _27772 = (abs(_27746) - _27737) * p_Meta2.z;
                                                            float _27778 = _42869 - (min(frac(_27746 - _27737), frac(_27746 + _27737)) * p_Meta2.z);
                                                            float _27784 = _42869 + (min(frac(_27737 - _27746), frac((_42870 * (-0.0002442598924972116947174072265625f)) - _27746)) * p_Meta2.z);
                                                            float _27789 = (_42870 * 0.000488519784994423389434814453125f) * p_Meta2.z;
                                                            bool _27136 = _27772 < 0.0f;
                                                            float _27137 = _27136 ? 1.0f : (-1.0f);
                                                            float _27851 = max(_27366, 9.9999999747524270787835121154785e-07f);
                                                            float _27856 = _27778 - (floor(_27778 / _27851) * _27366);
                                                            float _27871 = (_27856 < _27347) ? 0.0f : ((_27856 < _27352) ? 1.0f : ((_27856 < _27360) ? 2.0f : 3.0f));
                                                            bool _27959 = _27871 < 1.5f;
                                                            float _27960 = _27959 ? 1.0f : (-1.0f);
                                                            bool _27962 = (((_27871 < 0.5f) || ((_27871 > 1.5f) && (_27871 < 2.5f))) ? 0.0f : 1.0f) < 0.5f;
                                                            float2 _27964 = float2(_27960, 0.0f);
                                                            float2 _27966 = float2(0.0f, _27960);
                                                            bool2 _27967 = _27962.xx;
                                                            float2 _27968 = float2(_27967.x ? _27964.x : _27966.x, _27967.y ? _27964.y : _27966.y);
                                                            float _27972 = _27968.x;
                                                            float _27974 = _27968.y;
                                                            bool2 _27992 = _27959.xx;
                                                            float2 _27993 = float2(_27992.x ? _27669.x : _27671.x, _27992.y ? _27669.y : _27671.y);
                                                            float2 _28001 = float2(_27992.x ? _27677.x : _27679.x, _27992.y ? _27677.y : _27679.y);
                                                            float2 _28009 = float2(_27992.x ? _27685.x : _27687.x, _27992.y ? _27685.y : _27687.y);
                                                            float _28016 = _27962 ? _27993.x : _27993.y;
                                                            float _28023 = _27962 ? _28001.x : _28001.y;
                                                            float2 _27887 = float2(_27111 * (_27972 + _27974), _27113 * (_27974 - _27972)) - (_27968 * _28016);
                                                            float _27890 = _27856 - (_27962 ? _28009.x : _28009.y);
                                                            float2 _42898;
                                                            float2 _42899;
                                                            float _42900;
                                                            float _42901;
                                                            if (_27890 < _28023)
                                                            {
                                                                float _27899 = _28023 - _27890;
                                                                _42901 = (_27137 > 0.0f) ? _27899 : _27890;
                                                                _42900 = 2.0f * _27871;
                                                                _42899 = _27968;
                                                                _42898 = _27887 - (_27968 * _27899);
                                                            }
                                                            else
                                                            {
                                                                float2 _27917 = (_27968 + float2(-_27974, _27972)) * 0.707106769084930419921875f;
                                                                float _27922 = _27890 - _28023;
                                                                _42901 = (_27137 > 0.0f) ? max((_28016 * 1.41421353816986083984375f) - _27922, 0.0f) : _27922;
                                                                _42900 = (2.0f * _27871) + 1.0f;
                                                                _42899 = _27917;
                                                                _42898 = _27887 + (_27917 * _27922);
                                                            }
                                                            float _27152 = -_27137;
                                                            float _28069 = _27784 - (floor(_27784 / _27851) * _27366);
                                                            float _28084 = (_28069 < _27347) ? 0.0f : ((_28069 < _27352) ? 1.0f : ((_28069 < _27360) ? 2.0f : 3.0f));
                                                            bool _28172 = _28084 < 1.5f;
                                                            float _28173 = _28172 ? 1.0f : (-1.0f);
                                                            bool _28175 = (((_28084 < 0.5f) || ((_28084 > 1.5f) && (_28084 < 2.5f))) ? 0.0f : 1.0f) < 0.5f;
                                                            float2 _28177 = float2(_28173, 0.0f);
                                                            float2 _28179 = float2(0.0f, _28173);
                                                            bool2 _28180 = _28175.xx;
                                                            float2 _28181 = float2(_28180.x ? _28177.x : _28179.x, _28180.y ? _28177.y : _28179.y);
                                                            float _28185 = _28181.x;
                                                            float _28187 = _28181.y;
                                                            bool2 _28205 = _28172.xx;
                                                            float2 _28206 = float2(_28205.x ? _27669.x : _27671.x, _28205.y ? _27669.y : _27671.y);
                                                            float2 _28214 = float2(_28205.x ? _27677.x : _27679.x, _28205.y ? _27677.y : _27679.y);
                                                            float2 _28222 = float2(_28205.x ? _27685.x : _27687.x, _28205.y ? _27685.y : _27687.y);
                                                            float _28229 = _28175 ? _28206.x : _28206.y;
                                                            float _28236 = _28175 ? _28214.x : _28214.y;
                                                            float2 _28100 = float2(_27111 * (_28185 + _28187), _27113 * (_28187 - _28185)) - (_28181 * _28229);
                                                            float _28103 = _28069 - (_28175 ? _28222.x : _28222.y);
                                                            float2 _42906;
                                                            float2 _42907;
                                                            float _42908;
                                                            float _42909;
                                                            if (_28103 < _28236)
                                                            {
                                                                float _28112 = _28236 - _28103;
                                                                _42909 = (_27152 > 0.0f) ? _28112 : _28103;
                                                                _42908 = 2.0f * _28084;
                                                                _42907 = _28181;
                                                                _42906 = _28100 - (_28181 * _28112);
                                                            }
                                                            else
                                                            {
                                                                float2 _28130 = (_28181 + float2(-_28187, _28185)) * 0.707106769084930419921875f;
                                                                float _28135 = _28103 - _28236;
                                                                _42909 = (_27152 > 0.0f) ? max((_28229 * 1.41421353816986083984375f) - _28135, 0.0f) : _28135;
                                                                _42908 = (2.0f * _28084) + 1.0f;
                                                                _42907 = _28130;
                                                                _42906 = _28100 + (_28130 * _28135);
                                                            }
                                                            float2 _27167 = _42899 * _27137;
                                                            float2 _28365 = _12969 - _42898;
                                                            float _28376 = length(_28365 - (_27167 * clamp(dot(_28365, _27167), 0.0f, max(min(_42901, _27789), 0.0f))));
                                                            float2 _28300 = _42898 + (_27167 * _42901);
                                                            float _28303 = _27789 - _42901;
                                                            float _28380 = _27167.x;
                                                            float _28383 = _27167.y;
                                                            float2 _28394 = float2(_28380 - (_27137 * _28383), (_27137 * _28380) + _28383) * 0.707106769084930419921875f;
                                                            float _28309 = _42900 + _27137;
                                                            float _28410 = _28309 - (floor(_28309 * 0.125f) * 8.0f);
                                                            float _28413 = floor(_28410 * 0.5f);
                                                            bool2 _28450 = (_28413 < 1.5f).xx;
                                                            float2 _28451 = float2(_28450.x ? _27669.x : _27671.x, _28450.y ? _27669.y : _27671.y);
                                                            bool _28459 = (_28413 < 0.5f) || ((_28413 > 1.5f) && (_28413 < 2.5f));
                                                            float2 _28475 = float2(_28450.x ? _27677.x : _27679.x, _28450.y ? _27677.y : _27679.y);
                                                            float _28440 = ((_28410 - (2.0f * _28413)) >= 0.5f) ? max((_28459 ? _28451.x : _28451.y) * 1.41421353816986083984375f, 0.0f) : max(_28459 ? _28475.x : _28475.y, 0.0f);
                                                            float2 _28494 = _12969 - _28300;
                                                            float _28325 = (_28303 > 0.0f) ? min(_28376, length(_28494 - (_28394 * clamp(dot(_28494, _28394), 0.0f, max(min(_28440, _28303), 0.0f))))) : _28376;
                                                            float _28333 = _28303 - _28440;
                                                            float _28509 = _28394.x;
                                                            float _28512 = _28394.y;
                                                            float2 _28523 = float2(_28509 - (_27137 * _28512), (_27137 * _28509) + _28512) * 0.707106769084930419921875f;
                                                            float _28340 = _42900 + (2.0f * _27137);
                                                            float _28539 = _28340 - (floor(_28340 * 0.125f) * 8.0f);
                                                            float _28542 = floor(_28539 * 0.5f);
                                                            bool2 _28579 = (_28542 < 1.5f).xx;
                                                            float2 _28580 = float2(_28579.x ? _27669.x : _27671.x, _28579.y ? _27669.y : _27671.y);
                                                            bool _28588 = (_28542 < 0.5f) || ((_28542 > 1.5f) && (_28542 < 2.5f));
                                                            float2 _28604 = float2(_28579.x ? _27677.x : _27679.x, _28579.y ? _27677.y : _27679.y);
                                                            float2 _28623 = _12969 - (_28300 + (_28394 * _28440));
                                                            float _28359 = ((_28333 > 0.0f) ? min(_28325, length(_28623 - (_28523 * clamp(dot(_28623, _28523), 0.0f, max(min(((_28539 - (2.0f * _28542)) >= 0.5f) ? max((_28588 ? _28580.x : _28580.y) * 1.41421353816986083984375f, 0.0f) : max(_28588 ? _28604.x : _28604.y, 0.0f), _28333), 0.0f))))) : _28325) - _27097;
                                                            float2 _27182 = _42907 * _27152;
                                                            float2 _28748 = _12969 - _42906;
                                                            float _28759 = length(_28748 - (_27182 * clamp(dot(_28748, _27182), 0.0f, max(min(_42909, _27789), 0.0f))));
                                                            float2 _28683 = _42906 + (_27182 * _42909);
                                                            float _28686 = _27789 - _42909;
                                                            float _28763 = _27182.x;
                                                            float _28766 = _27182.y;
                                                            float2 _28777 = float2(_28763 - (_27152 * _28766), (_27152 * _28763) + _28766) * 0.707106769084930419921875f;
                                                            float _28692 = _42908 - _27137;
                                                            float _28793 = _28692 - (floor(_28692 * 0.125f) * 8.0f);
                                                            float _28796 = floor(_28793 * 0.5f);
                                                            bool2 _28833 = (_28796 < 1.5f).xx;
                                                            float2 _28834 = float2(_28833.x ? _27669.x : _27671.x, _28833.y ? _27669.y : _27671.y);
                                                            bool _28842 = (_28796 < 0.5f) || ((_28796 > 1.5f) && (_28796 < 2.5f));
                                                            float2 _28858 = float2(_28833.x ? _27677.x : _27679.x, _28833.y ? _27677.y : _27679.y);
                                                            float _28823 = ((_28793 - (2.0f * _28796)) >= 0.5f) ? max((_28842 ? _28834.x : _28834.y) * 1.41421353816986083984375f, 0.0f) : max(_28842 ? _28858.x : _28858.y, 0.0f);
                                                            float2 _28877 = _12969 - _28683;
                                                            float _28708 = (_28686 > 0.0f) ? min(_28759, length(_28877 - (_28777 * clamp(dot(_28877, _28777), 0.0f, max(min(_28823, _28686), 0.0f))))) : _28759;
                                                            float _28716 = _28686 - _28823;
                                                            float _28892 = _28777.x;
                                                            float _28895 = _28777.y;
                                                            float2 _28906 = float2(_28892 - (_27152 * _28895), (_27152 * _28892) + _28895) * 0.707106769084930419921875f;
                                                            float _28723 = _42908 + (_27137 * (-2.0f));
                                                            float _28922 = _28723 - (floor(_28723 * 0.125f) * 8.0f);
                                                            float _28925 = floor(_28922 * 0.5f);
                                                            bool2 _28962 = (_28925 < 1.5f).xx;
                                                            float2 _28963 = float2(_28962.x ? _27669.x : _27671.x, _28962.y ? _27669.y : _27671.y);
                                                            bool _28971 = (_28925 < 0.5f) || ((_28925 > 1.5f) && (_28925 < 2.5f));
                                                            float2 _28987 = float2(_28962.x ? _27677.x : _27679.x, _28962.y ? _27677.y : _27679.y);
                                                            float2 _29006 = _12969 - (_28683 + (_28777 * _28823));
                                                            float _28742 = ((_28716 > 0.0f) ? min(_28708, length(_29006 - (_28906 * clamp(dot(_29006, _28906), 0.0f, max(min(((_28922 - (2.0f * _28925)) >= 0.5f) ? max((_28971 ? _28963.x : _28963.y) * 1.41421353816986083984375f, 0.0f) : max(_28971 ? _28987.x : _28987.y, 0.0f), _28716), 0.0f))))) : _28708) - _27097;
                                                            _42913 = _27772;
                                                            _42912 = _27136 ? max(_28359, _28742) : min(_28359, _28742);
                                                            break;
                                                        }
                                                        float _29272;
                                                        float2 _29427;
                                                        float2 _29429;
                                                        float2 _29435;
                                                        float2 _29437;
                                                        float2 _29443;
                                                        float2 _29445;
                                                        float _29026 = min(p_Meta1.z, p_Meta1.w);
                                                        float _29029 = max(min(1.5f * p_Meta1.x, _29026 * 0.5f), 0.0f);
                                                        float _29031 = _29029 * 0.4142135679721832275390625f;
                                                        float4 _29044 = clamp(_50045, (0.58578646183013916015625f * _29029).xxxx, (_29026 - _29031).xxxx);
                                                        float _29054 = 2.0f * p_Meta1.z;
                                                        float _29057 = 2.0f * p_Meta1.w;
                                                        float _29066 = _29044.x;
                                                        float _29068 = _29044.y;
                                                        float _29070 = _29044.w;
                                                        float _29072 = _29044.z;
                                                        float4 _29073 = float4(_29066, _29068, _29070, _29072);
                                                        float _29089 = _29029 * 0.828427135944366455078125f;
                                                        float4 _29090 = _29089.xxxx;
                                                        float4 _29091 = ((float4(_29054, _29057, _29054, _29057) - _29073) - float4(_29072, _29066, _29068, _29070)) - _29090;
                                                        float4 _29103 = ((_29091 + (_29029 * 1.57079637050628662109375f).xxxx) + (_29073 * 1.41421353816986083984375f)) - _29090;
                                                        float _29105 = _29103.x;
                                                        float _29110 = _29105 + _29103.y;
                                                        float _29118 = _29110 + _29103.z;
                                                        float4 _29119 = float4(0.0f, _29105, _29110, _29118);
                                                        float _29124 = _29118 + _29103.w;
                                                        float _42827;
                                                        do
                                                        {
                                                            float _29175 = (_29066 - _29068) * 0.5f;
                                                            float _29181 = (_29070 - _29068) * 0.5f;
                                                            float _42824;
                                                            if ((p_TexCoord.x >= ((_29072 - _29066) * 0.5f)) && (p_TexCoord.y <= _29175))
                                                            {
                                                                _42824 = 0.0f;
                                                            }
                                                            else
                                                            {
                                                                float _42825;
                                                                if ((p_TexCoord.x >= _29181) && (p_TexCoord.y >= _29175))
                                                                {
                                                                    _42825 = 1.0f;
                                                                }
                                                                else
                                                                {
                                                                    _42825 = ((p_TexCoord.x <= _29181) && (p_TexCoord.y >= ((_29072 - _29070) * 0.5f))) ? 2.0f : 3.0f;
                                                                }
                                                                _42824 = _42825;
                                                            }
                                                            bool _29397 = _42824 < 1.5f;
                                                            float _29398 = _29397 ? 1.0f : (-1.0f);
                                                            bool _29400 = (((_42824 < 0.5f) || ((_42824 > 1.5f) && (_42824 < 2.5f))) ? 0.0f : 1.0f) < 0.5f;
                                                            float2 _29402 = float2(_29398, 0.0f);
                                                            float2 _29404 = float2(0.0f, _29398);
                                                            bool2 _29405 = _29400.xx;
                                                            float2 _29406 = float2(_29405.x ? _29402.x : _29404.x, _29405.y ? _29402.y : _29404.y);
                                                            float _29410 = _29406.x;
                                                            float _29412 = _29406.y;
                                                            float2 _29423 = float2(p_Meta1.z * (_29410 + _29412), p_Meta1.w * (_29412 - _29410));
                                                            _29427 = _29073.xy;
                                                            _29429 = _29073.zw;
                                                            bool2 _29430 = _29397.xx;
                                                            float2 _29431 = float2(_29430.x ? _29427.x : _29429.x, _29430.y ? _29427.y : _29429.y);
                                                            _29435 = _29091.xy;
                                                            _29437 = _29091.zw;
                                                            float2 _29439 = float2(_29430.x ? _29435.x : _29437.x, _29430.y ? _29435.y : _29437.y);
                                                            _29443 = _29119.xy;
                                                            _29445 = _29119.zw;
                                                            float2 _29447 = float2(_29430.x ? _29443.x : _29445.x, _29430.y ? _29443.y : _29445.y);
                                                            float _29454 = _29400 ? _29431.x : _29431.y;
                                                            float2 _29476 = float2(-_29412, _29410);
                                                            float2 _29238 = -_29476;
                                                            float2 _29242 = (_29406 - _29476) * 0.707106769084930419921875f;
                                                            float2 _29246 = (_29406 + _29476) * 0.707106769084930419921875f;
                                                            float _29253 = _29454 + _29031;
                                                            float2 _29259 = (_29423 - (_29406 * _29253)) + (_29476 * _29029);
                                                            _29272 = _29029 * 0.785398185253143310546875f;
                                                            float _29275 = (_29400 ? _29447.x : _29447.y) + (_29400 ? _29439.x : _29439.y);
                                                            float _29278 = _29275 + _29272;
                                                            float _29284 = (_29278 + (_29454 * 1.41421353816986083984375f)) - _29089;
                                                            float2 _29287 = _12969 - _29259;
                                                            float _29289 = dot(_29287, _29406);
                                                            if (_29289 <= 0.0f)
                                                            {
                                                                _42827 = _29275 + _29289;
                                                                break;
                                                            }
                                                            if (dot(_29287, _29246) <= 0.0f)
                                                            {
                                                                _42827 = _29275 + (_29029 * atan2((_29238.x * _29287.y) - (_29238.y * _29287.x), dot(_29238, _29287)));
                                                                break;
                                                            }
                                                            float2 _29327 = _12969 - ((_29423 + (_29476 * _29253)) - (_29406 * _29029));
                                                            if (dot(_29327, _29246) <= 0.0f)
                                                            {
                                                                _42827 = _29278 + dot(_12969 - (_29259 + (_29242 * _29029)), _29246);
                                                                break;
                                                            }
                                                            float _29349 = dot(_29327, _29476);
                                                            if (_29349 <= 0.0f)
                                                            {
                                                                _42827 = _29284 + (_29029 * atan2((_29242.x * _29327.y) - (_29242.y * _29327.x), dot(_29242, _29327)));
                                                                break;
                                                            }
                                                            _42827 = (_29284 + _29272) + _29349;
                                                            break;
                                                        } while(false);
                                                        float _29556 = floor(p_Meta2.w * 0.00048828125f);
                                                        float _29561 = p_Meta2.w - (_29556 * 2048.0f);
                                                        float _42828;
                                                        float _42830;
                                                        if (_29561 >= 2048.0f)
                                                        {
                                                            _42830 = _29561 - 2048.0f;
                                                            _42828 = _29556 + 1.0f;
                                                        }
                                                        else
                                                        {
                                                            float _42829;
                                                            float _42831;
                                                            if (_29561 < 0.0f)
                                                            {
                                                                _42831 = _29561 + 2048.0f;
                                                                _42829 = _29556 - 1.0f;
                                                            }
                                                            else
                                                            {
                                                                _42831 = _29561;
                                                                _42829 = _29556;
                                                            }
                                                            _42830 = _42831;
                                                            _42828 = _42829;
                                                        }
                                                        float _29495 = _42828 * 0.0002442598924972116947174072265625f;
                                                        float _29504 = frac(((_42827 / p_Meta2.z) - (_42830 * 0.000488519784994423389434814453125f)) + 0.5f) - 0.5f;
                                                        float _29530 = (abs(_29504) - _29495) * p_Meta2.z;
                                                        float _29536 = _42827 - (min(frac(_29504 - _29495), frac(_29504 + _29495)) * p_Meta2.z);
                                                        float _29542 = _42827 + (min(frac(_29495 - _29504), frac((_42828 * (-0.0002442598924972116947174072265625f)) - _29504)) * p_Meta2.z);
                                                        float _29657 = max(_29124, 9.9999999747524270787835121154785e-07f);
                                                        float _29662 = _29536 - (floor(_29536 / _29657) * _29124);
                                                        float _29677 = (_29662 < _29105) ? 0.0f : ((_29662 < _29110) ? 1.0f : ((_29662 < _29118) ? 2.0f : 3.0f));
                                                        bool _29960 = _29677 < 1.5f;
                                                        float _29961 = _29960 ? 1.0f : (-1.0f);
                                                        bool _29963 = (((_29677 < 0.5f) || ((_29677 > 1.5f) && (_29677 < 2.5f))) ? 0.0f : 1.0f) < 0.5f;
                                                        float2 _29965 = float2(_29961, 0.0f);
                                                        float2 _29967 = float2(0.0f, _29961);
                                                        bool2 _29968 = _29963.xx;
                                                        float2 _29969 = float2(_29968.x ? _29965.x : _29967.x, _29968.y ? _29965.y : _29967.y);
                                                        float _29973 = _29969.x;
                                                        float _29975 = _29969.y;
                                                        float2 _29986 = float2(p_Meta1.z * (_29973 + _29975), p_Meta1.w * (_29975 - _29973));
                                                        bool2 _29993 = _29960.xx;
                                                        float2 _29994 = float2(_29993.x ? _29427.x : _29429.x, _29993.y ? _29427.y : _29429.y);
                                                        float2 _30002 = float2(_29993.x ? _29435.x : _29437.x, _29993.y ? _29435.y : _29437.y);
                                                        float2 _30010 = float2(_29993.x ? _29443.x : _29445.x, _29993.y ? _29443.y : _29445.y);
                                                        float _30017 = _29963 ? _29994.x : _29994.y;
                                                        float _30024 = _29963 ? _30002.x : _30002.y;
                                                        float2 _30039 = float2(-_29975, _29973);
                                                        float2 _29692 = -_30039;
                                                        float2 _29696 = (_29969 - _30039) * 0.707106769084930419921875f;
                                                        float2 _29700 = (_29969 + _30039) * 0.707106769084930419921875f;
                                                        float _29707 = _30017 + _29031;
                                                        float2 _29713 = (_29986 - (_29969 * _29707)) + (_30039 * _29029);
                                                        float2 _29724 = (_29986 + (_30039 * _29707)) - (_29969 * _29029);
                                                        float _29731 = (_30017 * 1.41421353816986083984375f) - _29089;
                                                        float _29777 = _29662 - (_29963 ? _30010.x : _30010.y);
                                                        float _29781 = _30024 + _29272;
                                                        bool _29782 = _29777 >= _29781;
                                                        float _29792 = _29781 + _29731;
                                                        float2 _42856;
                                                        float2 _42857;
                                                        float _42858;
                                                        if ((_29777 < _30024) || (_29782 && (_29777 < _29792)))
                                                        {
                                                            bool2 _29800 = _29782.xx;
                                                            float2 _29801 = float2(_29800.x ? _29700.x : _29969.x, _29800.y ? _29700.y : _29969.y);
                                                            float2 _29812 = _29713 + (_29696 * _29029);
                                                            float2 _29821 = (_29713 + (_29692 * _29029)) - (_29969 * _30024);
                                                            _42858 = p_Meta1.x * (-1.0f);
                                                            _42857 = _29801;
                                                            _42856 = float2(_29800.x ? _29812.x : _29821.x, _29800.y ? _29812.y : _29821.y) + (_29801 * (_29782 ? ((_29777 - _30024) - _29272) : _29777));
                                                        }
                                                        else
                                                        {
                                                            bool _29864 = _29777 >= _29792;
                                                            float _29876 = ((_29777 - _30024) - (_29864 ? (_29272 + _29731) : 0.0f)) / max(_29029, 9.9999999747524270787835121154785e-07f);
                                                            bool2 _29880 = _29864.xx;
                                                            float2 _29881 = float2(_29880.x ? _29696.x : _29692.x, _29880.y ? _29696.y : _29692.y);
                                                            float _30063 = sin(_29876);
                                                            float _30065 = cos(_29876);
                                                            float _30067 = _29881.x;
                                                            float _30071 = _29881.y;
                                                            _42858 = 0.0f;
                                                            _42857 = float2(-((_30067 * _30063) + (_30071 * _30065)), (_30067 * _30065) - (_30071 * _30063));
                                                            _42856 = float2(_29880.x ? _29724.x : _29713.x, _29880.y ? _29724.y : _29713.y);
                                                        }
                                                        float _30500 = _29542 - (floor(_29542 / _29657) * _29124);
                                                        float _30515 = (_30500 < _29105) ? 0.0f : ((_30500 < _29110) ? 1.0f : ((_30500 < _29118) ? 2.0f : 3.0f));
                                                        bool _30798 = _30515 < 1.5f;
                                                        float _30799 = _30798 ? 1.0f : (-1.0f);
                                                        bool _30801 = (((_30515 < 0.5f) || ((_30515 > 1.5f) && (_30515 < 2.5f))) ? 0.0f : 1.0f) < 0.5f;
                                                        float2 _30803 = float2(_30799, 0.0f);
                                                        float2 _30805 = float2(0.0f, _30799);
                                                        bool2 _30806 = _30801.xx;
                                                        float2 _30807 = float2(_30806.x ? _30803.x : _30805.x, _30806.y ? _30803.y : _30805.y);
                                                        float _30811 = _30807.x;
                                                        float _30813 = _30807.y;
                                                        float2 _30824 = float2(p_Meta1.z * (_30811 + _30813), p_Meta1.w * (_30813 - _30811));
                                                        bool2 _30831 = _30798.xx;
                                                        float2 _30832 = float2(_30831.x ? _29427.x : _29429.x, _30831.y ? _29427.y : _29429.y);
                                                        float2 _30840 = float2(_30831.x ? _29435.x : _29437.x, _30831.y ? _29435.y : _29437.y);
                                                        float2 _30848 = float2(_30831.x ? _29443.x : _29445.x, _30831.y ? _29443.y : _29445.y);
                                                        float _30855 = _30801 ? _30832.x : _30832.y;
                                                        float _30862 = _30801 ? _30840.x : _30840.y;
                                                        float2 _30877 = float2(-_30813, _30811);
                                                        float2 _30530 = -_30877;
                                                        float2 _30534 = (_30807 - _30877) * 0.707106769084930419921875f;
                                                        float2 _30538 = (_30807 + _30877) * 0.707106769084930419921875f;
                                                        float _30545 = _30855 + _29031;
                                                        float2 _30551 = (_30824 - (_30807 * _30545)) + (_30877 * _29029);
                                                        float2 _30562 = (_30824 + (_30877 * _30545)) - (_30807 * _29029);
                                                        float _30569 = (_30855 * 1.41421353816986083984375f) - _29089;
                                                        float _30615 = _30500 - (_30801 ? _30848.x : _30848.y);
                                                        float _30619 = _30862 + _29272;
                                                        bool _30620 = _30615 >= _30619;
                                                        float _30630 = _30619 + _30569;
                                                        float2 _42863;
                                                        float2 _42864;
                                                        float _42865;
                                                        if ((_30615 < _30862) || (_30620 && (_30615 < _30630)))
                                                        {
                                                            bool2 _30638 = _30620.xx;
                                                            float2 _30639 = float2(_30638.x ? _30538.x : _30807.x, _30638.y ? _30538.y : _30807.y);
                                                            float2 _30650 = _30551 + (_30534 * _29029);
                                                            float2 _30659 = (_30551 + (_30530 * _29029)) - (_30807 * _30862);
                                                            _42865 = p_Meta1.x * (-1.0f);
                                                            _42864 = _30639;
                                                            _42863 = float2(_30638.x ? _30650.x : _30659.x, _30638.y ? _30650.y : _30659.y) + (_30639 * (_30620 ? ((_30615 - _30862) - _29272) : _30615));
                                                        }
                                                        else
                                                        {
                                                            bool _30702 = _30615 >= _30630;
                                                            float _30714 = ((_30615 - _30862) - (_30702 ? (_29272 + _30569) : 0.0f)) / max(_29029, 9.9999999747524270787835121154785e-07f);
                                                            bool2 _30718 = _30702.xx;
                                                            float2 _30719 = float2(_30718.x ? _30534.x : _30530.x, _30718.y ? _30534.y : _30530.y);
                                                            float _30901 = sin(_30714);
                                                            float _30903 = cos(_30714);
                                                            float _30905 = _30719.x;
                                                            float _30909 = _30719.y;
                                                            _42865 = 0.0f;
                                                            _42864 = float2(-((_30905 * _30901) + (_30909 * _30903)), (_30905 * _30903) - (_30909 * _30901));
                                                            _42863 = float2(_30718.x ? _30562.x : _30551.x, _30718.y ? _30562.y : _30551.y);
                                                        }
                                                        float2 _27270 = float2(_42857.y, -_42857.x);
                                                        float2 _27276 = float2(_42864.y, -_42864.x);
                                                        float2 _31304 = _12969 - (_42856 + (_27270 * (_42858 - _13715)));
                                                        float2 _31321 = _12969 - (_42863 + (_27276 * (_42865 - _13715)));
                                                        float _31291 = min(length(_31304 - (_27270 * clamp(dot(_31304, _27270), 0.0f, 1000000.0f))), length(_31321 - (_27276 * clamp(dot(_31321, _27276), 0.0f, 1000000.0f))));
                                                        _42913 = _29530;
                                                        _42912 = (_29530 >= 0.0f) ? _31291 : (-_31291);
                                                        break;
                                                    } while(false);
                                                    _46022 = _13717;
                                                    _44788 = _42913;
                                                    _44686 = _42912;
                                                }
                                                else
                                                {
                                                    _46022 = false;
                                                    _44788 = 1.0f;
                                                    _44686 = 1.0f;
                                                }
                                                _46021 = _46022;
                                                _44787 = _44788;
                                                _44685 = _44686;
                                                _43617 = _42711;
                                                _43518 = max(_42815, max(max(_26922.x, _26922.y), max(_26922.z, _26922.w)) * 0.707106769084930419921875f);
                                                _43425 = p_TexCoord.z;
                                            }
                                            else
                                            {
                                                bool _13734 = _42711 >= 0.5f;
                                                float _42797;
                                                float4 _49852;
                                                if (_13734)
                                                {
                                                    float _31340 = floor(p_Meta2.y * 0.00048828125f);
                                                    float _31345 = p_Meta2.y - (_31340 * 2048.0f);
                                                    float _42713;
                                                    float _42715;
                                                    if (_31345 >= 2048.0f)
                                                    {
                                                        _42715 = _31345 - 2048.0f;
                                                        _42713 = _31340 + 1.0f;
                                                    }
                                                    else
                                                    {
                                                        float _42714;
                                                        float _42716;
                                                        if (_31345 < 0.0f)
                                                        {
                                                            _42716 = _31345 + 2048.0f;
                                                            _42714 = _31340 - 1.0f;
                                                        }
                                                        else
                                                        {
                                                            _42716 = _31345;
                                                            _42714 = _31340;
                                                        }
                                                        _42715 = _42716;
                                                        _42713 = _42714;
                                                    }
                                                    float _13741 = _42715 - 1024.0f;
                                                    float _13743 = _13741 * 0.003070960752665996551513671875f;
                                                    float _13745 = _42713 - 1024.0f;
                                                    float _13747 = _13745 * 0.003070960752665996551513671875f;
                                                    float _31375 = floor(p_Meta2.x * 0.015625f);
                                                    float _31380 = p_Meta2.x - (_31375 * 64.0f);
                                                    float _42717;
                                                    float _42719;
                                                    if (_31380 >= 64.0f)
                                                    {
                                                        _42719 = _31380 - 64.0f;
                                                        _42717 = _31375 + 1.0f;
                                                    }
                                                    else
                                                    {
                                                        float _42718;
                                                        float _42720;
                                                        if (_31380 < 0.0f)
                                                        {
                                                            _42720 = _31380 + 64.0f;
                                                            _42718 = _31375 - 1.0f;
                                                        }
                                                        else
                                                        {
                                                            _42720 = _31380;
                                                            _42718 = _31375;
                                                        }
                                                        _42719 = _42720;
                                                        _42717 = _42718;
                                                    }
                                                    float _31410 = floor(_42717 * 0.0078125f);
                                                    float _31415 = _42717 - (_31410 * 128.0f);
                                                    float _42721;
                                                    float _42723;
                                                    if (_31415 >= 128.0f)
                                                    {
                                                        _42723 = _31415 - 128.0f;
                                                        _42721 = _31410 + 1.0f;
                                                    }
                                                    else
                                                    {
                                                        float _42722;
                                                        float _42724;
                                                        if (_31415 < 0.0f)
                                                        {
                                                            _42724 = _31415 + 128.0f;
                                                            _42722 = _31410 - 1.0f;
                                                        }
                                                        else
                                                        {
                                                            _42724 = _31415;
                                                            _42722 = _31410;
                                                        }
                                                        _42723 = _42724;
                                                        _42721 = _42722;
                                                    }
                                                    float _31445 = floor(_42721 * 0.0078125f);
                                                    float _31450 = _42721 - (_31445 * 128.0f);
                                                    float _42725;
                                                    float _42727;
                                                    if (_31450 >= 128.0f)
                                                    {
                                                        _42727 = _31450 - 128.0f;
                                                        _42725 = _31445 + 1.0f;
                                                    }
                                                    else
                                                    {
                                                        float _42726;
                                                        float _42728;
                                                        if (_31450 < 0.0f)
                                                        {
                                                            _42728 = _31450 + 128.0f;
                                                            _42726 = _31445 - 1.0f;
                                                        }
                                                        else
                                                        {
                                                            _42728 = _31450;
                                                            _42726 = _31445;
                                                        }
                                                        _42727 = _42728;
                                                        _42725 = _42726;
                                                    }
                                                    bool _13761 = _42725 >= 0.5f;
                                                    float _13765 = _13761 ? p_Meta2.z : p_Meta1.z;
                                                    float _13770 = _13761 ? 0.0f : p_Meta2.z;
                                                    float2 _13781 = float2(p_Meta1.z, _13765) * (1.0f.xx + (float2(_42723, _42727) * 0.0078740157186985015869140625f.xx));
                                                    float _13783 = _13745 * 0.0015354803763329982757568359375f;
                                                    float _13791 = _13741 * 0.0015354803763329982757568359375f;
                                                    float _13799 = sign(_13747);
                                                    float _13801 = sign(_13743);
                                                    float _13804 = -_13799;
                                                    float _13815 = -_13801;
                                                    float _13833 = _14846 * _12863;
                                                    float _42771;
                                                    do
                                                    {
                                                        float _31575 = abs(_13747);
                                                        float _31577 = abs(_13743);
                                                        bool _31583 = _31575 > 9.9999997473787516355514526367188e-05f;
                                                        float _31585 = _13781.x;
                                                        float _31590 = _31583 ? (_31585 * tan(_31575 * 0.5f)) : 0.0f;
                                                        bool _31592 = _31577 > 9.9999997473787516355514526367188e-05f;
                                                        float _31594 = _13781.y;
                                                        float _31599 = _31592 ? (_31594 * tan(_31577 * 0.5f)) : 0.0f;
                                                        float _31602 = _13770 + _31590;
                                                        float _31607 = (_13770 + p_Meta1.w) - _31599;
                                                        float2 _31613 = float2(_31590, _13799 * _31585);
                                                        float _31616 = p_Meta1.w - _31599;
                                                        float2 _31621 = float2(_31616, _13801 * _31594);
                                                        float _31628 = max(_31607 - _31602, 9.9999999747524270787835121154785e-07f);
                                                        float _31629 = (_13765 - p_Meta1.z) / _31628;
                                                        float _31638 = (abs(_31629) < 1.0f) ? sqrt(1.0f - (_31629 * _31629)) : 1.0f;
                                                        float _42745;
                                                        float _42766;
                                                        if (_31592 && (p_TexCoord.x > _31616))
                                                        {
                                                            float2 _31657 = _12969 - _31621;
                                                            _42766 = length(_31657) - _31594;
                                                            _42745 = _31607 + (clamp(atan2(_31657.x, _13815 * _31657.y), 0.0f, _31577) * _31594);
                                                        }
                                                        else
                                                        {
                                                            float _42746;
                                                            float _42767;
                                                            if (_31583 && (p_TexCoord.x < _31590))
                                                            {
                                                                float2 _31689 = _12969 - _31613;
                                                                _42767 = length(_31689) - _31585;
                                                                _42746 = _31602 - (clamp(atan2(-_31689.x, _13804 * _31689.y), 0.0f, _31575) * _31585);
                                                            }
                                                            else
                                                            {
                                                                _42767 = p_TexCoord.y;
                                                                _42746 = _13770 + p_TexCoord.x;
                                                            }
                                                            _42766 = _42767;
                                                            _42745 = _42746;
                                                        }
                                                        float _31984 = floor(p_TexCoord.z * 0.00048828125f);
                                                        float _31989 = p_TexCoord.z - (_31984 * 2048.0f);
                                                        bool _31992 = _31989 >= 2048.0f;
                                                        float _42747;
                                                        float _42749;
                                                        if (_31992)
                                                        {
                                                            _42749 = _31989 - 2048.0f;
                                                            _42747 = _31984 + 1.0f;
                                                        }
                                                        else
                                                        {
                                                            float _42748;
                                                            float _42750;
                                                            if (_31989 < 0.0f)
                                                            {
                                                                _42750 = _31989 + 2048.0f;
                                                                _42748 = _31984 - 1.0f;
                                                            }
                                                            else
                                                            {
                                                                _42750 = _31989;
                                                                _42748 = _31984;
                                                            }
                                                            _42749 = _42750;
                                                            _42747 = _42748;
                                                        }
                                                        float _31923 = _42747 * 0.0002442598924972116947174072265625f;
                                                        float _31932 = frac(((_42745 / p_Meta2.w) - (_42749 * 0.000488519784994423389434814453125f)) + 0.5f) - 0.5f;
                                                        float _31958 = (abs(_31932) - _31923) * p_Meta2.w;
                                                        float _31964 = _42745 - (min(frac(_31932 - _31923), frac(_31932 + _31923)) * p_Meta2.w);
                                                        float _31970 = _42745 + (min(frac(_31923 - _31932), frac((_42747 * (-0.0002442598924972116947174072265625f)) - _31932)) * p_Meta2.w);
                                                        bool _31717 = _42711 >= 1.5f;
                                                        if (_31717 && (_31958 < 0.0f))
                                                        {
                                                            float _32026 = lerp(p_Meta1.z, _13765, clamp((_42745 - _31602) / _31628, 0.0f, 1.0f));
                                                            _42771 = abs(_42766) - (((_42745 > _31602) && (_42745 < _31607)) ? (_32026 / _31638) : _32026);
                                                            break;
                                                        }
                                                        float2 _42751;
                                                        float2 _42752;
                                                        do
                                                        {
                                                            if ((_31964 > _31607) && _31592)
                                                            {
                                                                float _32069 = _31964 - _31607;
                                                                float _32074 = _32069 - (_31577 * _31594);
                                                                if (_32074 > 0.0f)
                                                                {
                                                                    float2 _51244 = float2(cos(_13743), sin(_13743));
                                                                    _42752 = _51244;
                                                                    _42751 = (_31621 + (float2(sin(_31577), _13815 * cos(_31577)) * _31594)) + (_51244 * _32074);
                                                                    break;
                                                                }
                                                                float _32108 = _32069 / _31594;
                                                                float _32110 = cos(_32108);
                                                                float _32113 = sin(_32108);
                                                                _42752 = float2(_32110, _13801 * _32113);
                                                                _42751 = _31621 + (float2(_32113, _13815 * _32110) * _31594);
                                                                break;
                                                            }
                                                            if ((_31964 < _31602) && _31583)
                                                            {
                                                                float _32146 = (_31602 - (_31575 * _31585)) - _31964;
                                                                if (_32146 > 0.0f)
                                                                {
                                                                    float2 _32155 = float2(cos(_13747), -sin(_13747));
                                                                    _42752 = _32155;
                                                                    _42751 = (_31613 + (float2(-sin(_31575), _13804 * cos(_31575)) * _31585)) - (_32155 * _32146);
                                                                    break;
                                                                }
                                                                float _32181 = (_31602 - _31964) / _31585;
                                                                float _32183 = cos(_32181);
                                                                float _32187 = sin(_32181);
                                                                _42752 = float2(_32183, _13804 * _32187);
                                                                _42751 = _31613 + (float2(-_32187, _13804 * _32183) * _31585);
                                                                break;
                                                            }
                                                            _42752 = float2(1.0f, 0.0f);
                                                            _42751 = float2(_31964 - _13770, 0.0f);
                                                            break;
                                                        } while(false);
                                                        float2 _42753;
                                                        float2 _42754;
                                                        do
                                                        {
                                                            if ((_31970 > _31607) && _31592)
                                                            {
                                                                float _32241 = _31970 - _31607;
                                                                float _32246 = _32241 - (_31577 * _31594);
                                                                if (_32246 > 0.0f)
                                                                {
                                                                    float2 _51245 = float2(cos(_13743), sin(_13743));
                                                                    _42754 = _51245;
                                                                    _42753 = (_31621 + (float2(sin(_31577), _13815 * cos(_31577)) * _31594)) + (_51245 * _32246);
                                                                    break;
                                                                }
                                                                float _32280 = _32241 / _31594;
                                                                float _32282 = cos(_32280);
                                                                float _32285 = sin(_32280);
                                                                _42754 = float2(_32282, _13801 * _32285);
                                                                _42753 = _31621 + (float2(_32285, _13815 * _32282) * _31594);
                                                                break;
                                                            }
                                                            if ((_31970 < _31602) && _31583)
                                                            {
                                                                float _32318 = (_31602 - (_31575 * _31585)) - _31970;
                                                                if (_32318 > 0.0f)
                                                                {
                                                                    float2 _32327 = float2(cos(_13747), -sin(_13747));
                                                                    _42754 = _32327;
                                                                    _42753 = (_31613 + (float2(-sin(_31575), _13804 * cos(_31575)) * _31585)) - (_32327 * _32318);
                                                                    break;
                                                                }
                                                                float _32353 = (_31602 - _31970) / _31585;
                                                                float _32355 = cos(_32353);
                                                                float _32359 = sin(_32353);
                                                                _42754 = float2(_32355, _13804 * _32359);
                                                                _42753 = _31613 + (float2(-_32359, _13804 * _32355) * _31585);
                                                                break;
                                                            }
                                                            _42754 = float2(1.0f, 0.0f);
                                                            _42753 = float2(_31970 - _13770, 0.0f);
                                                            break;
                                                        } while(false);
                                                        if (_31717)
                                                        {
                                                            float _32397 = lerp(p_Meta1.z, _13765, clamp((_31964 - _31602) / _31628, 0.0f, 1.0f));
                                                            float _32424 = lerp(p_Meta1.z, _13765, clamp((_31970 - _31602) / _31628, 0.0f, 1.0f));
                                                            _42771 = min(length(_12969 - _42751) - (((_31964 > _31602) && (_31964 < _31607)) ? (_32397 / _31638) : _32397), length(_12969 - _42753) - (((_31970 > _31602) && (_31970 < _31607)) ? (_32424 / _31638) : _32424));
                                                            break;
                                                        }
                                                        float _32451 = lerp(p_Meta1.z, _13765, clamp((_31964 - _31602) / _31628, 0.0f, 1.0f));
                                                        float _31798 = (((_31964 > _31602) && (_31964 < _31607)) ? (_32451 / _31638) : _32451) + _13833;
                                                        float _32478 = lerp(p_Meta1.z, _13765, clamp((_31970 - _31602) / _31628, 0.0f, 1.0f));
                                                        float _31808 = (((_31970 > _31602) && (_31970 < _31607)) ? (_32478 / _31638) : _32478) + _13833;
                                                        float2 _32498 = float2(-_42752.y, _42752.x);
                                                        float2 _32506 = float2(-_42754.y, _42754.x);
                                                        float2 _32512 = _12969 - (_42751 - (_32498 * _31798));
                                                        float2 _32529 = _12969 - (_42753 - (_32506 * _31808));
                                                        float _31833 = min(length(_32512 - (_32498 * clamp(dot(_32512, _32498), 0.0f, max(2.0f * _31798, 0.0f)))), length(_32529 - (_32506 * clamp(dot(_32529, _32506), 0.0f, max(2.0f * _31808, 0.0f)))));
                                                        float _42763;
                                                        if (_31592 && (p_TexCoord.x > p_Meta1.w))
                                                        {
                                                            float _31853 = (_31577 * _31594) * 0.5f;
                                                            float _42759;
                                                            float _42761;
                                                            if (_31992)
                                                            {
                                                                _42761 = _31989 - 2048.0f;
                                                                _42759 = _31984 + 1.0f;
                                                            }
                                                            else
                                                            {
                                                                float _42760;
                                                                float _42762;
                                                                if (_31989 < 0.0f)
                                                                {
                                                                    _42762 = _31989 + 2048.0f;
                                                                    _42760 = _31984 - 1.0f;
                                                                }
                                                                else
                                                                {
                                                                    _42762 = _31989;
                                                                    _42760 = _31984;
                                                                }
                                                                _42761 = _42762;
                                                                _42759 = _42760;
                                                            }
                                                            _42763 = (length(_12969 - float2(p_Meta1.w, 0.0f)) - _13765) + (min(((abs(frac((((_31607 + _31853) / p_Meta2.w) - (_42761 * 0.000488519784994423389434814453125f)) + 0.5f) - 0.5f) - (_42759 * 0.0002442598924972116947174072265625f)) * p_Meta2.w) + _31853, 0.0f) * 2.0f);
                                                        }
                                                        else
                                                        {
                                                            float _42764;
                                                            if (_31583 && (p_TexCoord.x < 0.0f))
                                                            {
                                                                float _31883 = (_31575 * _31585) * 0.5f;
                                                                float _42755;
                                                                float _42757;
                                                                if (_31992)
                                                                {
                                                                    _42757 = _31989 - 2048.0f;
                                                                    _42755 = _31984 + 1.0f;
                                                                }
                                                                else
                                                                {
                                                                    float _42756;
                                                                    float _42758;
                                                                    if (_31989 < 0.0f)
                                                                    {
                                                                        _42758 = _31989 + 2048.0f;
                                                                        _42756 = _31984 - 1.0f;
                                                                    }
                                                                    else
                                                                    {
                                                                        _42758 = _31989;
                                                                        _42756 = _31984;
                                                                    }
                                                                    _42757 = _42758;
                                                                    _42755 = _42756;
                                                                }
                                                                _42764 = (length(_12969) - p_Meta1.z) + (min(((abs(frac((((_31602 - _31883) / p_Meta2.w) - (_42757 * 0.000488519784994423389434814453125f)) + 0.5f) - 0.5f) - (_42755 * 0.0002442598924972116947174072265625f)) * p_Meta2.w) + _31883, 0.0f) * 2.0f);
                                                            }
                                                            else
                                                            {
                                                                _42764 = -1000000.0f;
                                                            }
                                                            _42763 = _42764;
                                                        }
                                                        _42771 = max((_31958 >= 0.0f) ? _31833 : (-_31833), _42763);
                                                        break;
                                                    } while(false);
                                                    _49852 = float4(_42719, atan2(_13804 * cos(_13783), _13804 * sin(_13783)), atan2(_13815 * cos(_13791), _13801 * sin(_13791)), _13765);
                                                    _42797 = _42771;
                                                }
                                                else
                                                {
                                                    _49852 = p_Meta2;
                                                    _42797 = -1000000.0f;
                                                }
                                                float _32824 = floor(_49852.x * 0.125f);
                                                float _32829 = _49852.x - (_32824 * 8.0f);
                                                bool _32832 = _32829 >= 8.0f;
                                                float _42772;
                                                float _42774;
                                                if (_32832)
                                                {
                                                    _42774 = _32829 - 8.0f;
                                                    _42772 = _32824 + 1.0f;
                                                }
                                                else
                                                {
                                                    float _42773;
                                                    float _42775;
                                                    if (_32829 < 0.0f)
                                                    {
                                                        _42775 = _32829 + 8.0f;
                                                        _42773 = _32824 - 1.0f;
                                                    }
                                                    else
                                                    {
                                                        _42775 = _32829;
                                                        _42773 = _32824;
                                                    }
                                                    _42774 = _42775;
                                                    _42772 = _42773;
                                                }
                                                float _32703 = abs(p_TexCoord.y);
                                                float _32706 = -p_TexCoord.x;
                                                float _32710 = p_TexCoord.x - p_Meta1.w;
                                                float _32717 = _32706 - p_Meta1.z;
                                                float _32720 = (_42774 >= 2.5f) ? (-1000000.0f) : ((_42774 >= 1.5f) ? _32717 : _32706);
                                                float _32730 = (_42772 >= 2.5f) ? (-1000000.0f) : ((_42772 >= 1.5f) ? (_32710 - p_Meta1.z) : _32710);
                                                float _32733 = max(_32720, _32730);
                                                float _42776;
                                                if (((_32720 > _32730) ? _42774 : _42772) < 0.5f)
                                                {
                                                    _42776 = length(float2(max(_32733, 0.0f), _32703)) - p_Meta1.z;
                                                }
                                                else
                                                {
                                                    float _32754 = _32703 - p_Meta1.z;
                                                    _42776 = min(max(_32733, _32754), 0.0f) + length(max(float2(_32733, _32754), 0.0f.xx));
                                                }
                                                float _42777;
                                                if (_42774 >= 3.5f)
                                                {
                                                    float _32772 = sin(_49852.y);
                                                    _42777 = max(_42776, dot(_12969, float2(cos(_49852.y), _32772)) - (p_Meta1.z * abs(_32772)));
                                                }
                                                else
                                                {
                                                    _42777 = _42776;
                                                }
                                                float _42778;
                                                if (_42772 >= 3.5f)
                                                {
                                                    float _32795 = sin(_49852.z);
                                                    _42778 = max(_42777, dot(_12969 - float2(p_Meta1.w, 0.0f), float2(cos(_49852.z), _32795)) - (p_Meta1.z * abs(_32795)));
                                                }
                                                else
                                                {
                                                    _42778 = _42777;
                                                }
                                                float _42779;
                                                float _42781;
                                                if (_32832)
                                                {
                                                    _42781 = _32829 - 8.0f;
                                                    _42779 = _32824 + 1.0f;
                                                }
                                                else
                                                {
                                                    float _42780;
                                                    float _42782;
                                                    if (_32829 < 0.0f)
                                                    {
                                                        _42782 = _32829 + 8.0f;
                                                        _42780 = _32824 - 1.0f;
                                                    }
                                                    else
                                                    {
                                                        _42782 = _32829;
                                                        _42780 = _32824;
                                                    }
                                                    _42781 = _42782;
                                                    _42779 = _42780;
                                                }
                                                float _32900 = (_42781 >= 2.5f) ? (-1000000.0f) : ((_42781 >= 1.5f) ? _32717 : _32706);
                                                float _32910 = (_42779 >= 2.5f) ? (-1000000.0f) : ((_42779 >= 1.5f) ? (_32710 - _49852.w) : _32710);
                                                float _32913 = max(_32900, _32910);
                                                float _32924 = (p_Meta1.z - _49852.w) / p_Meta1.w;
                                                float _42783;
                                                if (abs(_32924) >= 1.0f)
                                                {
                                                    float2 _32933 = float2(p_Meta1.w, 0.0f);
                                                    bool2 _32934 = (p_Meta1.z >= _49852.w).xx;
                                                    _42783 = length(_12969 - float2(_32934.x ? 0.0f.xx.x : _32933.x, _32934.y ? 0.0f.xx.y : _32933.y)) - max(p_Meta1.z, _49852.w);
                                                }
                                                else
                                                {
                                                    float _32949 = sqrt(1.0f - (_32924 * _32924));
                                                    float _32959 = ((_32924 * p_TexCoord.x) + (_32949 * _32703)) - p_Meta1.z;
                                                    float _32967 = (_32949 * p_TexCoord.x) - (_32924 * _32703);
                                                    float _42784;
                                                    if (((_32900 > _32910) ? _42781 : _42779) < 0.5f)
                                                    {
                                                        float _42785;
                                                        if ((_32967 < 0.0f) && (_42781 < 2.5f))
                                                        {
                                                            _42785 = length(_12969) - p_Meta1.z;
                                                        }
                                                        else
                                                        {
                                                            float _42786;
                                                            if ((_32967 > (_32949 * p_Meta1.w)) && (_42779 < 2.5f))
                                                            {
                                                                _42786 = length(_12969 - float2(p_Meta1.w, 0.0f)) - _49852.w;
                                                            }
                                                            else
                                                            {
                                                                _42786 = _32959;
                                                            }
                                                            _42785 = _42786;
                                                        }
                                                        _42784 = _42785;
                                                    }
                                                    else
                                                    {
                                                        _42784 = min(max(_32913, _32959), 0.0f) + length(max(float2(_32913, _32959), 0.0f.xx));
                                                    }
                                                    _42783 = _42784;
                                                }
                                                float _42787;
                                                if (_42781 >= 3.5f)
                                                {
                                                    float _33023 = sin(_49852.y);
                                                    _42787 = max(_42783, dot(_12969, float2(cos(_49852.y), _33023)) - (p_Meta1.z * abs(_33023)));
                                                }
                                                else
                                                {
                                                    _42787 = _42783;
                                                }
                                                float _42788;
                                                if (_42779 >= 3.5f)
                                                {
                                                    float _33046 = sin(_49852.z);
                                                    _42788 = max(_42787, dot(_12969 - float2(p_Meta1.w, 0.0f), float2(cos(_49852.z), _33046)) - (_49852.w * abs(_33046)));
                                                }
                                                else
                                                {
                                                    _42788 = _42787;
                                                }
                                                _46021 = false;
                                                _44787 = 1.0f;
                                                _44685 = 1.0f;
                                                _43617 = _13734 ? 0.0f : _42711;
                                                _43518 = max((_49852.w == p_Meta1.z) ? _42778 : _42788, _42797);
                                                _43425 = _13734 ? 0.0f : p_TexCoord.z;
                                            }
                                            _46019 = _46021;
                                            _44785 = _44787;
                                            _44683 = _44685;
                                            _44366 = 0.0f;
                                            _44207 = 0.0f;
                                            _44041 = float2(1.0f, 0.0f);
                                            _43884 = 0.0f;
                                            _43727 = false;
                                            _43615 = _43617;
                                            _43516 = _43518;
                                            _43423 = _43425;
                                        }
                                        _46017 = _46019;
                                        _44783 = _44785;
                                        _44681 = _44683;
                                        _44364 = _44366;
                                        _44205 = _44207;
                                        _44039 = _44041;
                                        _43882 = _43884;
                                        _43725 = _43727;
                                        _43613 = _43615;
                                        _43514 = _43516;
                                        _43421 = _43423;
                                    }
                                    _46010 = _46017;
                                    _44765 = _44783;
                                    _44674 = _44681;
                                    _44346 = _44364;
                                    _44187 = _44205;
                                    _44021 = _44039;
                                    _43864 = _43882;
                                    _43707 = _43725;
                                    _43600 = _43613;
                                    _43501 = _43514;
                                    _43403 = _43421;
                                }
                                _50500 = 0.0f.xx;
                                _45995 = _46010;
                                _45834 = 0.0f.xxxx;
                                _45711 = float4(0.0f, 0.0f, 1.0f, 0.0f);
                                _45583 = 0.0f.xxxx;
                                _45462 = float4(0.0f, 0.0f, 1.0f, 0.0f);
                                _45341 = 0.0f;
                                _45222 = float2(1.0f, 0.0f);
                                _45103 = float2(1.0f, 0.0f);
                                _44984 = 0.0f.xx;
                                _44865 = 0.0f.xx;
                                _44763 = _44765;
                                _44672 = _44674;
                                _44331 = _44346;
                                _44172 = _44187;
                                _44006 = _44021;
                                _43849 = _43864;
                                _43692 = _43707;
                                _43585 = _43600;
                                _43486 = _43501;
                                _43388 = _43403;
                            }
                            _50496 = _50500;
                            _45984 = _45995;
                            _45830 = _45834;
                            _45707 = _45711;
                            _45579 = _45583;
                            _45458 = _45462;
                            _45337 = _45341;
                            _45218 = _45222;
                            _45099 = _45103;
                            _44980 = _44984;
                            _44861 = _44865;
                            _44759 = _44763;
                            _44668 = _44672;
                            _44320 = _44331;
                            _44161 = _44172;
                            _43995 = _44006;
                            _43838 = _43849;
                            _43681 = _43692;
                            _43576 = _43585;
                            _43476 = _43486;
                            _43379 = _43388;
                        }
                        _50485 = _50496;
                        _45973 = _45984;
                        _45819 = _45830;
                        _45696 = _45707;
                        _45568 = _45579;
                        _45447 = _45458;
                        _45326 = _45337;
                        _45207 = _45218;
                        _45088 = _45099;
                        _44969 = _44980;
                        _44850 = _44861;
                        _44748 = _44759;
                        _44657 = _44668;
                        _44309 = _44320;
                        _44150 = _44161;
                        _43984 = _43995;
                        _43827 = _43838;
                        _43670 = _43681;
                        _43574 = _43576;
                        _43473 = _43476;
                        _43376 = _43379;
                    }
                    _50483 = _50485;
                    _45943 = _45973;
                    _45817 = _45819;
                    _45694 = _45696;
                    _45566 = _45568;
                    _45445 = _45447;
                    _45324 = _45326;
                    _45205 = _45207;
                    _45086 = _45088;
                    _44967 = _44969;
                    _44848 = _44850;
                    _44746 = _44748;
                    _44655 = _44657;
                    _44279 = _44309;
                    _44120 = _44150;
                    _43954 = _43984;
                    _43797 = _43827;
                    _43640 = _43670;
                    _43544 = _43574;
                    _43472 = _43473;
                    _43346 = _43376;
                }
                _50479 = _50483;
                _45939 = _45943;
                _45813 = _45817;
                _45690 = _45694;
                _45562 = _45566;
                _45441 = _45445;
                _45322 = _45324;
                _45203 = _45205;
                _45084 = _45086;
                _44965 = _44967;
                _44846 = _44848;
                _44744 = _44746;
                _44653 = _44655;
                _44275 = _44279;
                _44116 = _44120;
                _43950 = _43954;
                _43793 = _43797;
                _43636 = _43640;
                _43540 = _43544;
                _43468 = _43472;
                _43342 = _43346;
            }
            _50476 = _50479;
            _45936 = _45939;
            _45810 = _45813;
            _45687 = _45690;
            _45559 = _45562;
            _45438 = _45441;
            _45319 = _45322;
            _45200 = _45203;
            _45081 = _45084;
            _44962 = _44965;
            _44843 = _44846;
            _44741 = _44744;
            _44650 = _44653;
            _44433 = 1.0f;
            _44272 = _44275;
            _44113 = _44116;
            _43947 = _43950;
            _43790 = _43793;
            _43633 = _43636;
            _43537 = _43540;
            _43465 = _43468;
            _43339 = _43342;
        }
        float _13879 = _43465 - _43339;
        bool _13881 = _43537 >= 0.5f;
        float _44430;
        if (_13881 && _43633)
        {
            float _33143 = floor(_43947.y * 0.00048828125f);
            float _33148 = _43947.y - (_33143 * 2048.0f);
            float _44104;
            float _44106;
            if (_33148 >= 2048.0f)
            {
                _44106 = _33148 - 2048.0f;
                _44104 = _33143 + 1.0f;
            }
            else
            {
                float _44105;
                float _44107;
                if (_33148 < 0.0f)
                {
                    _44107 = _33148 + 2048.0f;
                    _44105 = _33143 - 1.0f;
                }
                else
                {
                    _44107 = _33148;
                    _44105 = _33143;
                }
                _44106 = _44107;
                _44104 = _44105;
            }
            float _33135 = (abs(frac(((_43790 / _43947.x) - (_44106 * 0.000488519784994423389434814453125f)) + 0.5f) - 0.5f) - (_44104 * 0.0002442598924972116947174072265625f)) * _43947.x;
            float _44431;
            if (_43537 >= 1.5f)
            {
                _44431 = max(_13879, length(float2(max(_33135, 0.0f), _44113)) - _44272);
            }
            else
            {
                _44431 = max(_13879, _33135);
            }
            _44430 = _44431;
        }
        else
        {
            _44430 = _13879;
        }
        float _33177 = ddx(_44430);
        float _33179 = ddy(_44430);
        float _33183 = abs(_33177);
        float _33186 = abs(_33179);
        float _33192 = clamp(max(_33183, _33186) / _14849, 0.0f, 1.0f);
        float _33225 = clamp(_14876 ? min(_33183 + _33186, _14846 * (_33192 + sqrt(1.0f - (_33192 * _33192)))) : length(float2(_33177, _33179)), _14850, _14906);
        if (_42709 >= 0.5f)
        {
            float _13917 = max(p_Meta1.y, 0.5f * _33225);
            float _13919 = 3.0f * _13917;
            bool _13924 = p_Meta1.x > 0.0f;
            if ((_44430 >= _13919) || (_13924 && (_44430 <= ((-p_Meta1.x) - _13919))))
            {
                discard;
            }
            float _33231 = _13917 * 1.41421353816986083984375f;
            float _33232 = _44430 / _33231;
            float _33242 = abs(_33232);
            float _33245 = 1.0f + (0.3275910913944244384765625f * _33242);
            float _33246 = 1.0f / _33245;
            float _33266 = (((((((((1.0614054203033447265625f / _33245) - 1.45315206050872802734375f) * _33246) + 1.42141377925872802734375f) * _33246) - 0.284496724605560302734375f) * _33246) + 0.254829585552215576171875f) * _33246) * exp((-_33242) * _33242);
            float _33235 = 0.5f * (1.0f - ((_33232 < 0.0f) ? (_33266 - 1.0f) : (1.0f - _33266)));
            float _47649;
            if (_13924)
            {
                float _33280 = (_44430 + p_Meta1.x) / _33231;
                float _33290 = abs(_33280);
                float _33293 = 1.0f + (0.3275910913944244384765625f * _33290);
                float _33294 = 1.0f / _33293;
                float _33314 = (((((((((1.0614054203033447265625f / _33293) - 1.45315206050872802734375f) * _33294) + 1.42141377925872802734375f) * _33294) - 0.284496724605560302734375f) * _33294) + 0.254829585552215576171875f) * _33294) * exp((-_33290) * _33290);
                _47649 = _33235 - (0.5f * (1.0f - ((_33280 < 0.0f) ? (_33314 - 1.0f) : (1.0f - _33314))));
            }
            else
            {
                _47649 = _33235;
            }
            float _33357 = floor(p_Fill.x * 0.00048828125f);
            float _33362 = p_Fill.x - (_33357 * 2048.0f);
            float _47639;
            float _47641;
            if (_33362 >= 2048.0f)
            {
                _47641 = _33362 - 2048.0f;
                _47639 = _33357 + 1.0f;
            }
            else
            {
                float _47640;
                float _47642;
                if (_33362 < 0.0f)
                {
                    _47642 = _33362 + 2048.0f;
                    _47640 = _33357 - 1.0f;
                }
                else
                {
                    _47642 = _33362;
                    _47640 = _33357;
                }
                _47641 = _47642;
                _47639 = _47640;
            }
            float2 _33349 = float2(_47639, _47641) * 0.000488519784994423389434814453125f.xx;
            float _33405 = floor(p_Fill.y * 0.00048828125f);
            float _33410 = p_Fill.y - (_33405 * 2048.0f);
            float _47643;
            float _47645;
            if (_33410 >= 2048.0f)
            {
                _47645 = _33410 - 2048.0f;
                _47643 = _33405 + 1.0f;
            }
            else
            {
                float _47644;
                float _47646;
                if (_33410 < 0.0f)
                {
                    _47646 = _33410 + 2048.0f;
                    _47644 = _33405 - 1.0f;
                }
                else
                {
                    _47646 = _33410;
                    _47644 = _33405;
                }
                _47645 = _47646;
                _47643 = _47644;
            }
            float2 _33397 = float2(_47643, _47645) * 0.000488519784994423389434814453125f.xx;
            float _33332 = _33349.x;
            float _33333 = _33349.y;
            float _33334 = _33397.x;
            float4 _33336 = float4(_33332, _33333, _33334, _33397.y);
            float _13956 = _33397.y * _47649;
            _33336.w = _13956;
            float4 _47652;
            if (_42707 < 0.5f)
            {
                float _33446 = _33333 * 0.4000000059604644775390625f;
                float _33450 = (_33334 * 6.283185482025146484375f) - 3.1415927410125732421875f;
                float _33456 = _33446 * cos(_33450);
                float _33460 = _33446 * sin(_33450);
                float _33509 = (_33332 + (0.3963377773761749267578125f * _33456)) + (0.21580375730991363525390625f * _33460);
                float _33519 = (_33332 - (0.1055613458156585693359375f * _33456)) - (0.06385417282581329345703125f * _33460);
                float _33529 = (_33332 - (0.089484177529811859130859375f * _33456)) - (1.2914855480194091796875f * _33460);
                float _33534 = (_33509 * _33509) * _33509;
                float _33539 = (_33519 * _33519) * _33519;
                float _33544 = (_33529 * _33529) * _33529;
                float _33552 = ((4.076741695404052734375f * _33534) - (3.30771160125732421875f * _33539)) + (0.2309699356555938720703125f * _33544);
                float _33560 = (((-1.26843798160552978515625f) * _33534) + (2.60975742340087890625f * _33539)) - (0.341319382190704345703125f * _33544);
                float _33568 = (((-0.0041960864327847957611083984375f) * _33534) - (0.70341861248016357421875f * _33539)) + (1.7076146602630615234375f * _33544);
                _47652 = float4((_33552 >= 0.003130800090730190277099609375f) ? ((pow(abs(_33552), 0.4166666567325592041015625f) * 1.05499994754791259765625f) - 0.054999999701976776123046875f) : (12.9200000762939453125f * _33552), (_33560 >= 0.003130800090730190277099609375f) ? ((pow(abs(_33560), 0.4166666567325592041015625f) * 1.05499994754791259765625f) - 0.054999999701976776123046875f) : (12.9200000762939453125f * _33560), (_33568 >= 0.003130800090730190277099609375f) ? ((pow(abs(_33568), 0.4166666567325592041015625f) * 1.05499994754791259765625f) - 0.054999999701976776123046875f) : (12.9200000762939453125f * _33568), _13956);
            }
            else
            {
                float4 _47653;
                if (_42707 < 1.5f)
                {
                    float _33474 = (_33333 * 0.800000011920928955078125f) - 0.4000000059604644775390625f;
                    float _33478 = (_33334 * 0.800000011920928955078125f) - 0.4000000059604644775390625f;
                    float _33637 = (_33332 + (0.3963377773761749267578125f * _33474)) + (0.21580375730991363525390625f * _33478);
                    float _33647 = (_33332 - (0.1055613458156585693359375f * _33474)) - (0.06385417282581329345703125f * _33478);
                    float _33657 = (_33332 - (0.089484177529811859130859375f * _33474)) - (1.2914855480194091796875f * _33478);
                    float _33662 = (_33637 * _33637) * _33637;
                    float _33667 = (_33647 * _33647) * _33647;
                    float _33672 = (_33657 * _33657) * _33657;
                    float _33680 = ((4.076741695404052734375f * _33662) - (3.30771160125732421875f * _33667)) + (0.2309699356555938720703125f * _33672);
                    float _33688 = (((-1.26843798160552978515625f) * _33662) + (2.60975742340087890625f * _33667)) - (0.341319382190704345703125f * _33672);
                    float _33696 = (((-0.0041960864327847957611083984375f) * _33662) - (0.70341861248016357421875f * _33667)) + (1.7076146602630615234375f * _33672);
                    _47653 = float4((_33680 >= 0.003130800090730190277099609375f) ? ((pow(abs(_33680), 0.4166666567325592041015625f) * 1.05499994754791259765625f) - 0.054999999701976776123046875f) : (12.9200000762939453125f * _33680), (_33688 >= 0.003130800090730190277099609375f) ? ((pow(abs(_33688), 0.4166666567325592041015625f) * 1.05499994754791259765625f) - 0.054999999701976776123046875f) : (12.9200000762939453125f * _33688), (_33696 >= 0.003130800090730190277099609375f) ? ((pow(abs(_33696), 0.4166666567325592041015625f) * 1.05499994754791259765625f) - 0.054999999701976776123046875f) : (12.9200000762939453125f * _33696), _13956);
                }
                else
                {
                    _47653 = _33336;
                }
                _47652 = _47653;
            }
            float3 _13965 = _47652.xyz * _47652.w;
            float4 _49251 = _47652;
            _49251.x = _13965.x;
            _49251.y = _13965.y;
            _49251.z = _13965.z;
            float4 _13974 = _49251 * _12948;
            float4 _33755 = mul(float4(p_Pos.xy, 0.0f, 1.0f), _8606_view_projection);
            float2 _33764 = (_33755.xy / _33755.w.xx) * _8606_half_viewport;
            float3 _13986 = _13974.xyz + ((((_8606_dither_mode < 0.5f) ? clamp(frac(52.98291778564453125f * frac(dot(_33764, float2(0.067110560834407806396484375f, 0.005837149918079376220703125f)))), 0.0f, 1.0f) : BlueNoiseTex.Sample(BlueNoiseSampler, _33764 * 0.015625f.xx).x) - 0.5f) * _8606_dither_scale).xxx;
            float4 _49258 = _13974;
            _49258.x = _13986.x;
            _49258.y = _13986.y;
            _49258.z = _13986.z;
            _47662 = _49258;
            break;
        }
        float _14001 = (_12976 ? _14846 : _33225) * _12863;
        if (_12976 ? (_44433 <= 0.0f) : (_44430 >= (_14001 * _12936)))
        {
            discard;
        }
        bool _14016 = p_Fill.y < (-0.5f);
        bool _14019 = p_Fill.z < (-0.5f);
        bool _14022 = p_Fill.w < (-0.5f);
        bool _14025 = p_Border.y < (-0.5f);
        bool _14028 = p_Border.z < (-0.5f);
        bool _14031 = p_Border.w < (-0.5f);
        bool _14034 = !_14016;
        bool _14035 = _14019 && _14034;
        bool _14039 = _14028 && (!_14025);
        float _14049 = _14016 ? ((-1.0f) - p_Fill.y) : p_Fill.y;
        float _14057 = _14019 ? ((-1.0f) - p_Fill.z) : p_Fill.z;
        float _14065 = _14022 ? ((-1.0f) - p_Fill.w) : p_Fill.w;
        float _14076 = _14025 ? ((-1.0f) - p_Border.y) : p_Border.y;
        float _14084 = _14028 ? ((-1.0f) - p_Border.z) : p_Border.z;
        float _14092 = _14031 ? ((-1.0f) - p_Border.w) : p_Border.w;
        float _33824 = floor(p_Fill.x * 0.00048828125f);
        float _33829 = p_Fill.x - (_33824 * 2048.0f);
        float _44591;
        float _44593;
        if (_33829 >= 2048.0f)
        {
            _44593 = _33829 - 2048.0f;
            _44591 = _33824 + 1.0f;
        }
        else
        {
            float _44592;
            float _44594;
            if (_33829 < 0.0f)
            {
                _44594 = _33829 + 2048.0f;
                _44592 = _33824 - 1.0f;
            }
            else
            {
                _44594 = _33829;
                _44592 = _33824;
            }
            _44593 = _44594;
            _44591 = _44592;
        }
        float _33872 = floor(_14049 * 0.00048828125f);
        float _33877 = _14049 - (_33872 * 2048.0f);
        bool _33880 = _33877 >= 2048.0f;
        float _44595;
        float _44597;
        if (_33880)
        {
            _44597 = _33877 - 2048.0f;
            _44595 = _33872 + 1.0f;
        }
        else
        {
            float _44596;
            float _44598;
            if (_33877 < 0.0f)
            {
                _44598 = _33877 + 2048.0f;
                _44596 = _33872 - 1.0f;
            }
            else
            {
                _44598 = _33877;
                _44596 = _33872;
            }
            _44597 = _44598;
            _44595 = _44596;
        }
        float2 _33864 = float2(_44595, _44597) * 0.000488519784994423389434814453125f.xx;
        float4 _33803 = float4(float2(_44591, _44593) * 0.000488519784994423389434814453125f.xx, _33864);
        float _33935 = floor(_14057 * 0.00048828125f);
        float _33940 = _14057 - (_33935 * 2048.0f);
        bool _33943 = _33940 >= 2048.0f;
        float _44599;
        float _44601;
        if (_33943)
        {
            _44601 = _33940 - 2048.0f;
            _44599 = _33935 + 1.0f;
        }
        else
        {
            float _44600;
            float _44602;
            if (_33940 < 0.0f)
            {
                _44602 = _33940 + 2048.0f;
                _44600 = _33935 - 1.0f;
            }
            else
            {
                _44602 = _33940;
                _44600 = _33935;
            }
            _44601 = _44602;
            _44599 = _44600;
        }
        float _33983 = floor(_14065 * 0.00048828125f);
        float _33988 = _14065 - (_33983 * 2048.0f);
        bool _33991 = _33988 >= 2048.0f;
        float _44603;
        float _44605;
        if (_33991)
        {
            _44605 = _33988 - 2048.0f;
            _44603 = _33983 + 1.0f;
        }
        else
        {
            float _44604;
            float _44606;
            if (_33988 < 0.0f)
            {
                _44606 = _33988 + 2048.0f;
                _44604 = _33983 - 1.0f;
            }
            else
            {
                _44606 = _33988;
                _44604 = _33983;
            }
            _44605 = _44606;
            _44603 = _44604;
        }
        float2 _33975 = float2(_44603, _44605) * 0.000488519784994423389434814453125f.xx;
        float4 _33914 = float4(float2(_44599, _44601) * 0.000488519784994423389434814453125f.xx, _33975);
        float _14122 = _12976 ? 0.0f : smoothstep(0.0f, 1.0f, clamp((((_44430 + p_Meta1.x) / _14001) + 1.0f) - _12867, 0.0f, 1.0f));
        float _46078;
        if (_13881 && (!_43633))
        {
            float _14132 = _14846 * _12863;
            float _46079;
            if (_43537 >= 1.5f)
            {
                float _14137 = p_Meta1.x * 0.5f;
                float _14141 = abs(_44430 + _14137);
                float _14143 = _14141 - _14137;
                bool _14145 = _44741 < 0.0f;
                float _14147 = _14145 ? 1.0f : (-1.0f);
                float2 _14150 = _12969 - _44843;
                float2 _14153 = _12969 - _44962;
                float _14155 = length(_14150);
                float _14157 = length(_14153);
                float2 _34018 = float2(-_45081.y, _45081.x);
                float2 _34026 = float2(-_45200.y, _45200.x);
                float _14191 = -_14147;
                float2 _14197 = min(float2(-dot(_14150, _45081), dot(_14153, _45200)) - (abs(float2(dot(_14150, _34018), dot(_14153, _34026))) - _14141.xx), _45319.xx - float2(_14155, _14157)) * _14147;
                float _14200 = _45319 - _14137;
                float2 _14203 = _50476 * float2(_14147, _14191);
                bool2 _14204 = bool2(_14203.x > 0.0f.xx.x, _14203.y > 0.0f.xx.y);
                float _34057 = (_45559.w >= 0.0f) ? 1.0f : (-1.0f);
                float2 _34068 = _34018 * ((-_34057) * _50476.x);
                float _34076 = length((_45559.xy + (_34068 * _45559.z)) - _44843);
                float2 _34079 = _45081 * _14147;
                float _34202 = length(_14150 - (_34079 * clamp(dot(_14150, _34079), 0.0f, max(min(_34076, _14200), 0.0f))));
                float _34088 = _14200 - _34076;
                float _45675;
                if (_34088 > 0.0f)
                {
                    float _34097 = abs(_45559.w);
                    float _34101 = _34057 * min(_34088 / max(_45559.z, 9.9999999747524270787835121154785e-07f), _34097);
                    float _34208 = sin(_34101);
                    float _34210 = cos(_34101);
                    float _34212 = _34068.x;
                    float _34216 = _34068.y;
                    float _34219 = (_34212 * _34210) - (_34216 * _34208);
                    float _34228 = (_34212 * _34208) + (_34216 * _34210);
                    float2 _34106 = _12969 - _45559.xy;
                    float _34110 = _34106.y;
                    float _34115 = _34106.x;
                    float _34152 = min(_34202, min((min(((_34212 * _34110) - (_34216 * _34115)) * _34057, ((_34115 * _34228) - (_34110 * _34219)) * _34057) >= 0.0f) ? abs(length(_34106) - _45559.z) : 1000000.0f, length(_12969 - (_45559.xy + (float2(_34219, _34228) * _45559.z)))));
                    float _34158 = _34088 - (_45559.z * _34097);
                    float _45676;
                    if (_34158 > 0.0f)
                    {
                        float2 _34166 = _45438.zw * _14147;
                        float2 _34235 = _12969 - _45438.xy;
                        _45676 = min(_34152, length(_34235 - (_34166 * clamp(dot(_34235, _34166), 0.0f, max(_34158, 0.0f)))));
                    }
                    else
                    {
                        _45676 = _34152;
                    }
                    _45675 = _45676;
                }
                else
                {
                    _45675 = _34202;
                }
                float _14227 = min(_14204.x ? (_45675 - _14137) : max(_14143, _14197.x), _14155 - _14137);
                float _34277 = (_45810.w >= 0.0f) ? 1.0f : (-1.0f);
                float2 _34288 = _34026 * ((-_34277) * _50476.y);
                float _34296 = length((_45810.xy + (_34288 * _45810.z)) - _44962);
                float2 _34299 = _45200 * _14191;
                float _34422 = length(_14153 - (_34299 * clamp(dot(_14153, _34299), 0.0f, max(min(_34296, _14200), 0.0f))));
                float _34308 = _14200 - _34296;
                float _45927;
                if (_34308 > 0.0f)
                {
                    float _34317 = abs(_45810.w);
                    float _34321 = _34277 * min(_34308 / max(_45810.z, 9.9999999747524270787835121154785e-07f), _34317);
                    float _34428 = sin(_34321);
                    float _34430 = cos(_34321);
                    float _34432 = _34288.x;
                    float _34436 = _34288.y;
                    float _34439 = (_34432 * _34430) - (_34436 * _34428);
                    float _34448 = (_34432 * _34428) + (_34436 * _34430);
                    float2 _34326 = _12969 - _45810.xy;
                    float _34330 = _34326.y;
                    float _34335 = _34326.x;
                    float _34372 = min(_34422, min((min(((_34432 * _34330) - (_34436 * _34335)) * _34277, ((_34335 * _34448) - (_34330 * _34439)) * _34277) >= 0.0f) ? abs(length(_34326) - _45810.z) : 1000000.0f, length(_12969 - (_45810.xy + (float2(_34439, _34448) * _45810.z)))));
                    float _34378 = _34308 - (_45810.z * _34317);
                    float _45928;
                    if (_34378 > 0.0f)
                    {
                        float2 _34386 = _45687.zw * _14191;
                        float2 _34455 = _12969 - _45687.xy;
                        _45928 = min(_34372, length(_34455 - (_34386 * clamp(dot(_34455, _34386), 0.0f, max(_34378, 0.0f)))));
                    }
                    else
                    {
                        _45928 = _34372;
                    }
                    _45927 = _45928;
                }
                else
                {
                    _45927 = _34422;
                }
                float _14250 = min(_14204.y ? (_45927 - _14137) : max(_14143, _14197.y), _14157 - _14137);
                _46079 = 1.0f - smoothstep(0.0f, 1.0f, clamp(((_45936 ? _44650 : (_14145 ? max(_14227, _14250) : min(_14227, _14250))) / _14132) + _12867, 0.0f, 1.0f));
            }
            else
            {
                _46079 = _14122 * (1.0f - smoothstep(0.0f, 1.0f, clamp((_44650 / _14132) + _12867, 0.0f, 1.0f)));
            }
            _46078 = _46079;
        }
        else
        {
            _46078 = _14122;
        }
        bool _14312 = p_Fill.x < (-0.5f);
        bool _14314 = !_14312;
        bool _14330 = !(((all(bool4(p_Fill.x == p_Border.x, p_Fill.y == p_Border.y, p_Fill.z == p_Border.z, p_Fill.w == p_Border.w)) && all(bool4(p_FillCoord.x == p_BorderCoord.x, p_FillCoord.y == p_BorderCoord.y, p_FillCoord.z == p_BorderCoord.z, p_FillCoord.w == p_BorderCoord.w))) && all(bool2(_51240.x == _51241.x, _51240.y == _51241.y))) && all(bool2(p_Meta3.xy.x == p_Meta3.zw.x, p_Meta3.xy.y == p_Meta3.zw.y)));
        bool _14331 = ((((_14314 && _14034) && (!_14035)) && (_33864.y == 0.0f)) && (_33975.y == 0.0f)) && _14330;
        if (_14331 && (_46078 <= 0.0f))
        {
            discard;
        }
        float4 _51234;
        if (!_14331)
        {
            bool _14343 = _14016 || _14035;
            float _46312;
            float _46313;
            float _46315;
            if (_42691 < 0.5f)
            {
                _46315 = 0.0f;
                _46313 = 0.0f;
                _46312 = 1.0f;
            }
            else
            {
                float2 _34523 = p_FillCoord.zw - p_FillCoord.xy;
                float _34524 = length(_34523);
                bool _34530 = _42691 < 9.5f;
                float2 _46086;
                do
                {
                    float _34814 = p_FillCoord.x - p_Pos.x;
                    float _34819 = p_Pos.y - p_FillCoord.y;
                    if (_34524 == 0.0f)
                    {
                        _46086 = p_Pos.xy;
                        break;
                    }
                    float _34827 = (p_FillCoord.z - p_FillCoord.x) / _34524;
                    float _34830 = (p_FillCoord.w - p_FillCoord.y) / _34524;
                    _46086 = float2((_34827 * _34814) - (_34830 * _34819), (_34830 * _34814) + (_34827 * _34819));
                    break;
                } while(false);
                bool2 _34540 = ((_42691 >= 3.5f) && _34530).xx;
                float2 _34541 = float2(_34540.x ? _46086.x : p_Pos.xy.x, _34540.y ? _46086.y : p_Pos.xy.y);
                float _46087;
                float _46289;
                float _46318;
                if (_42691 < 1.5f)
                {
                    _46318 = 0.0f;
                    _46289 = _14001 / _34524;
                    _46087 = length(p_Pos.xy - p_FillCoord.xy) / _34524;
                }
                else
                {
                    float _46088;
                    float _46290;
                    float _46320;
                    if (_42691 < 2.5f)
                    {
                        _46320 = 0.0f;
                        _46290 = _14001 / _34524;
                        _46088 = dot(_34523, p_Pos.xy - p_FillCoord.xy) / (_34524 * _34524);
                    }
                    else
                    {
                        float _46089;
                        float _46291;
                        float _46321;
                        if (_42691 < 3.5f)
                        {
                            _46321 = 0.0f;
                            _46291 = _14001 / _34524;
                            _46089 = abs(dot(_34523, p_Pos.xy - p_FillCoord.xy)) / (_34524 * _34524);
                        }
                        else
                        {
                            float _46090;
                            float _46292;
                            float _46322;
                            if (_42691 < 4.5f)
                            {
                                _46322 = 0.0f;
                                _46292 = _14001 / (3.1415927410125732421875f * max(length(p_FillCoord.xy - p_Pos.xy), 9.9999999747524270787835121154785e-07f));
                                _46090 = abs(atan2(-_34541.y, -_34541.x) * 0.3183098733425140380859375f);
                            }
                            else
                            {
                                float _46091;
                                float _46293;
                                float _46323;
                                if (_42691 < 5.5f)
                                {
                                    float _34904 = (atan2(_34541.y, _34541.x) * 0.15915493667125701904296875f) + 0.5f;
                                    float _34612 = length(p_FillCoord.xy - p_Pos.xy);
                                    float _46092;
                                    if (_14314 && (!_14343))
                                    {
                                        float _34914 = max(0.5f * clamp(_14001 / (6.283185482025146484375f * _34612), 0.0f, 1.0f), 9.9999999747524270787835121154785e-07f);
                                        float _34916 = frac(_34904);
                                        _46092 = lerp(_34916, 0.5f, max(clamp((_34916 - (1.0f - _34914)) / _34914, 0.0f, 1.0f), clamp((_34914 - _34916) / _34914, 0.0f, 1.0f)));
                                    }
                                    else
                                    {
                                        _46092 = _34904;
                                    }
                                    _46323 = 1.0f;
                                    _46293 = _14001 / (6.283185482025146484375f * max(_34612, 9.9999999747524270787835121154785e-07f));
                                    _46091 = _46092;
                                }
                                else
                                {
                                    float _46093;
                                    float _46295;
                                    float _46325;
                                    if (_42691 < 6.5f)
                                    {
                                        _46325 = 0.0f;
                                        _46295 = _14001 / _34524;
                                        _46093 = max(abs(_34541.x), abs(_34541.y)) / _34524;
                                    }
                                    else
                                    {
                                        float _46094;
                                        float _46296;
                                        float _46326;
                                        if (_42691 < 7.5f)
                                        {
                                            _46326 = 0.0f;
                                            _46296 = _14001 / _34524;
                                            _46094 = min(abs(_34541.x), abs(_34541.y)) / _34524;
                                        }
                                        else
                                        {
                                            float _46095;
                                            float _46297;
                                            if (_34530)
                                            {
                                                float _34979 = frac(((((_42691 < 8.5f) ? 1.0f : (-1.0f)) * atan2(-_34541.y, -_34541.x)) * 0.15915493667125701904296875f) + (length(_34541) / _34524));
                                                float _34987 = 6.283185482025146484375f * length(p_Pos.xy - p_FillCoord.xy);
                                                float _34673 = _14001 * sqrt((1.0f / (_34524 * _34524)) + (1.0f / max(_34987 * _34987, 9.9999999600419720025001879548654e-13f)));
                                                float _46096;
                                                if (_14314 && (!_14343))
                                                {
                                                    float _35008 = max(0.5f * clamp(_34673, 0.0f, 1.0f), 9.9999999747524270787835121154785e-07f);
                                                    float _35010 = frac(_34979);
                                                    _46096 = lerp(_35010, 0.5f, max(clamp((_35010 - (1.0f - _35008)) / _35008, 0.0f, 1.0f), clamp((_35008 - _35010) / _35008, 0.0f, 1.0f)));
                                                }
                                                else
                                                {
                                                    _46096 = _34979;
                                                }
                                                _46297 = _34673;
                                                _46095 = _46096;
                                            }
                                            else
                                            {
                                                float _46097;
                                                float _46299;
                                                if (_42691 < 10.5f)
                                                {
                                                    float _35036 = p_FillCoord.y - p_FillCoord.x;
                                                    _46299 = _14001 / max(abs(_35036), 9.9999999747524270787835121154785e-07f);
                                                    _46097 = (_44430 - p_FillCoord.x) / _35036;
                                                }
                                                else
                                                {
                                                    _46299 = 0.0f;
                                                    _46097 = _46115;
                                                }
                                                _46297 = _46299;
                                                _46095 = _46097;
                                            }
                                            _46326 = float(_34530);
                                            _46296 = _46297;
                                            _46094 = _46095;
                                        }
                                        _46325 = _46326;
                                        _46295 = _46296;
                                        _46093 = _46094;
                                    }
                                    _46323 = _46325;
                                    _46293 = _46295;
                                    _46091 = _46093;
                                }
                                _46322 = _46323;
                                _46292 = _46293;
                                _46090 = _46091;
                            }
                            _46321 = _46322;
                            _46291 = _46292;
                            _46089 = _46090;
                        }
                        _46320 = _46321;
                        _46290 = _46291;
                        _46088 = _46089;
                    }
                    _46318 = _46320;
                    _46289 = _46290;
                    _46087 = _46088;
                }
                float _46301;
                float _46306;
                float _46317;
                if (_42695 < 0.5f)
                {
                    _46317 = _46318;
                    _46306 = _46289;
                    _46301 = _46087;
                }
                else
                {
                    float _46302;
                    float _46307;
                    float _46330;
                    if (_42695 < 1.5f)
                    {
                        float _35041 = frac(_46087);
                        float _46303;
                        if (_14314 && (!_14343))
                        {
                            float _35051 = max(0.5f * clamp(_14001 / _34524, 0.0f, 1.0f), 9.9999999747524270787835121154785e-07f);
                            float _35053 = frac(_35041);
                            _46303 = lerp(_35053, 0.5f, max(clamp((_35053 - (1.0f - _35051)) / _35051, 0.0f, 1.0f), clamp((_35051 - _35053) / _35051, 0.0f, 1.0f)));
                        }
                        else
                        {
                            _46303 = _35041;
                        }
                        _46330 = 1.0f;
                        _46307 = _46289;
                        _46302 = _46303;
                    }
                    else
                    {
                        float _46304;
                        float _46309;
                        float _46332;
                        if (_42695 < 2.5f)
                        {
                            _46332 = 0.0f;
                            _46309 = _46289;
                            _46304 = abs(mod(mod(_46087 + 1.0f, 2.0f) + 2.0f, 2.0f) - 1.0f);
                        }
                        else
                        {
                            bool _34746 = _42695 < 3.5f;
                            float _46305;
                            float _46310;
                            if (_34746)
                            {
                                float _34750 = (_46087 * 3.1415927410125732421875f) - 1.5f;
                                _46310 = _46289 * (abs(cos(_34750)) * 1.57079637050628662109375f);
                                _46305 = (sin(_34750) * 0.5f) + 0.5f;
                            }
                            else
                            {
                                _46310 = _46289;
                                _46305 = _46087;
                            }
                            _46332 = _34746 ? 0.0f : _46318;
                            _46309 = _46310;
                            _46304 = _46305;
                        }
                        _46330 = _46332;
                        _46307 = _46309;
                        _46302 = _46304;
                    }
                    _46317 = _46330;
                    _46306 = _46307;
                    _46301 = _46302;
                }
                float _35109 = (1.0f - p_Meta3.y) - p_Meta3.x;
                _46315 = ((p_Meta3.x != 0.0f) || (p_Meta3.y != 0.0f)) ? 0.0f : _46317;
                _46313 = _46306 / max(abs(_35109), 9.9999999747524270787835121154785e-07f);
                _46312 = clamp((_46301 - p_Meta3.x) / _35109, 0.0f, 1.0f);
            }
            float4 _46614;
            float _46616;
            if (_14312)
            {
                float _35181 = (-1.0f) - p_Fill.x;
                float _35370 = floor(_35181 * 0.00048828125f);
                float _35375 = _35181 - (_35370 * 2048.0f);
                float _46376;
                float _46378;
                if (_35375 >= 2048.0f)
                {
                    _46378 = _35375 - 2048.0f;
                    _46376 = _35370 + 1.0f;
                }
                else
                {
                    float _46377;
                    float _46379;
                    if (_35375 < 0.0f)
                    {
                        _46379 = _35375 + 2048.0f;
                        _46377 = _35370 - 1.0f;
                    }
                    else
                    {
                        _46379 = _35375;
                        _46377 = _35370;
                    }
                    _46378 = _46379;
                    _46376 = _46377;
                }
                float _46380;
                float _46382;
                if (_33880)
                {
                    _46382 = _33877 - 2048.0f;
                    _46380 = _33872 + 1.0f;
                }
                else
                {
                    float _46381;
                    float _46383;
                    if (_33877 < 0.0f)
                    {
                        _46383 = _33877 + 2048.0f;
                        _46381 = _33872 - 1.0f;
                    }
                    else
                    {
                        _46383 = _33877;
                        _46381 = _33872;
                    }
                    _46382 = _46383;
                    _46380 = _46381;
                }
                float _46384;
                float _46386;
                if (_33943)
                {
                    _46386 = _33940 - 2048.0f;
                    _46384 = _33935 + 1.0f;
                }
                else
                {
                    float _46385;
                    float _46387;
                    if (_33940 < 0.0f)
                    {
                        _46387 = _33940 + 2048.0f;
                        _46385 = _33935 - 1.0f;
                    }
                    else
                    {
                        _46387 = _33940;
                        _46385 = _33935;
                    }
                    _46386 = _46387;
                    _46384 = _46385;
                }
                float _46388;
                float _46390;
                if (_33991)
                {
                    _46390 = _33988 - 2048.0f;
                    _46388 = _33983 + 1.0f;
                }
                else
                {
                    float _46389;
                    float _46391;
                    if (_33988 < 0.0f)
                    {
                        _46391 = _33988 + 2048.0f;
                        _46389 = _33983 - 1.0f;
                    }
                    else
                    {
                        _46391 = _33988;
                        _46389 = _33983;
                    }
                    _46390 = _46391;
                    _46388 = _46389;
                }
                float _35510 = floor(_46376 * 0.0078125f);
                float _35515 = _46376 - (_35510 * 128.0f);
                float _46398;
                float _46400;
                if (_35515 >= 128.0f)
                {
                    _46400 = _35515 - 128.0f;
                    _46398 = _35510 + 1.0f;
                }
                else
                {
                    float _46399;
                    float _46401;
                    if (_35515 < 0.0f)
                    {
                        _46401 = _35515 + 128.0f;
                        _46399 = _35510 - 1.0f;
                    }
                    else
                    {
                        _46401 = _35515;
                        _46399 = _35510;
                    }
                    _46400 = _46401;
                    _46398 = _46399;
                }
                float _35545 = floor(_46378 * 0.0078125f);
                float _35550 = _46378 - (_35545 * 128.0f);
                float _46410;
                float _46412;
                if (_35550 >= 128.0f)
                {
                    _46412 = _35550 - 128.0f;
                    _46410 = _35545 + 1.0f;
                }
                else
                {
                    float _46411;
                    float _46413;
                    if (_35550 < 0.0f)
                    {
                        _46413 = _35550 + 128.0f;
                        _46411 = _35545 - 1.0f;
                    }
                    else
                    {
                        _46413 = _35550;
                        _46411 = _35545;
                    }
                    _46412 = _46413;
                    _46410 = _46411;
                }
                float _35580 = floor(_46380 * 0.0078125f);
                float _35585 = _46380 - (_35580 * 128.0f);
                float _46422;
                float _46424;
                if (_35585 >= 128.0f)
                {
                    _46424 = _35585 - 128.0f;
                    _46422 = _35580 + 1.0f;
                }
                else
                {
                    float _46423;
                    float _46425;
                    if (_35585 < 0.0f)
                    {
                        _46425 = _35585 + 128.0f;
                        _46423 = _35580 - 1.0f;
                    }
                    else
                    {
                        _46425 = _35585;
                        _46423 = _35580;
                    }
                    _46424 = _46425;
                    _46422 = _46423;
                }
                float _35615 = floor(_46382 * 0.0078125f);
                float _35620 = _46382 - (_35615 * 128.0f);
                float _46436;
                float _46438;
                if (_35620 >= 128.0f)
                {
                    _46438 = _35620 - 128.0f;
                    _46436 = _35615 + 1.0f;
                }
                else
                {
                    float _46437;
                    float _46439;
                    if (_35620 < 0.0f)
                    {
                        _46439 = _35620 + 128.0f;
                        _46437 = _35615 - 1.0f;
                    }
                    else
                    {
                        _46439 = _35620;
                        _46437 = _35615;
                    }
                    _46438 = _46439;
                    _46436 = _46437;
                }
                float _35650 = floor(_46384 * 0.0078125f);
                float _35655 = _46384 - (_35650 * 128.0f);
                float _46450;
                float _46452;
                if (_35655 >= 128.0f)
                {
                    _46452 = _35655 - 128.0f;
                    _46450 = _35650 + 1.0f;
                }
                else
                {
                    float _46451;
                    float _46453;
                    if (_35655 < 0.0f)
                    {
                        _46453 = _35655 + 128.0f;
                        _46451 = _35650 - 1.0f;
                    }
                    else
                    {
                        _46453 = _35655;
                        _46451 = _35650;
                    }
                    _46452 = _46453;
                    _46450 = _46451;
                }
                float _35685 = floor(_46386 * 0.0078125f);
                float _35690 = _46386 - (_35685 * 128.0f);
                float _46466;
                float _46468;
                if (_35690 >= 128.0f)
                {
                    _46468 = _35690 - 128.0f;
                    _46466 = _35685 + 1.0f;
                }
                else
                {
                    float _46467;
                    float _46469;
                    if (_35690 < 0.0f)
                    {
                        _46469 = _35690 + 128.0f;
                        _46467 = _35685 - 1.0f;
                    }
                    else
                    {
                        _46469 = _35690;
                        _46467 = _35685;
                    }
                    _46468 = _46469;
                    _46466 = _46467;
                }
                float3 _35225 = float3(_46398, _46410, _46422);
                float3 _46602;
                float _46609;
                float4 _51035;
                if (_14016)
                {
                    float _35720 = floor(_46388 * 0.25f);
                    float _35725 = _46388 - (_35720 * 4.0f);
                    float _46548;
                    float _46550;
                    if (_35725 >= 4.0f)
                    {
                        _46550 = _35725 - 4.0f;
                        _46548 = _35720 + 1.0f;
                    }
                    else
                    {
                        float _46549;
                        float _46551;
                        if (_35725 < 0.0f)
                        {
                            _46551 = _35725 + 4.0f;
                            _46549 = _35720 - 1.0f;
                        }
                        else
                        {
                            _46551 = _35725;
                            _46549 = _35720;
                        }
                        _46550 = _46551;
                        _46548 = _46549;
                    }
                    float _35755 = floor(_46548 * 0.25f);
                    float _35760 = _46548 - (_35755 * 4.0f);
                    float _46552;
                    float _46554;
                    if (_35760 >= 4.0f)
                    {
                        _46554 = _35760 - 4.0f;
                        _46552 = _35755 + 1.0f;
                    }
                    else
                    {
                        float _46553;
                        float _46555;
                        if (_35760 < 0.0f)
                        {
                            _46555 = _35760 + 4.0f;
                            _46553 = _35755 - 1.0f;
                        }
                        else
                        {
                            _46555 = _35760;
                            _46553 = _35755;
                        }
                        _46554 = _46555;
                        _46552 = _46553;
                    }
                    float _35790 = floor(_46552 * 0.25f);
                    float _35795 = _46552 - (_35790 * 4.0f);
                    float _46556;
                    float _46558;
                    if (_35795 >= 4.0f)
                    {
                        _46558 = _35795 - 4.0f;
                        _46556 = _35790 + 1.0f;
                    }
                    else
                    {
                        float _46557;
                        float _46559;
                        if (_35795 < 0.0f)
                        {
                            _46559 = _35795 + 4.0f;
                            _46557 = _35790 - 1.0f;
                        }
                        else
                        {
                            _46559 = _35795;
                            _46557 = _35790;
                        }
                        _46558 = _46559;
                        _46556 = _46557;
                    }
                    float _35825 = floor(_46390 * 0.015625f);
                    float _35830 = _46390 - (_35825 * 64.0f);
                    float _46566;
                    float _46568;
                    if (_35830 >= 64.0f)
                    {
                        _46568 = _35830 - 64.0f;
                        _46566 = _35825 + 1.0f;
                    }
                    else
                    {
                        float _46567;
                        float _46569;
                        if (_35830 < 0.0f)
                        {
                            _46569 = _35830 + 64.0f;
                            _46567 = _35825 - 1.0f;
                        }
                        else
                        {
                            _46569 = _35830;
                            _46567 = _35825;
                        }
                        _46568 = _46569;
                        _46566 = _46567;
                    }
                    bool _35260 = _46315 > 0.5f;
                    float _35901 = 0.5f * clamp(_46313 * 256.0f, 9.9999997473787516355514526367188e-06f, 256.0f);
                    float _35903 = _46312 * 256.0f;
                    float _35905 = _35903 - _35901;
                    float _35909 = _35903 + _35901;
                    float _46596;
                    float _46597;
                    if (!_35260)
                    {
                        _46597 = clamp(_35909, 0.0f, 256.0f);
                        _46596 = clamp(_35905, 0.0f, 256.0f);
                    }
                    else
                    {
                        _46597 = _35909;
                        _46596 = _35905;
                    }
                    float _35919 = floor(_46596);
                    float _35921 = floor(_46597);
                    float _35927 = _35260 ? mod(mod(_35919, 256.0f) + 256.0f, 256.0f) : min(_35919, 255.0f);
                    float _35932 = min(_35921, 255.0f);
                    float _35933 = _35260 ? mod(mod(_35921, 256.0f) + 256.0f, 256.0f) : _35932;
                    float _36096 = _35927 * 2.0f;
                    float _36105 = ((_46556 + (32.0f * _46566)) + 0.5f) * _8606_ramp_texel.y;
                    float4 _36141 = RampTex.Sample(RampSampler, float2((_36096 + 0.5f) * _8606_ramp_texel.x, _36105));
                    float2 _36134 = float2((floor((_36141.y * 255.0f) + 0.5f) * 256.0f) + floor((_36141.x * 255.0f) + 0.5f), (floor((_36141.w * 255.0f) + 0.5f) * 256.0f) + floor((_36141.z * 255.0f) + 0.5f)) * 1.525902189314365386962890625e-05f.xx;
                    float _36147 = _35933 * 2.0f;
                    float4 _36192 = RampTex.Sample(RampSampler, float2((_36147 + 0.5f) * _8606_ramp_texel.x, _36105));
                    float2 _36185 = float2((floor((_36192.y * 255.0f) + 0.5f) * 256.0f) + floor((_36192.x * 255.0f) + 0.5f), (floor((_36192.w * 255.0f) + 0.5f) * 256.0f) + floor((_36192.z * 255.0f) + 0.5f)) * 1.525902189314365386962890625e-05f.xx;
                    float _35950 = _46596 - (_35260 ? _35919 : _35927);
                    float _35956 = _46597 - (_35260 ? _35921 : _35933);
                    float _35958 = _36134.x;
                    float _35960 = _36134.y;
                    float _35962 = lerp(_35958, _35960, _35950);
                    float _35964 = _36185.x;
                    float _35966 = _36185.y;
                    float _35968 = lerp(_35964, _35966, _35956);
                    float _35971 = _46597 - _46596;
                    float _35972 = max(_35971, 9.9999997473787516355514526367188e-06f);
                    float _46599;
                    if (_35971 <= 1.0f)
                    {
                        _46599 = (((_35260 ? _35921 : _35932) - _35919) < 0.5f) ? ((_35962 + _35968) * 0.5f) : ((((1.0f - _35950) * lerp(_35958, _35960, (1.0f + _35950) * 0.5f)) + (_35956 * lerp(_35964, _35966, _35956 * 0.5f))) / _35972);
                    }
                    else
                    {
                        float4 _36252 = RampTex.Sample(RampSampler, float2((_36096 + 1.5f) * _8606_ramp_texel.x, _36105));
                        float4 _36312 = RampTex.Sample(RampSampler, float2((_36147 + 1.5f) * _8606_ramp_texel.x, _36105));
                        float _36305 = (((floor((_36312.x * 255.0f) + 0.5f) + (floor((_36312.y * 255.0f) + 0.5f) * 256.0f)) + (floor((_36312.z * 255.0f) + 0.5f) * 65536.0f)) * 1.5258790881489403545856475830078e-05f) + ((_35964 + (((_35966 - _35964) * 0.5f) * _35956)) * _35956);
                        float _46598;
                        if (_35260)
                        {
                            float4 _36363 = RampTex.Sample(RampSampler, float2(510.5f * _8606_ramp_texel.x, _36105));
                            float2 _36356 = float2((floor((_36363.y * 255.0f) + 0.5f) * 256.0f) + floor((_36363.x * 255.0f) + 0.5f), (floor((_36363.w * 255.0f) + 0.5f) * 256.0f) + floor((_36363.z * 255.0f) + 0.5f)) * 1.525902189314365386962890625e-05f.xx;
                            float4 _36423 = RampTex.Sample(RampSampler, float2(511.5f * _8606_ramp_texel.x, _36105));
                            float _36404 = _36356.x;
                            _46598 = _36305 + ((floor(_46597 * 0.00390625f) - floor(_46596 * 0.00390625f)) * ((((floor((_36423.x * 255.0f) + 0.5f) + (floor((_36423.y * 255.0f) + 0.5f) * 256.0f)) + (floor((_36423.z * 255.0f) + 0.5f) * 65536.0f)) * 1.5258790881489403545856475830078e-05f) + (_36404 + ((_36356.y - _36404) * 0.5f))));
                        }
                        else
                        {
                            _46598 = _36305;
                        }
                        _46599 = (_46598 - ((((floor((_36252.x * 255.0f) + 0.5f) + (floor((_36252.y * 255.0f) + 0.5f) * 256.0f)) + (floor((_36252.z * 255.0f) + 0.5f) * 65536.0f)) * 1.5258790881489403545856475830078e-05f) + ((_35958 + (((_35960 - _35958) * 0.5f) * _35950)) * _35950))) / _35972;
                    }
                    float _36047 = _35968 - _35962;
                    float _36052 = abs(_36047);
                    float _36056 = (_35968 - _46599) / ((_36052 > 0.001000000047497451305389404296875f) ? _36047 : 1000000015047466219876688855040.0f);
                    _51035 = float4(_35962, _35968, (((_36056 < 0.0f) || (_36056 > 1.0f)) || (_36052 <= 0.001000000047497451305389404296875f)) ? (-1.0f) : _36056, _46599);
                    _46609 = _46568;
                    _46602 = ((float3(_46436, _46450, _46466) * 4.0f) + float3(_46550, _46554, _46558)) * 0.015625f.xxx;
                }
                else
                {
                    float _36431 = floor(_46388 * 0.03125f);
                    float _36436 = _46388 - (_36431 * 32.0f);
                    float _46506;
                    float _46508;
                    if (_36436 >= 32.0f)
                    {
                        _46508 = _36436 - 32.0f;
                        _46506 = _36431 + 1.0f;
                    }
                    else
                    {
                        float _46507;
                        float _46509;
                        if (_36436 < 0.0f)
                        {
                            _46509 = _36436 + 32.0f;
                            _46507 = _36431 - 1.0f;
                        }
                        else
                        {
                            _46509 = _36436;
                            _46507 = _36431;
                        }
                        _46508 = _46509;
                        _46506 = _46507;
                    }
                    float _36466 = floor(_46390 * 0.03125f);
                    float _36471 = _46390 - (_36466 * 32.0f);
                    float _46524;
                    float _46526;
                    if (_36471 >= 32.0f)
                    {
                        _46526 = _36471 - 32.0f;
                        _46524 = _36466 + 1.0f;
                    }
                    else
                    {
                        float _46525;
                        float _46527;
                        if (_36471 < 0.0f)
                        {
                            _46527 = _36471 + 32.0f;
                            _46525 = _36466 - 1.0f;
                        }
                        else
                        {
                            _46527 = _36471;
                            _46525 = _36466;
                        }
                        _46526 = _46527;
                        _46524 = _46525;
                    }
                    _51035 = float4(_46312, _46312, -1.0f, _46312);
                    _46609 = _46524;
                    _46602 = ((float3(_46436, _46450, _46466) * 32.0f) + float3(_46508, _46506, _46526)) * 0.001953125f.xxx;
                }
                float3 _35288 = float3(_46400, _46412, _46424);
                float3 _35292 = float3(_46438, _46452, _46468);
                float3 _46607;
                if (_51035.z >= 0.0f)
                {
                    _46607 = lerp(clamp((_35288 + (_35292 * cos(((_35225 * _51035.y) + _46602) * 6.283185482025146484375f))) * 0.0078740157186985015869140625f.xxx, 0.0f.xxx, 1.0f.xxx), clamp((_35288 + (_35292 * cos(((_35225 * _51035.x) + _46602) * 6.283185482025146484375f))) * 0.0078740157186985015869140625f.xxx, 0.0f.xxx, 1.0f.xxx), _51035.z.xxx);
                }
                else
                {
                    _46607 = clamp((_35288 + (_35292 * cos(((_35225 * _51035.w) + _46602) * 6.283185482025146484375f))) * 0.0078740157186985015869140625f.xxx, 0.0f.xxx, 1.0f.xxx);
                }
                _46616 = _42707;
                _46614 = float4(_46607, _46609 * 0.01587301678955554962158203125f);
            }
            else
            {
                float4 _46615;
                float _46655;
                if (_14035)
                {
                    float _46365;
                    float _46367;
                    if (_33943)
                    {
                        _46367 = _33940 - 2048.0f;
                        _46365 = _33935 + 1.0f;
                    }
                    else
                    {
                        float _46366;
                        float _46368;
                        if (_33940 < 0.0f)
                        {
                            _46368 = _33940 + 2048.0f;
                            _46366 = _33935 - 1.0f;
                        }
                        else
                        {
                            _46368 = _33940;
                            _46366 = _33935;
                        }
                        _46367 = _46368;
                        _46365 = _46366;
                    }
                    bool _14377 = _46315 > 0.5f;
                    float _36591 = 0.5f * clamp(_46313 * 256.0f, 9.9999997473787516355514526367188e-06f, 256.0f);
                    float _36593 = _46312 * 256.0f;
                    float _36595 = _36593 - _36591;
                    float _36599 = _36593 + _36591;
                    float _46371;
                    float _46372;
                    if (!_14377)
                    {
                        _46372 = clamp(_36599, 0.0f, 256.0f);
                        _46371 = clamp(_36595, 0.0f, 256.0f);
                    }
                    else
                    {
                        _46372 = _36599;
                        _46371 = _36595;
                    }
                    float _36609 = floor(_46371);
                    float _36611 = floor(_46372);
                    float _36617 = _14377 ? mod(mod(_36609, 256.0f) + 256.0f, 256.0f) : min(_36609, 255.0f);
                    float _36622 = min(_36611, 255.0f);
                    float _36623 = _14377 ? mod(mod(_36611, 256.0f) + 256.0f, 256.0f) : _36622;
                    float _36634 = _46371 - (_14377 ? _36609 : _36617);
                    float _36640 = _46372 - (_14377 ? _36611 : _36623);
                    float _36763 = _36617 * 2.0f;
                    float _36769 = (_36763 + 0.5f) * _8606_ramp_texel.x;
                    float _36774 = (_46365 + 0.5f) * _8606_ramp_texel.y;
                    float4 _36783 = RampTex.Sample(RampSampler, float2(_36769, _36774));
                    float _36794 = (_36763 + 1.5f) * _8606_ramp_texel.x;
                    float4 _36808 = RampTex.Sample(RampSampler, float2(_36794, _36774));
                    float _36813 = _36623 * 2.0f;
                    float _36819 = (_36813 + 0.5f) * _8606_ramp_texel.x;
                    float4 _36833 = RampTex.Sample(RampSampler, float2(_36819, _36774));
                    float _36844 = (_36813 + 1.5f) * _8606_ramp_texel.x;
                    float4 _36858 = RampTex.Sample(RampSampler, float2(_36844, _36774));
                    float _36656 = max(_46372 - _46371, 9.9999997473787516355514526367188e-06f);
                    float _36659 = (_14377 ? _36611 : _36622) - _36609;
                    float4 _46374;
                    if (_36659 < 0.5f)
                    {
                        _46374 = lerp(_36783, _36808, ((_36634 + _36640) * 0.5f).xxxx);
                    }
                    else
                    {
                        float4 _46375;
                        if (_36659 < 1.5f)
                        {
                            _46375 = ((lerp(_36783, _36808, ((1.0f + _36634) * 0.5f).xxxx) * (1.0f - _36634)) + (lerp(_36833, _36858, (_36640 * 0.5f).xxxx) * _36640)) / _36656.xxxx;
                        }
                        else
                        {
                            float _36894 = (_46367 + 0.5f) * _8606_ramp_texel.y;
                            float4 _36967 = RampTex.Sample(RampSampler, float2(_36769, _36894));
                            float4 _36974 = RampTex.Sample(RampSampler, float2(_36794, _36894));
                            float4 _37083 = RampTex.Sample(RampSampler, float2(_36819, _36894));
                            float4 _37090 = RampTex.Sample(RampSampler, float2(_36844, _36894));
                            float4 _36993 = ((float4((floor((_37083.y * 255.0f) + 0.5f) * 256.0f) + floor((_37083.x * 255.0f) + 0.5f), (floor((_37083.w * 255.0f) + 0.5f) * 256.0f) + floor((_37083.z * 255.0f) + 0.5f), (floor((_37090.y * 255.0f) + 0.5f) * 256.0f) + floor((_37090.x * 255.0f) + 0.5f), (floor((_37090.w * 255.0f) + 0.5f) * 256.0f) + floor((_37090.z * 255.0f) + 0.5f)) * 1.525902189314365386962890625e-05f.xxxx) * 256.0f) + ((_36833 + (((_36858 - _36833) * 0.5f) * _36640)) * _36640);
                            float4 _46373;
                            if (_14377)
                            {
                                float _37101 = 510.5f * _8606_ramp_texel.x;
                                float4 _37115 = RampTex.Sample(RampSampler, float2(_37101, _36774));
                                float _37126 = 511.5f * _8606_ramp_texel.x;
                                float4 _37249 = RampTex.Sample(RampSampler, float2(_37101, _36894));
                                float4 _37256 = RampTex.Sample(RampSampler, float2(_37126, _36894));
                                _46373 = _36993 + ((((float4((floor((_37249.y * 255.0f) + 0.5f) * 256.0f) + floor((_37249.x * 255.0f) + 0.5f), (floor((_37249.w * 255.0f) + 0.5f) * 256.0f) + floor((_37249.z * 255.0f) + 0.5f), (floor((_37256.y * 255.0f) + 0.5f) * 256.0f) + floor((_37256.x * 255.0f) + 0.5f), (floor((_37256.w * 255.0f) + 0.5f) * 256.0f) + floor((_37256.z * 255.0f) + 0.5f)) * 1.525902189314365386962890625e-05f.xxxx) * 256.0f) + ((_37115 + (((RampTex.Sample(RampSampler, float2(_37126, _36774)) - _37115) * 0.5f) * 1.0f)) * 1.0f)) * (floor(_46372 * 0.00390625f) - floor(_46371 * 0.00390625f)));
                            }
                            else
                            {
                                _46373 = _36993;
                            }
                            _46375 = (_46373 - (((float4((floor((_36967.y * 255.0f) + 0.5f) * 256.0f) + floor((_36967.x * 255.0f) + 0.5f), (floor((_36967.w * 255.0f) + 0.5f) * 256.0f) + floor((_36967.z * 255.0f) + 0.5f), (floor((_36974.y * 255.0f) + 0.5f) * 256.0f) + floor((_36974.x * 255.0f) + 0.5f), (floor((_36974.w * 255.0f) + 0.5f) * 256.0f) + floor((_36974.z * 255.0f) + 0.5f)) * 1.525902189314365386962890625e-05f.xxxx) * 256.0f) + ((_36783 + (((_36808 - _36783) * 0.5f) * _36634)) * _36634))) / _36656.xxxx;
                        }
                        _46374 = _46375;
                    }
                    _46655 = max(_42707, 1.0f);
                    _46615 = _46374;
                }
                else
                {
                    float _46364;
                    float4 _51026;
                    float4 _51030;
                    if (_14016)
                    {
                        float _46334;
                        if (_33880)
                        {
                            _46334 = _33877 - 2048.0f;
                        }
                        else
                        {
                            float _46335;
                            if (_33877 < 0.0f)
                            {
                                _46335 = _33877 + 2048.0f;
                            }
                            else
                            {
                                _46335 = _33877;
                            }
                            _46334 = _46335;
                        }
                        float _37342 = floor(_46334 * 0.00390625f);
                        float _37347 = _46334 - (_37342 * 256.0f);
                        float _46336;
                        float _46338;
                        if (_37347 >= 256.0f)
                        {
                            _46338 = _37347 - 256.0f;
                            _46336 = _37342 + 1.0f;
                        }
                        else
                        {
                            float _46337;
                            float _46339;
                            if (_37347 < 0.0f)
                            {
                                _46339 = _37347 + 256.0f;
                                _46337 = _37342 - 1.0f;
                            }
                            else
                            {
                                _46339 = _37347;
                                _46337 = _37342;
                            }
                            _46338 = _46339;
                            _46336 = _46337;
                        }
                        float4 _49431 = _33803;
                        _49431.w = _46338 * 0.0039215688593685626983642578125f;
                        float _46340;
                        if (_33991)
                        {
                            _46340 = _33988 - 2048.0f;
                        }
                        else
                        {
                            float _46341;
                            if (_33988 < 0.0f)
                            {
                                _46341 = _33988 + 2048.0f;
                            }
                            else
                            {
                                _46341 = _33988;
                            }
                            _46340 = _46341;
                        }
                        float _37412 = floor(_46340 * 0.00390625f);
                        float _37417 = _46340 - (_37412 * 256.0f);
                        float _46342;
                        float _46344;
                        if (_37417 >= 256.0f)
                        {
                            _46344 = _37417 - 256.0f;
                            _46342 = _37412 + 1.0f;
                        }
                        else
                        {
                            float _46343;
                            float _46345;
                            if (_37417 < 0.0f)
                            {
                                _46345 = _37417 + 256.0f;
                                _46343 = _37412 - 1.0f;
                            }
                            else
                            {
                                _46345 = _37417;
                                _46343 = _37412;
                            }
                            _46344 = _46345;
                            _46342 = _46343;
                        }
                        float4 _49434 = _33914;
                        _49434.w = _46344 * 0.0039215688593685626983642578125f;
                        bool _14397 = _46315 > 0.5f;
                        float _37488 = 0.5f * clamp(_46313 * 256.0f, 9.9999997473787516355514526367188e-06f, 256.0f);
                        float _37490 = _46312 * 256.0f;
                        float _37492 = _37490 - _37488;
                        float _37496 = _37490 + _37488;
                        float _46358;
                        float _46359;
                        if (!_14397)
                        {
                            _46359 = clamp(_37496, 0.0f, 256.0f);
                            _46358 = clamp(_37492, 0.0f, 256.0f);
                        }
                        else
                        {
                            _46359 = _37496;
                            _46358 = _37492;
                        }
                        float _37506 = floor(_46358);
                        float _37508 = floor(_46359);
                        float _37514 = _14397 ? mod(mod(_37506, 256.0f) + 256.0f, 256.0f) : min(_37506, 255.0f);
                        float _37519 = min(_37508, 255.0f);
                        float _37520 = _14397 ? mod(mod(_37508, 256.0f) + 256.0f, 256.0f) : _37519;
                        float _37683 = _37514 * 2.0f;
                        float _37692 = ((((_46336 + (8.0f * _46342)) + (_14019 ? 64.0f : 0.0f)) + (_14022 ? 128.0f : 0.0f)) + 0.5f) * _8606_ramp_texel.y;
                        float4 _37728 = RampTex.Sample(RampSampler, float2((_37683 + 0.5f) * _8606_ramp_texel.x, _37692));
                        float2 _37721 = float2((floor((_37728.y * 255.0f) + 0.5f) * 256.0f) + floor((_37728.x * 255.0f) + 0.5f), (floor((_37728.w * 255.0f) + 0.5f) * 256.0f) + floor((_37728.z * 255.0f) + 0.5f)) * 1.525902189314365386962890625e-05f.xx;
                        float _37734 = _37520 * 2.0f;
                        float4 _37779 = RampTex.Sample(RampSampler, float2((_37734 + 0.5f) * _8606_ramp_texel.x, _37692));
                        float2 _37772 = float2((floor((_37779.y * 255.0f) + 0.5f) * 256.0f) + floor((_37779.x * 255.0f) + 0.5f), (floor((_37779.w * 255.0f) + 0.5f) * 256.0f) + floor((_37779.z * 255.0f) + 0.5f)) * 1.525902189314365386962890625e-05f.xx;
                        float _37537 = _46358 - (_14397 ? _37506 : _37514);
                        float _37543 = _46359 - (_14397 ? _37508 : _37520);
                        float _37545 = _37721.x;
                        float _37547 = _37721.y;
                        float _37551 = _37772.x;
                        float _37553 = _37772.y;
                        float _37558 = _46359 - _46358;
                        float _37559 = max(_37558, 9.9999997473787516355514526367188e-06f);
                        float _46361;
                        if (_37558 <= 1.0f)
                        {
                            _46361 = (((_14397 ? _37508 : _37519) - _37506) < 0.5f) ? ((lerp(_37545, _37547, _37537) + lerp(_37551, _37553, _37543)) * 0.5f) : ((((1.0f - _37537) * lerp(_37545, _37547, (1.0f + _37537) * 0.5f)) + (_37543 * lerp(_37551, _37553, _37543 * 0.5f))) / _37559);
                        }
                        else
                        {
                            float4 _37839 = RampTex.Sample(RampSampler, float2((_37683 + 1.5f) * _8606_ramp_texel.x, _37692));
                            float4 _37899 = RampTex.Sample(RampSampler, float2((_37734 + 1.5f) * _8606_ramp_texel.x, _37692));
                            float _37892 = (((floor((_37899.x * 255.0f) + 0.5f) + (floor((_37899.y * 255.0f) + 0.5f) * 256.0f)) + (floor((_37899.z * 255.0f) + 0.5f) * 65536.0f)) * 1.5258790881489403545856475830078e-05f) + ((_37551 + (((_37553 - _37551) * 0.5f) * _37543)) * _37543);
                            float _46360;
                            if (_14397)
                            {
                                float4 _37950 = RampTex.Sample(RampSampler, float2(510.5f * _8606_ramp_texel.x, _37692));
                                float2 _37943 = float2((floor((_37950.y * 255.0f) + 0.5f) * 256.0f) + floor((_37950.x * 255.0f) + 0.5f), (floor((_37950.w * 255.0f) + 0.5f) * 256.0f) + floor((_37950.z * 255.0f) + 0.5f)) * 1.525902189314365386962890625e-05f.xx;
                                float4 _38010 = RampTex.Sample(RampSampler, float2(511.5f * _8606_ramp_texel.x, _37692));
                                float _37991 = _37943.x;
                                _46360 = _37892 + ((floor(_46359 * 0.00390625f) - floor(_46358 * 0.00390625f)) * ((((floor((_38010.x * 255.0f) + 0.5f) + (floor((_38010.y * 255.0f) + 0.5f) * 256.0f)) + (floor((_38010.z * 255.0f) + 0.5f) * 65536.0f)) * 1.5258790881489403545856475830078e-05f) + (_37991 + ((_37943.y - _37991) * 0.5f))));
                            }
                            else
                            {
                                _46360 = _37892;
                            }
                            _46361 = (_46360 - ((((floor((_37839.x * 255.0f) + 0.5f) + (floor((_37839.y * 255.0f) + 0.5f) * 256.0f)) + (floor((_37839.z * 255.0f) + 0.5f) * 65536.0f)) * 1.5258790881489403545856475830078e-05f) + ((_37545 + (((_37547 - _37545) * 0.5f) * _37537)) * _37537))) / _37559;
                        }
                        _51030 = _49434;
                        _51026 = _49431;
                        _46364 = _46361;
                    }
                    else
                    {
                        _51030 = _33914;
                        _51026 = _33803;
                        _46364 = _46312;
                    }
                    float _38024 = lerp(_51026.w, _51030.w, _46364);
                    float _38034 = (_38024 > 0.0f) ? ((_46364 * _51030.w) / _38024) : _46364;
                    float4 _38052 = lerp(_51026, _51030, _38034.xxxx);
                    float4 _51034;
                    if (_42707 < 0.5f)
                    {
                        float4 _49479 = _38052;
                        _49479.z = frac(_51026.z + ((frac((_51030.z - _51026.z) + 0.5f) - 0.5f) * _38034));
                        _51034 = _49479;
                    }
                    else
                    {
                        _51034 = _38052;
                    }
                    float4 _49481 = _51034;
                    _49481.w = _38024;
                    _46655 = _42707;
                    _46615 = _49481;
                }
                _46616 = _46655;
                _46614 = _46615;
            }
            float4 _46670;
            if (_46616 < 0.5f)
            {
                float _38093 = _46614.y * 0.4000000059604644775390625f;
                float _38097 = (_46614.z * 6.283185482025146484375f) - 3.1415927410125732421875f;
                float _38103 = _38093 * cos(_38097);
                float _38107 = _38093 * sin(_38097);
                float _38156 = (_46614.x + (0.3963377773761749267578125f * _38103)) + (0.21580375730991363525390625f * _38107);
                float _38166 = (_46614.x - (0.1055613458156585693359375f * _38103)) - (0.06385417282581329345703125f * _38107);
                float _38176 = (_46614.x - (0.089484177529811859130859375f * _38103)) - (1.2914855480194091796875f * _38107);
                float _38181 = (_38156 * _38156) * _38156;
                float _38186 = (_38166 * _38166) * _38166;
                float _38191 = (_38176 * _38176) * _38176;
                float _38199 = ((4.076741695404052734375f * _38181) - (3.30771160125732421875f * _38186)) + (0.2309699356555938720703125f * _38191);
                float _38207 = (((-1.26843798160552978515625f) * _38181) + (2.60975742340087890625f * _38186)) - (0.341319382190704345703125f * _38191);
                float _38215 = (((-0.0041960864327847957611083984375f) * _38181) - (0.70341861248016357421875f * _38186)) + (1.7076146602630615234375f * _38191);
                _46670 = float4((_38199 >= 0.003130800090730190277099609375f) ? ((pow(abs(_38199), 0.4166666567325592041015625f) * 1.05499994754791259765625f) - 0.054999999701976776123046875f) : (12.9200000762939453125f * _38199), (_38207 >= 0.003130800090730190277099609375f) ? ((pow(abs(_38207), 0.4166666567325592041015625f) * 1.05499994754791259765625f) - 0.054999999701976776123046875f) : (12.9200000762939453125f * _38207), (_38215 >= 0.003130800090730190277099609375f) ? ((pow(abs(_38215), 0.4166666567325592041015625f) * 1.05499994754791259765625f) - 0.054999999701976776123046875f) : (12.9200000762939453125f * _38215), _46614.w);
            }
            else
            {
                float4 _46671;
                if (_46616 < 1.5f)
                {
                    float _38121 = (_46614.y * 0.800000011920928955078125f) - 0.4000000059604644775390625f;
                    float _38125 = (_46614.z * 0.800000011920928955078125f) - 0.4000000059604644775390625f;
                    float _38284 = (_46614.x + (0.3963377773761749267578125f * _38121)) + (0.21580375730991363525390625f * _38125);
                    float _38294 = (_46614.x - (0.1055613458156585693359375f * _38121)) - (0.06385417282581329345703125f * _38125);
                    float _38304 = (_46614.x - (0.089484177529811859130859375f * _38121)) - (1.2914855480194091796875f * _38125);
                    float _38309 = (_38284 * _38284) * _38284;
                    float _38314 = (_38294 * _38294) * _38294;
                    float _38319 = (_38304 * _38304) * _38304;
                    float _38327 = ((4.076741695404052734375f * _38309) - (3.30771160125732421875f * _38314)) + (0.2309699356555938720703125f * _38319);
                    float _38335 = (((-1.26843798160552978515625f) * _38309) + (2.60975742340087890625f * _38314)) - (0.341319382190704345703125f * _38319);
                    float _38343 = (((-0.0041960864327847957611083984375f) * _38309) - (0.70341861248016357421875f * _38314)) + (1.7076146602630615234375f * _38319);
                    _46671 = float4((_38327 >= 0.003130800090730190277099609375f) ? ((pow(abs(_38327), 0.4166666567325592041015625f) * 1.05499994754791259765625f) - 0.054999999701976776123046875f) : (12.9200000762939453125f * _38327), (_38335 >= 0.003130800090730190277099609375f) ? ((pow(abs(_38335), 0.4166666567325592041015625f) * 1.05499994754791259765625f) - 0.054999999701976776123046875f) : (12.9200000762939453125f * _38335), (_38343 >= 0.003130800090730190277099609375f) ? ((pow(abs(_38343), 0.4166666567325592041015625f) * 1.05499994754791259765625f) - 0.054999999701976776123046875f) : (12.9200000762939453125f * _38343), _46614.w);
                }
                else
                {
                    _46671 = _46614;
                }
                _46670 = _46671;
            }
            float3 _14418 = _46670.xyz * _46670.w;
            float4 _49512 = _46670;
            _49512.x = _14418.x;
            _49512.y = _14418.y;
            _49512.z = _14418.z;
            _51234 = _49512;
        }
        else
        {
            _51234 = 0.0f.xxxx;
        }
        float4 _51233;
        if (_14330)
        {
            bool _14432 = p_Border.x < (-0.5f);
            bool _14435 = _14025 || _14039;
            float _47061;
            float _47062;
            float _47064;
            if (_42699 < 0.5f)
            {
                _47064 = 0.0f;
                _47062 = 0.0f;
                _47061 = 1.0f;
            }
            else
            {
                float2 _38445 = p_BorderCoord.zw - p_BorderCoord.xy;
                float _38446 = length(_38445);
                bool _38452 = _42699 < 9.5f;
                float2 _46754;
                do
                {
                    float _38736 = p_BorderCoord.x - p_Pos.x;
                    float _38741 = p_Pos.y - p_BorderCoord.y;
                    if (_38446 == 0.0f)
                    {
                        _46754 = p_Pos.xy;
                        break;
                    }
                    float _38749 = (p_BorderCoord.z - p_BorderCoord.x) / _38446;
                    float _38752 = (p_BorderCoord.w - p_BorderCoord.y) / _38446;
                    _46754 = float2((_38749 * _38736) - (_38752 * _38741), (_38752 * _38736) + (_38749 * _38741));
                    break;
                } while(false);
                bool2 _38462 = ((_42699 >= 3.5f) && _38452).xx;
                float2 _38463 = float2(_38462.x ? _46754.x : p_Pos.xy.x, _38462.y ? _46754.y : p_Pos.xy.y);
                float _46755;
                float _47038;
                float _47067;
                if (_42699 < 1.5f)
                {
                    _47067 = 0.0f;
                    _47038 = _14001 / _38446;
                    _46755 = length(p_Pos.xy - p_BorderCoord.xy) / _38446;
                }
                else
                {
                    float _46756;
                    float _47039;
                    float _47069;
                    if (_42699 < 2.5f)
                    {
                        _47069 = 0.0f;
                        _47039 = _14001 / _38446;
                        _46756 = dot(_38445, p_Pos.xy - p_BorderCoord.xy) / (_38446 * _38446);
                    }
                    else
                    {
                        float _46757;
                        float _47040;
                        float _47070;
                        if (_42699 < 3.5f)
                        {
                            _47070 = 0.0f;
                            _47040 = _14001 / _38446;
                            _46757 = abs(dot(_38445, p_Pos.xy - p_BorderCoord.xy)) / (_38446 * _38446);
                        }
                        else
                        {
                            float _46758;
                            float _47041;
                            float _47071;
                            if (_42699 < 4.5f)
                            {
                                _47071 = 0.0f;
                                _47041 = _14001 / (3.1415927410125732421875f * max(length(p_BorderCoord.xy - p_Pos.xy), 9.9999999747524270787835121154785e-07f));
                                _46758 = abs(atan2(-_38463.y, -_38463.x) * 0.3183098733425140380859375f);
                            }
                            else
                            {
                                float _46759;
                                float _47042;
                                float _47072;
                                if (_42699 < 5.5f)
                                {
                                    float _38826 = (atan2(_38463.y, _38463.x) * 0.15915493667125701904296875f) + 0.5f;
                                    float _38534 = length(p_BorderCoord.xy - p_Pos.xy);
                                    float _46760;
                                    if ((!_14432) && (!_14435))
                                    {
                                        float _38836 = max(0.5f * clamp(_14001 / (6.283185482025146484375f * _38534), 0.0f, 1.0f), 9.9999999747524270787835121154785e-07f);
                                        float _38838 = frac(_38826);
                                        _46760 = lerp(_38838, 0.5f, max(clamp((_38838 - (1.0f - _38836)) / _38836, 0.0f, 1.0f), clamp((_38836 - _38838) / _38836, 0.0f, 1.0f)));
                                    }
                                    else
                                    {
                                        _46760 = _38826;
                                    }
                                    _47072 = 1.0f;
                                    _47042 = _14001 / (6.283185482025146484375f * max(_38534, 9.9999999747524270787835121154785e-07f));
                                    _46759 = _46760;
                                }
                                else
                                {
                                    float _46761;
                                    float _47044;
                                    float _47074;
                                    if (_42699 < 6.5f)
                                    {
                                        _47074 = 0.0f;
                                        _47044 = _14001 / _38446;
                                        _46761 = max(abs(_38463.x), abs(_38463.y)) / _38446;
                                    }
                                    else
                                    {
                                        float _46762;
                                        float _47045;
                                        float _47075;
                                        if (_42699 < 7.5f)
                                        {
                                            _47075 = 0.0f;
                                            _47045 = _14001 / _38446;
                                            _46762 = min(abs(_38463.x), abs(_38463.y)) / _38446;
                                        }
                                        else
                                        {
                                            float _46763;
                                            float _47046;
                                            if (_38452)
                                            {
                                                float _38901 = frac(((((_42699 < 8.5f) ? 1.0f : (-1.0f)) * atan2(-_38463.y, -_38463.x)) * 0.15915493667125701904296875f) + (length(_38463) / _38446));
                                                float _38909 = 6.283185482025146484375f * length(p_Pos.xy - p_BorderCoord.xy);
                                                float _38595 = _14001 * sqrt((1.0f / (_38446 * _38446)) + (1.0f / max(_38909 * _38909, 9.9999999600419720025001879548654e-13f)));
                                                float _46764;
                                                if ((!_14432) && (!_14435))
                                                {
                                                    float _38930 = max(0.5f * clamp(_38595, 0.0f, 1.0f), 9.9999999747524270787835121154785e-07f);
                                                    float _38932 = frac(_38901);
                                                    _46764 = lerp(_38932, 0.5f, max(clamp((_38932 - (1.0f - _38930)) / _38930, 0.0f, 1.0f), clamp((_38930 - _38932) / _38930, 0.0f, 1.0f)));
                                                }
                                                else
                                                {
                                                    _46764 = _38901;
                                                }
                                                _47046 = _38595;
                                                _46763 = _46764;
                                            }
                                            else
                                            {
                                                float _46765;
                                                float _47048;
                                                if (_42699 < 10.5f)
                                                {
                                                    float _38958 = p_BorderCoord.y - p_BorderCoord.x;
                                                    _47048 = _14001 / max(abs(_38958), 9.9999999747524270787835121154785e-07f);
                                                    _46765 = (_44430 - p_BorderCoord.x) / _38958;
                                                }
                                                else
                                                {
                                                    _47048 = 0.0f;
                                                    _46765 = _46115;
                                                }
                                                _47046 = _47048;
                                                _46763 = _46765;
                                            }
                                            _47075 = float(_38452);
                                            _47045 = _47046;
                                            _46762 = _46763;
                                        }
                                        _47074 = _47075;
                                        _47044 = _47045;
                                        _46761 = _46762;
                                    }
                                    _47072 = _47074;
                                    _47042 = _47044;
                                    _46759 = _46761;
                                }
                                _47071 = _47072;
                                _47041 = _47042;
                                _46758 = _46759;
                            }
                            _47070 = _47071;
                            _47040 = _47041;
                            _46757 = _46758;
                        }
                        _47069 = _47070;
                        _47039 = _47040;
                        _46756 = _46757;
                    }
                    _47067 = _47069;
                    _47038 = _47039;
                    _46755 = _46756;
                }
                float _47050;
                float _47055;
                float _47066;
                if (_42703 < 0.5f)
                {
                    _47066 = _47067;
                    _47055 = _47038;
                    _47050 = _46755;
                }
                else
                {
                    float _47051;
                    float _47056;
                    float _47079;
                    if (_42703 < 1.5f)
                    {
                        float _38963 = frac(_46755);
                        float _47052;
                        if ((!_14432) && (!_14435))
                        {
                            float _38973 = max(0.5f * clamp(_14001 / _38446, 0.0f, 1.0f), 9.9999999747524270787835121154785e-07f);
                            float _38975 = frac(_38963);
                            _47052 = lerp(_38975, 0.5f, max(clamp((_38975 - (1.0f - _38973)) / _38973, 0.0f, 1.0f), clamp((_38973 - _38975) / _38973, 0.0f, 1.0f)));
                        }
                        else
                        {
                            _47052 = _38963;
                        }
                        _47079 = 1.0f;
                        _47056 = _47038;
                        _47051 = _47052;
                    }
                    else
                    {
                        float _47053;
                        float _47058;
                        float _47081;
                        if (_42703 < 2.5f)
                        {
                            _47081 = 0.0f;
                            _47058 = _47038;
                            _47053 = abs(mod(mod(_46755 + 1.0f, 2.0f) + 2.0f, 2.0f) - 1.0f);
                        }
                        else
                        {
                            bool _38668 = _42703 < 3.5f;
                            float _47054;
                            float _47059;
                            if (_38668)
                            {
                                float _38672 = (_46755 * 3.1415927410125732421875f) - 1.5f;
                                _47059 = _47038 * (abs(cos(_38672)) * 1.57079637050628662109375f);
                                _47054 = (sin(_38672) * 0.5f) + 0.5f;
                            }
                            else
                            {
                                _47059 = _47038;
                                _47054 = _46755;
                            }
                            _47081 = _38668 ? 0.0f : _47067;
                            _47058 = _47059;
                            _47053 = _47054;
                        }
                        _47079 = _47081;
                        _47056 = _47058;
                        _47051 = _47053;
                    }
                    _47066 = _47079;
                    _47055 = _47056;
                    _47050 = _47051;
                }
                float _39031 = (1.0f - p_Meta3.w) - p_Meta3.z;
                _47064 = ((p_Meta3.z != 0.0f) || (p_Meta3.w != 0.0f)) ? 0.0f : _47066;
                _47062 = _47055 / max(abs(_39031), 9.9999999747524270787835121154785e-07f);
                _47061 = clamp((_47050 - p_Meta3.z) / _39031, 0.0f, 1.0f);
            }
            float4 _47401;
            float _47403;
            if (_14432)
            {
                float _39103 = (-1.0f) - p_Border.x;
                float _39292 = floor(_39103 * 0.00048828125f);
                float _39297 = _39103 - (_39292 * 2048.0f);
                float _47163;
                float _47165;
                if (_39297 >= 2048.0f)
                {
                    _47165 = _39297 - 2048.0f;
                    _47163 = _39292 + 1.0f;
                }
                else
                {
                    float _47164;
                    float _47166;
                    if (_39297 < 0.0f)
                    {
                        _47166 = _39297 + 2048.0f;
                        _47164 = _39292 - 1.0f;
                    }
                    else
                    {
                        _47166 = _39297;
                        _47164 = _39292;
                    }
                    _47165 = _47166;
                    _47163 = _47164;
                }
                float _39327 = floor(_14076 * 0.00048828125f);
                float _39332 = _14076 - (_39327 * 2048.0f);
                float _47167;
                float _47169;
                if (_39332 >= 2048.0f)
                {
                    _47169 = _39332 - 2048.0f;
                    _47167 = _39327 + 1.0f;
                }
                else
                {
                    float _47168;
                    float _47170;
                    if (_39332 < 0.0f)
                    {
                        _47170 = _39332 + 2048.0f;
                        _47168 = _39327 - 1.0f;
                    }
                    else
                    {
                        _47170 = _39332;
                        _47168 = _39327;
                    }
                    _47169 = _47170;
                    _47167 = _47168;
                }
                float _39362 = floor(_14084 * 0.00048828125f);
                float _39367 = _14084 - (_39362 * 2048.0f);
                float _47171;
                float _47173;
                if (_39367 >= 2048.0f)
                {
                    _47173 = _39367 - 2048.0f;
                    _47171 = _39362 + 1.0f;
                }
                else
                {
                    float _47172;
                    float _47174;
                    if (_39367 < 0.0f)
                    {
                        _47174 = _39367 + 2048.0f;
                        _47172 = _39362 - 1.0f;
                    }
                    else
                    {
                        _47174 = _39367;
                        _47172 = _39362;
                    }
                    _47173 = _47174;
                    _47171 = _47172;
                }
                float _39397 = floor(_14092 * 0.00048828125f);
                float _39402 = _14092 - (_39397 * 2048.0f);
                float _47175;
                float _47177;
                if (_39402 >= 2048.0f)
                {
                    _47177 = _39402 - 2048.0f;
                    _47175 = _39397 + 1.0f;
                }
                else
                {
                    float _47176;
                    float _47178;
                    if (_39402 < 0.0f)
                    {
                        _47178 = _39402 + 2048.0f;
                        _47176 = _39397 - 1.0f;
                    }
                    else
                    {
                        _47178 = _39402;
                        _47176 = _39397;
                    }
                    _47177 = _47178;
                    _47175 = _47176;
                }
                float _39432 = floor(_47163 * 0.0078125f);
                float _39437 = _47163 - (_39432 * 128.0f);
                float _47185;
                float _47187;
                if (_39437 >= 128.0f)
                {
                    _47187 = _39437 - 128.0f;
                    _47185 = _39432 + 1.0f;
                }
                else
                {
                    float _47186;
                    float _47188;
                    if (_39437 < 0.0f)
                    {
                        _47188 = _39437 + 128.0f;
                        _47186 = _39432 - 1.0f;
                    }
                    else
                    {
                        _47188 = _39437;
                        _47186 = _39432;
                    }
                    _47187 = _47188;
                    _47185 = _47186;
                }
                float _39467 = floor(_47165 * 0.0078125f);
                float _39472 = _47165 - (_39467 * 128.0f);
                float _47197;
                float _47199;
                if (_39472 >= 128.0f)
                {
                    _47199 = _39472 - 128.0f;
                    _47197 = _39467 + 1.0f;
                }
                else
                {
                    float _47198;
                    float _47200;
                    if (_39472 < 0.0f)
                    {
                        _47200 = _39472 + 128.0f;
                        _47198 = _39467 - 1.0f;
                    }
                    else
                    {
                        _47200 = _39472;
                        _47198 = _39467;
                    }
                    _47199 = _47200;
                    _47197 = _47198;
                }
                float _39502 = floor(_47167 * 0.0078125f);
                float _39507 = _47167 - (_39502 * 128.0f);
                float _47209;
                float _47211;
                if (_39507 >= 128.0f)
                {
                    _47211 = _39507 - 128.0f;
                    _47209 = _39502 + 1.0f;
                }
                else
                {
                    float _47210;
                    float _47212;
                    if (_39507 < 0.0f)
                    {
                        _47212 = _39507 + 128.0f;
                        _47210 = _39502 - 1.0f;
                    }
                    else
                    {
                        _47212 = _39507;
                        _47210 = _39502;
                    }
                    _47211 = _47212;
                    _47209 = _47210;
                }
                float _39537 = floor(_47169 * 0.0078125f);
                float _39542 = _47169 - (_39537 * 128.0f);
                float _47223;
                float _47225;
                if (_39542 >= 128.0f)
                {
                    _47225 = _39542 - 128.0f;
                    _47223 = _39537 + 1.0f;
                }
                else
                {
                    float _47224;
                    float _47226;
                    if (_39542 < 0.0f)
                    {
                        _47226 = _39542 + 128.0f;
                        _47224 = _39537 - 1.0f;
                    }
                    else
                    {
                        _47226 = _39542;
                        _47224 = _39537;
                    }
                    _47225 = _47226;
                    _47223 = _47224;
                }
                float _39572 = floor(_47171 * 0.0078125f);
                float _39577 = _47171 - (_39572 * 128.0f);
                float _47237;
                float _47239;
                if (_39577 >= 128.0f)
                {
                    _47239 = _39577 - 128.0f;
                    _47237 = _39572 + 1.0f;
                }
                else
                {
                    float _47238;
                    float _47240;
                    if (_39577 < 0.0f)
                    {
                        _47240 = _39577 + 128.0f;
                        _47238 = _39572 - 1.0f;
                    }
                    else
                    {
                        _47240 = _39577;
                        _47238 = _39572;
                    }
                    _47239 = _47240;
                    _47237 = _47238;
                }
                float _39607 = floor(_47173 * 0.0078125f);
                float _39612 = _47173 - (_39607 * 128.0f);
                float _47253;
                float _47255;
                if (_39612 >= 128.0f)
                {
                    _47255 = _39612 - 128.0f;
                    _47253 = _39607 + 1.0f;
                }
                else
                {
                    float _47254;
                    float _47256;
                    if (_39612 < 0.0f)
                    {
                        _47256 = _39612 + 128.0f;
                        _47254 = _39607 - 1.0f;
                    }
                    else
                    {
                        _47256 = _39612;
                        _47254 = _39607;
                    }
                    _47255 = _47256;
                    _47253 = _47254;
                }
                float3 _39147 = float3(_47185, _47197, _47209);
                float3 _47389;
                float _47396;
                float4 _51138;
                if (_14025)
                {
                    float _39642 = floor(_47175 * 0.25f);
                    float _39647 = _47175 - (_39642 * 4.0f);
                    float _47335;
                    float _47337;
                    if (_39647 >= 4.0f)
                    {
                        _47337 = _39647 - 4.0f;
                        _47335 = _39642 + 1.0f;
                    }
                    else
                    {
                        float _47336;
                        float _47338;
                        if (_39647 < 0.0f)
                        {
                            _47338 = _39647 + 4.0f;
                            _47336 = _39642 - 1.0f;
                        }
                        else
                        {
                            _47338 = _39647;
                            _47336 = _39642;
                        }
                        _47337 = _47338;
                        _47335 = _47336;
                    }
                    float _39677 = floor(_47335 * 0.25f);
                    float _39682 = _47335 - (_39677 * 4.0f);
                    float _47339;
                    float _47341;
                    if (_39682 >= 4.0f)
                    {
                        _47341 = _39682 - 4.0f;
                        _47339 = _39677 + 1.0f;
                    }
                    else
                    {
                        float _47340;
                        float _47342;
                        if (_39682 < 0.0f)
                        {
                            _47342 = _39682 + 4.0f;
                            _47340 = _39677 - 1.0f;
                        }
                        else
                        {
                            _47342 = _39682;
                            _47340 = _39677;
                        }
                        _47341 = _47342;
                        _47339 = _47340;
                    }
                    float _39712 = floor(_47339 * 0.25f);
                    float _39717 = _47339 - (_39712 * 4.0f);
                    float _47343;
                    float _47345;
                    if (_39717 >= 4.0f)
                    {
                        _47345 = _39717 - 4.0f;
                        _47343 = _39712 + 1.0f;
                    }
                    else
                    {
                        float _47344;
                        float _47346;
                        if (_39717 < 0.0f)
                        {
                            _47346 = _39717 + 4.0f;
                            _47344 = _39712 - 1.0f;
                        }
                        else
                        {
                            _47346 = _39717;
                            _47344 = _39712;
                        }
                        _47345 = _47346;
                        _47343 = _47344;
                    }
                    float _39747 = floor(_47177 * 0.015625f);
                    float _39752 = _47177 - (_39747 * 64.0f);
                    float _47353;
                    float _47355;
                    if (_39752 >= 64.0f)
                    {
                        _47355 = _39752 - 64.0f;
                        _47353 = _39747 + 1.0f;
                    }
                    else
                    {
                        float _47354;
                        float _47356;
                        if (_39752 < 0.0f)
                        {
                            _47356 = _39752 + 64.0f;
                            _47354 = _39747 - 1.0f;
                        }
                        else
                        {
                            _47356 = _39752;
                            _47354 = _39747;
                        }
                        _47355 = _47356;
                        _47353 = _47354;
                    }
                    bool _39182 = _47064 > 0.5f;
                    float _39823 = 0.5f * clamp(_47062 * 256.0f, 9.9999997473787516355514526367188e-06f, 256.0f);
                    float _39825 = _47061 * 256.0f;
                    float _39827 = _39825 - _39823;
                    float _39831 = _39825 + _39823;
                    float _47383;
                    float _47384;
                    if (!_39182)
                    {
                        _47384 = clamp(_39831, 0.0f, 256.0f);
                        _47383 = clamp(_39827, 0.0f, 256.0f);
                    }
                    else
                    {
                        _47384 = _39831;
                        _47383 = _39827;
                    }
                    float _39841 = floor(_47383);
                    float _39843 = floor(_47384);
                    float _39849 = _39182 ? mod(mod(_39841, 256.0f) + 256.0f, 256.0f) : min(_39841, 255.0f);
                    float _39854 = min(_39843, 255.0f);
                    float _39855 = _39182 ? mod(mod(_39843, 256.0f) + 256.0f, 256.0f) : _39854;
                    float _40018 = _39849 * 2.0f;
                    float _40027 = ((_47343 + (32.0f * _47353)) + 0.5f) * _8606_ramp_texel.y;
                    float4 _40063 = RampTex.Sample(RampSampler, float2((_40018 + 0.5f) * _8606_ramp_texel.x, _40027));
                    float2 _40056 = float2((floor((_40063.y * 255.0f) + 0.5f) * 256.0f) + floor((_40063.x * 255.0f) + 0.5f), (floor((_40063.w * 255.0f) + 0.5f) * 256.0f) + floor((_40063.z * 255.0f) + 0.5f)) * 1.525902189314365386962890625e-05f.xx;
                    float _40069 = _39855 * 2.0f;
                    float4 _40114 = RampTex.Sample(RampSampler, float2((_40069 + 0.5f) * _8606_ramp_texel.x, _40027));
                    float2 _40107 = float2((floor((_40114.y * 255.0f) + 0.5f) * 256.0f) + floor((_40114.x * 255.0f) + 0.5f), (floor((_40114.w * 255.0f) + 0.5f) * 256.0f) + floor((_40114.z * 255.0f) + 0.5f)) * 1.525902189314365386962890625e-05f.xx;
                    float _39872 = _47383 - (_39182 ? _39841 : _39849);
                    float _39878 = _47384 - (_39182 ? _39843 : _39855);
                    float _39880 = _40056.x;
                    float _39882 = _40056.y;
                    float _39884 = lerp(_39880, _39882, _39872);
                    float _39886 = _40107.x;
                    float _39888 = _40107.y;
                    float _39890 = lerp(_39886, _39888, _39878);
                    float _39893 = _47384 - _47383;
                    float _39894 = max(_39893, 9.9999997473787516355514526367188e-06f);
                    float _47386;
                    if (_39893 <= 1.0f)
                    {
                        _47386 = (((_39182 ? _39843 : _39854) - _39841) < 0.5f) ? ((_39884 + _39890) * 0.5f) : ((((1.0f - _39872) * lerp(_39880, _39882, (1.0f + _39872) * 0.5f)) + (_39878 * lerp(_39886, _39888, _39878 * 0.5f))) / _39894);
                    }
                    else
                    {
                        float4 _40174 = RampTex.Sample(RampSampler, float2((_40018 + 1.5f) * _8606_ramp_texel.x, _40027));
                        float4 _40234 = RampTex.Sample(RampSampler, float2((_40069 + 1.5f) * _8606_ramp_texel.x, _40027));
                        float _40227 = (((floor((_40234.x * 255.0f) + 0.5f) + (floor((_40234.y * 255.0f) + 0.5f) * 256.0f)) + (floor((_40234.z * 255.0f) + 0.5f) * 65536.0f)) * 1.5258790881489403545856475830078e-05f) + ((_39886 + (((_39888 - _39886) * 0.5f) * _39878)) * _39878);
                        float _47385;
                        if (_39182)
                        {
                            float4 _40285 = RampTex.Sample(RampSampler, float2(510.5f * _8606_ramp_texel.x, _40027));
                            float2 _40278 = float2((floor((_40285.y * 255.0f) + 0.5f) * 256.0f) + floor((_40285.x * 255.0f) + 0.5f), (floor((_40285.w * 255.0f) + 0.5f) * 256.0f) + floor((_40285.z * 255.0f) + 0.5f)) * 1.525902189314365386962890625e-05f.xx;
                            float4 _40345 = RampTex.Sample(RampSampler, float2(511.5f * _8606_ramp_texel.x, _40027));
                            float _40326 = _40278.x;
                            _47385 = _40227 + ((floor(_47384 * 0.00390625f) - floor(_47383 * 0.00390625f)) * ((((floor((_40345.x * 255.0f) + 0.5f) + (floor((_40345.y * 255.0f) + 0.5f) * 256.0f)) + (floor((_40345.z * 255.0f) + 0.5f) * 65536.0f)) * 1.5258790881489403545856475830078e-05f) + (_40326 + ((_40278.y - _40326) * 0.5f))));
                        }
                        else
                        {
                            _47385 = _40227;
                        }
                        _47386 = (_47385 - ((((floor((_40174.x * 255.0f) + 0.5f) + (floor((_40174.y * 255.0f) + 0.5f) * 256.0f)) + (floor((_40174.z * 255.0f) + 0.5f) * 65536.0f)) * 1.5258790881489403545856475830078e-05f) + ((_39880 + (((_39882 - _39880) * 0.5f) * _39872)) * _39872))) / _39894;
                    }
                    float _39969 = _39890 - _39884;
                    float _39974 = abs(_39969);
                    float _39978 = (_39890 - _47386) / ((_39974 > 0.001000000047497451305389404296875f) ? _39969 : 1000000015047466219876688855040.0f);
                    _51138 = float4(_39884, _39890, (((_39978 < 0.0f) || (_39978 > 1.0f)) || (_39974 <= 0.001000000047497451305389404296875f)) ? (-1.0f) : _39978, _47386);
                    _47396 = _47355;
                    _47389 = ((float3(_47223, _47237, _47253) * 4.0f) + float3(_47337, _47341, _47345)) * 0.015625f.xxx;
                }
                else
                {
                    float _40353 = floor(_47175 * 0.03125f);
                    float _40358 = _47175 - (_40353 * 32.0f);
                    float _47293;
                    float _47295;
                    if (_40358 >= 32.0f)
                    {
                        _47295 = _40358 - 32.0f;
                        _47293 = _40353 + 1.0f;
                    }
                    else
                    {
                        float _47294;
                        float _47296;
                        if (_40358 < 0.0f)
                        {
                            _47296 = _40358 + 32.0f;
                            _47294 = _40353 - 1.0f;
                        }
                        else
                        {
                            _47296 = _40358;
                            _47294 = _40353;
                        }
                        _47295 = _47296;
                        _47293 = _47294;
                    }
                    float _40388 = floor(_47177 * 0.03125f);
                    float _40393 = _47177 - (_40388 * 32.0f);
                    float _47311;
                    float _47313;
                    if (_40393 >= 32.0f)
                    {
                        _47313 = _40393 - 32.0f;
                        _47311 = _40388 + 1.0f;
                    }
                    else
                    {
                        float _47312;
                        float _47314;
                        if (_40393 < 0.0f)
                        {
                            _47314 = _40393 + 32.0f;
                            _47312 = _40388 - 1.0f;
                        }
                        else
                        {
                            _47314 = _40393;
                            _47312 = _40388;
                        }
                        _47313 = _47314;
                        _47311 = _47312;
                    }
                    _51138 = float4(_47061, _47061, -1.0f, _47061);
                    _47396 = _47311;
                    _47389 = ((float3(_47223, _47237, _47253) * 32.0f) + float3(_47295, _47293, _47313)) * 0.001953125f.xxx;
                }
                float3 _39210 = float3(_47187, _47199, _47211);
                float3 _39214 = float3(_47225, _47239, _47255);
                float3 _47394;
                if (_51138.z >= 0.0f)
                {
                    _47394 = lerp(clamp((_39210 + (_39214 * cos(((_39147 * _51138.y) + _47389) * 6.283185482025146484375f))) * 0.0078740157186985015869140625f.xxx, 0.0f.xxx, 1.0f.xxx), clamp((_39210 + (_39214 * cos(((_39147 * _51138.x) + _47389) * 6.283185482025146484375f))) * 0.0078740157186985015869140625f.xxx, 0.0f.xxx, 1.0f.xxx), _51138.z.xxx);
                }
                else
                {
                    _47394 = clamp((_39210 + (_39214 * cos(((_39147 * _51138.w) + _47389) * 6.283185482025146484375f))) * 0.0078740157186985015869140625f.xxx, 0.0f.xxx, 1.0f.xxx);
                }
                _47403 = _42707;
                _47401 = float4(_47394, _47396 * 0.01587301678955554962158203125f);
            }
            else
            {
                float4 _47402;
                float _47442;
                if (_14039)
                {
                    float _40423 = floor(_14084 * 0.00048828125f);
                    float _40428 = _14084 - (_40423 * 2048.0f);
                    float _47152;
                    float _47154;
                    if (_40428 >= 2048.0f)
                    {
                        _47154 = _40428 - 2048.0f;
                        _47152 = _40423 + 1.0f;
                    }
                    else
                    {
                        float _47153;
                        float _47155;
                        if (_40428 < 0.0f)
                        {
                            _47155 = _40428 + 2048.0f;
                            _47153 = _40423 - 1.0f;
                        }
                        else
                        {
                            _47155 = _40428;
                            _47153 = _40423;
                        }
                        _47154 = _47155;
                        _47152 = _47153;
                    }
                    bool _14469 = _47064 > 0.5f;
                    float _40513 = 0.5f * clamp(_47062 * 256.0f, 9.9999997473787516355514526367188e-06f, 256.0f);
                    float _40515 = _47061 * 256.0f;
                    float _40517 = _40515 - _40513;
                    float _40521 = _40515 + _40513;
                    float _47158;
                    float _47159;
                    if (!_14469)
                    {
                        _47159 = clamp(_40521, 0.0f, 256.0f);
                        _47158 = clamp(_40517, 0.0f, 256.0f);
                    }
                    else
                    {
                        _47159 = _40521;
                        _47158 = _40517;
                    }
                    float _40531 = floor(_47158);
                    float _40533 = floor(_47159);
                    float _40539 = _14469 ? mod(mod(_40531, 256.0f) + 256.0f, 256.0f) : min(_40531, 255.0f);
                    float _40544 = min(_40533, 255.0f);
                    float _40545 = _14469 ? mod(mod(_40533, 256.0f) + 256.0f, 256.0f) : _40544;
                    float _40556 = _47158 - (_14469 ? _40531 : _40539);
                    float _40562 = _47159 - (_14469 ? _40533 : _40545);
                    float _40685 = _40539 * 2.0f;
                    float _40691 = (_40685 + 0.5f) * _8606_ramp_texel.x;
                    float _40696 = (_47152 + 0.5f) * _8606_ramp_texel.y;
                    float4 _40705 = RampTex.Sample(RampSampler, float2(_40691, _40696));
                    float _40716 = (_40685 + 1.5f) * _8606_ramp_texel.x;
                    float4 _40730 = RampTex.Sample(RampSampler, float2(_40716, _40696));
                    float _40735 = _40545 * 2.0f;
                    float _40741 = (_40735 + 0.5f) * _8606_ramp_texel.x;
                    float4 _40755 = RampTex.Sample(RampSampler, float2(_40741, _40696));
                    float _40766 = (_40735 + 1.5f) * _8606_ramp_texel.x;
                    float4 _40780 = RampTex.Sample(RampSampler, float2(_40766, _40696));
                    float _40578 = max(_47159 - _47158, 9.9999997473787516355514526367188e-06f);
                    float _40581 = (_14469 ? _40533 : _40544) - _40531;
                    float4 _47161;
                    if (_40581 < 0.5f)
                    {
                        _47161 = lerp(_40705, _40730, ((_40556 + _40562) * 0.5f).xxxx);
                    }
                    else
                    {
                        float4 _47162;
                        if (_40581 < 1.5f)
                        {
                            _47162 = ((lerp(_40705, _40730, ((1.0f + _40556) * 0.5f).xxxx) * (1.0f - _40556)) + (lerp(_40755, _40780, (_40562 * 0.5f).xxxx) * _40562)) / _40578.xxxx;
                        }
                        else
                        {
                            float _40816 = (_47154 + 0.5f) * _8606_ramp_texel.y;
                            float4 _40889 = RampTex.Sample(RampSampler, float2(_40691, _40816));
                            float4 _40896 = RampTex.Sample(RampSampler, float2(_40716, _40816));
                            float4 _41005 = RampTex.Sample(RampSampler, float2(_40741, _40816));
                            float4 _41012 = RampTex.Sample(RampSampler, float2(_40766, _40816));
                            float4 _40915 = ((float4((floor((_41005.y * 255.0f) + 0.5f) * 256.0f) + floor((_41005.x * 255.0f) + 0.5f), (floor((_41005.w * 255.0f) + 0.5f) * 256.0f) + floor((_41005.z * 255.0f) + 0.5f), (floor((_41012.y * 255.0f) + 0.5f) * 256.0f) + floor((_41012.x * 255.0f) + 0.5f), (floor((_41012.w * 255.0f) + 0.5f) * 256.0f) + floor((_41012.z * 255.0f) + 0.5f)) * 1.525902189314365386962890625e-05f.xxxx) * 256.0f) + ((_40755 + (((_40780 - _40755) * 0.5f) * _40562)) * _40562);
                            float4 _47160;
                            if (_14469)
                            {
                                float _41023 = 510.5f * _8606_ramp_texel.x;
                                float4 _41037 = RampTex.Sample(RampSampler, float2(_41023, _40696));
                                float _41048 = 511.5f * _8606_ramp_texel.x;
                                float4 _41171 = RampTex.Sample(RampSampler, float2(_41023, _40816));
                                float4 _41178 = RampTex.Sample(RampSampler, float2(_41048, _40816));
                                _47160 = _40915 + ((((float4((floor((_41171.y * 255.0f) + 0.5f) * 256.0f) + floor((_41171.x * 255.0f) + 0.5f), (floor((_41171.w * 255.0f) + 0.5f) * 256.0f) + floor((_41171.z * 255.0f) + 0.5f), (floor((_41178.y * 255.0f) + 0.5f) * 256.0f) + floor((_41178.x * 255.0f) + 0.5f), (floor((_41178.w * 255.0f) + 0.5f) * 256.0f) + floor((_41178.z * 255.0f) + 0.5f)) * 1.525902189314365386962890625e-05f.xxxx) * 256.0f) + ((_41037 + (((RampTex.Sample(RampSampler, float2(_41048, _40696)) - _41037) * 0.5f) * 1.0f)) * 1.0f)) * (floor(_47159 * 0.00390625f) - floor(_47158 * 0.00390625f)));
                            }
                            else
                            {
                                _47160 = _40915;
                            }
                            _47162 = (_47160 - (((float4((floor((_40889.y * 255.0f) + 0.5f) * 256.0f) + floor((_40889.x * 255.0f) + 0.5f), (floor((_40889.w * 255.0f) + 0.5f) * 256.0f) + floor((_40889.z * 255.0f) + 0.5f), (floor((_40896.y * 255.0f) + 0.5f) * 256.0f) + floor((_40896.x * 255.0f) + 0.5f), (floor((_40896.w * 255.0f) + 0.5f) * 256.0f) + floor((_40896.z * 255.0f) + 0.5f)) * 1.525902189314365386962890625e-05f.xxxx) * 256.0f) + ((_40705 + (((_40730 - _40705) * 0.5f) * _40556)) * _40556))) / _40578.xxxx;
                        }
                        _47161 = _47162;
                    }
                    _47442 = max(_42707, 1.0f);
                    _47402 = _47161;
                }
                else
                {
                    float _41214 = floor(p_Border.x * 0.00048828125f);
                    float _41219 = p_Border.x - (_41214 * 2048.0f);
                    float _47083;
                    float _47085;
                    if (_41219 >= 2048.0f)
                    {
                        _47085 = _41219 - 2048.0f;
                        _47083 = _41214 + 1.0f;
                    }
                    else
                    {
                        float _47084;
                        float _47086;
                        if (_41219 < 0.0f)
                        {
                            _47086 = _41219 + 2048.0f;
                            _47084 = _41214 - 1.0f;
                        }
                        else
                        {
                            _47086 = _41219;
                            _47084 = _41214;
                        }
                        _47085 = _47086;
                        _47083 = _47084;
                    }
                    float _41262 = floor(_14076 * 0.00048828125f);
                    float _41267 = _14076 - (_41262 * 2048.0f);
                    bool _41270 = _41267 >= 2048.0f;
                    float _47087;
                    float _47089;
                    if (_41270)
                    {
                        _47089 = _41267 - 2048.0f;
                        _47087 = _41262 + 1.0f;
                    }
                    else
                    {
                        float _47088;
                        float _47090;
                        if (_41267 < 0.0f)
                        {
                            _47090 = _41267 + 2048.0f;
                            _47088 = _41262 - 1.0f;
                        }
                        else
                        {
                            _47090 = _41267;
                            _47088 = _41262;
                        }
                        _47089 = _47090;
                        _47087 = _47088;
                    }
                    float4 _41193 = float4(float2(_47083, _47085) * 0.000488519784994423389434814453125f.xx, float2(_47087, _47089) * 0.000488519784994423389434814453125f.xx);
                    float _41325 = floor(_14084 * 0.00048828125f);
                    float _41330 = _14084 - (_41325 * 2048.0f);
                    float _47091;
                    float _47093;
                    if (_41330 >= 2048.0f)
                    {
                        _47093 = _41330 - 2048.0f;
                        _47091 = _41325 + 1.0f;
                    }
                    else
                    {
                        float _47092;
                        float _47094;
                        if (_41330 < 0.0f)
                        {
                            _47094 = _41330 + 2048.0f;
                            _47092 = _41325 - 1.0f;
                        }
                        else
                        {
                            _47094 = _41330;
                            _47092 = _41325;
                        }
                        _47093 = _47094;
                        _47091 = _47092;
                    }
                    float _41373 = floor(_14092 * 0.00048828125f);
                    float _41378 = _14092 - (_41373 * 2048.0f);
                    bool _41381 = _41378 >= 2048.0f;
                    float _47095;
                    float _47097;
                    if (_41381)
                    {
                        _47097 = _41378 - 2048.0f;
                        _47095 = _41373 + 1.0f;
                    }
                    else
                    {
                        float _47096;
                        float _47098;
                        if (_41378 < 0.0f)
                        {
                            _47098 = _41378 + 2048.0f;
                            _47096 = _41373 - 1.0f;
                        }
                        else
                        {
                            _47098 = _41378;
                            _47096 = _41373;
                        }
                        _47097 = _47098;
                        _47095 = _47096;
                    }
                    float4 _41304 = float4(float2(_47091, _47093) * 0.000488519784994423389434814453125f.xx, float2(_47095, _47097) * 0.000488519784994423389434814453125f.xx);
                    float4 _47141;
                    float4 _47146;
                    float _47151;
                    if (_14025)
                    {
                        float _47103;
                        if (_41270)
                        {
                            _47103 = _41267 - 2048.0f;
                        }
                        else
                        {
                            float _47104;
                            if (_41267 < 0.0f)
                            {
                                _47104 = _41267 + 2048.0f;
                            }
                            else
                            {
                                _47104 = _41267;
                            }
                            _47103 = _47104;
                        }
                        float _41486 = floor(_47103 * 0.00390625f);
                        float _41491 = _47103 - (_41486 * 256.0f);
                        float _47105;
                        float _47107;
                        if (_41491 >= 256.0f)
                        {
                            _47107 = _41491 - 256.0f;
                            _47105 = _41486 + 1.0f;
                        }
                        else
                        {
                            float _47106;
                            float _47108;
                            if (_41491 < 0.0f)
                            {
                                _47108 = _41491 + 256.0f;
                                _47106 = _41486 - 1.0f;
                            }
                            else
                            {
                                _47108 = _41491;
                                _47106 = _41486;
                            }
                            _47107 = _47108;
                            _47105 = _47106;
                        }
                        float4 _49637 = _41193;
                        _49637.w = _47107 * 0.0039215688593685626983642578125f;
                        float _47109;
                        if (_41381)
                        {
                            _47109 = _41378 - 2048.0f;
                        }
                        else
                        {
                            float _47110;
                            if (_41378 < 0.0f)
                            {
                                _47110 = _41378 + 2048.0f;
                            }
                            else
                            {
                                _47110 = _41378;
                            }
                            _47109 = _47110;
                        }
                        float _41556 = floor(_47109 * 0.00390625f);
                        float _41561 = _47109 - (_41556 * 256.0f);
                        float _47111;
                        float _47113;
                        if (_41561 >= 256.0f)
                        {
                            _47113 = _41561 - 256.0f;
                            _47111 = _41556 + 1.0f;
                        }
                        else
                        {
                            float _47112;
                            float _47114;
                            if (_41561 < 0.0f)
                            {
                                _47114 = _41561 + 256.0f;
                                _47112 = _41556 - 1.0f;
                            }
                            else
                            {
                                _47114 = _41561;
                                _47112 = _41556;
                            }
                            _47113 = _47114;
                            _47111 = _47112;
                        }
                        float4 _49640 = _41304;
                        _49640.w = _47113 * 0.0039215688593685626983642578125f;
                        bool _14495 = _47064 > 0.5f;
                        float _41632 = 0.5f * clamp(_47062 * 256.0f, 9.9999997473787516355514526367188e-06f, 256.0f);
                        float _41634 = _47061 * 256.0f;
                        float _41636 = _41634 - _41632;
                        float _41640 = _41634 + _41632;
                        float _47135;
                        float _47136;
                        if (!_14495)
                        {
                            _47136 = clamp(_41640, 0.0f, 256.0f);
                            _47135 = clamp(_41636, 0.0f, 256.0f);
                        }
                        else
                        {
                            _47136 = _41640;
                            _47135 = _41636;
                        }
                        float _41650 = floor(_47135);
                        float _41652 = floor(_47136);
                        float _41658 = _14495 ? mod(mod(_41650, 256.0f) + 256.0f, 256.0f) : min(_41650, 255.0f);
                        float _41663 = min(_41652, 255.0f);
                        float _41664 = _14495 ? mod(mod(_41652, 256.0f) + 256.0f, 256.0f) : _41663;
                        float _41827 = _41658 * 2.0f;
                        float _41836 = ((((_47105 + (8.0f * _47111)) + (_14028 ? 64.0f : 0.0f)) + (_14031 ? 128.0f : 0.0f)) + 0.5f) * _8606_ramp_texel.y;
                        float4 _41872 = RampTex.Sample(RampSampler, float2((_41827 + 0.5f) * _8606_ramp_texel.x, _41836));
                        float2 _41865 = float2((floor((_41872.y * 255.0f) + 0.5f) * 256.0f) + floor((_41872.x * 255.0f) + 0.5f), (floor((_41872.w * 255.0f) + 0.5f) * 256.0f) + floor((_41872.z * 255.0f) + 0.5f)) * 1.525902189314365386962890625e-05f.xx;
                        float _41878 = _41664 * 2.0f;
                        float4 _41923 = RampTex.Sample(RampSampler, float2((_41878 + 0.5f) * _8606_ramp_texel.x, _41836));
                        float2 _41916 = float2((floor((_41923.y * 255.0f) + 0.5f) * 256.0f) + floor((_41923.x * 255.0f) + 0.5f), (floor((_41923.w * 255.0f) + 0.5f) * 256.0f) + floor((_41923.z * 255.0f) + 0.5f)) * 1.525902189314365386962890625e-05f.xx;
                        float _41681 = _47135 - (_14495 ? _41650 : _41658);
                        float _41687 = _47136 - (_14495 ? _41652 : _41664);
                        float _41689 = _41865.x;
                        float _41691 = _41865.y;
                        float _41695 = _41916.x;
                        float _41697 = _41916.y;
                        float _41702 = _47136 - _47135;
                        float _41703 = max(_41702, 9.9999997473787516355514526367188e-06f);
                        float _47138;
                        if (_41702 <= 1.0f)
                        {
                            _47138 = (((_14495 ? _41652 : _41663) - _41650) < 0.5f) ? ((lerp(_41689, _41691, _41681) + lerp(_41695, _41697, _41687)) * 0.5f) : ((((1.0f - _41681) * lerp(_41689, _41691, (1.0f + _41681) * 0.5f)) + (_41687 * lerp(_41695, _41697, _41687 * 0.5f))) / _41703);
                        }
                        else
                        {
                            float4 _41983 = RampTex.Sample(RampSampler, float2((_41827 + 1.5f) * _8606_ramp_texel.x, _41836));
                            float4 _42043 = RampTex.Sample(RampSampler, float2((_41878 + 1.5f) * _8606_ramp_texel.x, _41836));
                            float _42036 = (((floor((_42043.x * 255.0f) + 0.5f) + (floor((_42043.y * 255.0f) + 0.5f) * 256.0f)) + (floor((_42043.z * 255.0f) + 0.5f) * 65536.0f)) * 1.5258790881489403545856475830078e-05f) + ((_41695 + (((_41697 - _41695) * 0.5f) * _41687)) * _41687);
                            float _47137;
                            if (_14495)
                            {
                                float4 _42094 = RampTex.Sample(RampSampler, float2(510.5f * _8606_ramp_texel.x, _41836));
                                float2 _42087 = float2((floor((_42094.y * 255.0f) + 0.5f) * 256.0f) + floor((_42094.x * 255.0f) + 0.5f), (floor((_42094.w * 255.0f) + 0.5f) * 256.0f) + floor((_42094.z * 255.0f) + 0.5f)) * 1.525902189314365386962890625e-05f.xx;
                                float4 _42154 = RampTex.Sample(RampSampler, float2(511.5f * _8606_ramp_texel.x, _41836));
                                float _42135 = _42087.x;
                                _47137 = _42036 + ((floor(_47136 * 0.00390625f) - floor(_47135 * 0.00390625f)) * ((((floor((_42154.x * 255.0f) + 0.5f) + (floor((_42154.y * 255.0f) + 0.5f) * 256.0f)) + (floor((_42154.z * 255.0f) + 0.5f) * 65536.0f)) * 1.5258790881489403545856475830078e-05f) + (_42135 + ((_42087.y - _42135) * 0.5f))));
                            }
                            else
                            {
                                _47137 = _42036;
                            }
                            _47138 = (_47137 - ((((floor((_41983.x * 255.0f) + 0.5f) + (floor((_41983.y * 255.0f) + 0.5f) * 256.0f)) + (floor((_41983.z * 255.0f) + 0.5f) * 65536.0f)) * 1.5258790881489403545856475830078e-05f) + ((_41689 + (((_41691 - _41689) * 0.5f) * _41681)) * _41681))) / _41703;
                        }
                        _47151 = _47138;
                        _47146 = _49640;
                        _47141 = _49637;
                    }
                    else
                    {
                        _47151 = _47061;
                        _47146 = _41304;
                        _47141 = _41193;
                    }
                    float _42168 = lerp(_47141.w, _47146.w, _47151);
                    float _42178 = (_42168 > 0.0f) ? ((_47151 * _47146.w) / _42168) : _47151;
                    float4 _42196 = lerp(_47141, _47146, _42178.xxxx);
                    float4 _51137;
                    if (_42707 < 0.5f)
                    {
                        float4 _49685 = _42196;
                        _49685.z = frac(_47141.z + ((frac((_47146.z - _47141.z) + 0.5f) - 0.5f) * _42178));
                        _51137 = _49685;
                    }
                    else
                    {
                        _51137 = _42196;
                    }
                    float4 _49687 = _51137;
                    _49687.w = _42168;
                    _47442 = _42707;
                    _47402 = _49687;
                }
                _47403 = _47442;
                _47401 = _47402;
            }
            float4 _47465;
            if (_47403 < 0.5f)
            {
                float _42237 = _47401.y * 0.4000000059604644775390625f;
                float _42241 = (_47401.z * 6.283185482025146484375f) - 3.1415927410125732421875f;
                float _42247 = _42237 * cos(_42241);
                float _42251 = _42237 * sin(_42241);
                float _42300 = (_47401.x + (0.3963377773761749267578125f * _42247)) + (0.21580375730991363525390625f * _42251);
                float _42310 = (_47401.x - (0.1055613458156585693359375f * _42247)) - (0.06385417282581329345703125f * _42251);
                float _42320 = (_47401.x - (0.089484177529811859130859375f * _42247)) - (1.2914855480194091796875f * _42251);
                float _42325 = (_42300 * _42300) * _42300;
                float _42330 = (_42310 * _42310) * _42310;
                float _42335 = (_42320 * _42320) * _42320;
                float _42343 = ((4.076741695404052734375f * _42325) - (3.30771160125732421875f * _42330)) + (0.2309699356555938720703125f * _42335);
                float _42351 = (((-1.26843798160552978515625f) * _42325) + (2.60975742340087890625f * _42330)) - (0.341319382190704345703125f * _42335);
                float _42359 = (((-0.0041960864327847957611083984375f) * _42325) - (0.70341861248016357421875f * _42330)) + (1.7076146602630615234375f * _42335);
                _47465 = float4((_42343 >= 0.003130800090730190277099609375f) ? ((pow(abs(_42343), 0.4166666567325592041015625f) * 1.05499994754791259765625f) - 0.054999999701976776123046875f) : (12.9200000762939453125f * _42343), (_42351 >= 0.003130800090730190277099609375f) ? ((pow(abs(_42351), 0.4166666567325592041015625f) * 1.05499994754791259765625f) - 0.054999999701976776123046875f) : (12.9200000762939453125f * _42351), (_42359 >= 0.003130800090730190277099609375f) ? ((pow(abs(_42359), 0.4166666567325592041015625f) * 1.05499994754791259765625f) - 0.054999999701976776123046875f) : (12.9200000762939453125f * _42359), _47401.w);
            }
            else
            {
                float4 _47466;
                if (_47403 < 1.5f)
                {
                    float _42265 = (_47401.y * 0.800000011920928955078125f) - 0.4000000059604644775390625f;
                    float _42269 = (_47401.z * 0.800000011920928955078125f) - 0.4000000059604644775390625f;
                    float _42428 = (_47401.x + (0.3963377773761749267578125f * _42265)) + (0.21580375730991363525390625f * _42269);
                    float _42438 = (_47401.x - (0.1055613458156585693359375f * _42265)) - (0.06385417282581329345703125f * _42269);
                    float _42448 = (_47401.x - (0.089484177529811859130859375f * _42265)) - (1.2914855480194091796875f * _42269);
                    float _42453 = (_42428 * _42428) * _42428;
                    float _42458 = (_42438 * _42438) * _42438;
                    float _42463 = (_42448 * _42448) * _42448;
                    float _42471 = ((4.076741695404052734375f * _42453) - (3.30771160125732421875f * _42458)) + (0.2309699356555938720703125f * _42463);
                    float _42479 = (((-1.26843798160552978515625f) * _42453) + (2.60975742340087890625f * _42458)) - (0.341319382190704345703125f * _42463);
                    float _42487 = (((-0.0041960864327847957611083984375f) * _42453) - (0.70341861248016357421875f * _42458)) + (1.7076146602630615234375f * _42463);
                    _47466 = float4((_42471 >= 0.003130800090730190277099609375f) ? ((pow(abs(_42471), 0.4166666567325592041015625f) * 1.05499994754791259765625f) - 0.054999999701976776123046875f) : (12.9200000762939453125f * _42471), (_42479 >= 0.003130800090730190277099609375f) ? ((pow(abs(_42479), 0.4166666567325592041015625f) * 1.05499994754791259765625f) - 0.054999999701976776123046875f) : (12.9200000762939453125f * _42479), (_42487 >= 0.003130800090730190277099609375f) ? ((pow(abs(_42487), 0.4166666567325592041015625f) * 1.05499994754791259765625f) - 0.054999999701976776123046875f) : (12.9200000762939453125f * _42487), _47401.w);
                }
                else
                {
                    _47466 = _47401;
                }
                _47465 = _47466;
            }
            float3 _14516 = _47465.xyz * _47465.w;
            float4 _49718 = _47465;
            _49718.x = _14516.x;
            _49718.y = _14516.y;
            _49718.z = _14516.z;
            _51233 = _49718;
        }
        else
        {
            _51233 = _51234;
        }
        float4 _14533 = (lerp(_51234, _51233, _46078.xxxx) * (_12976 ? _44433 : (1.0f - smoothstep(0.0f, 1.0f, clamp((_44430 / _14001) + _12867, 0.0f, 1.0f))))) * _12948;
        float4 _42546 = mul(float4(p_Pos.xy, 0.0f, 1.0f), _8606_view_projection);
        float2 _42555 = (_42546.xy / _42546.w.xx) * _8606_half_viewport;
        float3 _14545 = _14533.xyz + ((((_8606_dither_mode < 0.5f) ? clamp(frac(52.98291778564453125f * frac(dot(_42555, float2(0.067110560834407806396484375f, 0.005837149918079376220703125f)))), 0.0f, 1.0f) : BlueNoiseTex.Sample(BlueNoiseSampler, _42555 * 0.015625f.xx).x) - 0.5f) * _8606_dither_scale).xxx;
        float4 _49725 = _14533;
        _49725.x = _14545.x;
        _49725.y = _14545.y;
        _49725.z = _14545.z;
        _47662 = _49725;
        break;
    } while(false);
    _entryPointOutput = _47662;
}

SPIRV_Cross_Output main(SPIRV_Cross_Input stage_input)
{
    p_TexCoord = stage_input.p_TexCoord;
    p_Fill = stage_input.p_Fill;
    p_Border = stage_input.p_Border;
    p_FillCoord = stage_input.p_FillCoord;
    p_BorderCoord = stage_input.p_BorderCoord;
    p_Meta1 = stage_input.p_Meta1;
    p_Meta2 = stage_input.p_Meta2;
    p_Meta3 = stage_input.p_Meta3;
    p_Pos = stage_input.p_Pos;
    p_ClipMeta = stage_input.p_ClipMeta;
    frag_main();
    SPIRV_Cross_Output stage_output;
    stage_output._entryPointOutput = _entryPointOutput;
    return stage_output;
}
