#include <metal_stdlib>
#include <simd/simd.h>

using namespace metal;

struct _Global
{
    float4x4 view_projection;
    float2 half_viewport;
    float dither_scale;
    float dither_mode;
    float2 ramp_texel;
    float2 band_tex_size;
    float2 band_texel;
    float2 curve_texel;
};

struct main0_out
{
    float4 _entryPointOutput_Position [[user(locn0)]];
    float4 _entryPointOutput_TexCoord [[user(locn1)]];
    float4 _entryPointOutput_Fill [[user(locn2)]];
    float4 _entryPointOutput_Border [[user(locn3)]];
    float4 _entryPointOutput_FillCoord [[user(locn4)]];
    float4 _entryPointOutput_BorderCoord [[user(locn5)]];
    float4 _entryPointOutput_Meta1 [[user(locn6)]];
    float4 _entryPointOutput_Meta2 [[user(locn7)]];
    float4 _entryPointOutput_Meta3 [[user(locn8)]];
    float4 _entryPointOutput_Pos [[user(locn9)]];
    float4 _entryPointOutput_ClipMeta [[user(locn10)]];
    float4 gl_Position [[position]];
};

struct main0_in
{
    float4 v_Position [[attribute(0)]];
    float4 v_TexCoord [[attribute(1)]];
    float4 v_FillA [[attribute(2)]];
    float4 v_FillB [[attribute(3)]];
    float4 v_BorderA [[attribute(4)]];
    float4 v_BorderB [[attribute(5)]];
    float4 v_FillCoord [[attribute(6)]];
    float4 v_BorderCoord [[attribute(7)]];
    float4 v_Meta1 [[attribute(8)]];
    float4 v_Meta2 [[attribute(9)]];
    float4 v_Meta3 [[attribute(10)]];
    float4 v_ClipDist [[attribute(11)]];
    float2 v_ClipRoundAA [[attribute(12)]];
};

vertex main0_out main0(main0_in in [[stage_in]], constant _Global& _108 [[buffer(0)]])
{
    main0_out out = {};
    float4 _565 = select(in.v_FillA, in.v_FillA * float4(3.0518509447574615478515625e-05), bool4(any(abs(in.v_FillA) > float4(1.5))));
    float4 _577 = select(in.v_FillB, in.v_FillB * float4(3.0518509447574615478515625e-05), bool4(any(abs(in.v_FillB) > float4(1.5))));
    float4 _589 = select(in.v_BorderA, in.v_BorderA * float4(3.0518509447574615478515625e-05), bool4(any(abs(in.v_BorderA) > float4(1.5))));
    float4 _601 = select(in.v_BorderB, in.v_BorderB * float4(3.0518509447574615478515625e-05), bool4(any(abs(in.v_BorderB) > float4(1.5))));
    float4 _424 = abs(_565);
    float4 _426 = abs(_577);
    float _644 = (floor((_424.x * 2047.0) + 0.5) * 2048.0) + floor((_424.y * 2047.0) + 0.5);
    float4 _632 = float4(_644, (floor((_424.z * 2047.0) + 0.5) * 2048.0) + floor((_424.w * 2047.0) + 0.5), (floor((_426.x * 2047.0) + 0.5) * 2048.0) + floor((_426.y * 2047.0) + 0.5), (floor((_426.z * 2047.0) + 0.5) * 2048.0) + floor((_426.w * 2047.0) + 0.5));
    float4 _430 = abs(_589);
    float4 _432 = abs(_601);
    float _723 = (floor((_430.x * 2047.0) + 0.5) * 2048.0) + floor((_430.y * 2047.0) + 0.5);
    float4 _711 = float4(_723, (floor((_430.z * 2047.0) + 0.5) * 2048.0) + floor((_430.w * 2047.0) + 0.5), (floor((_432.x * 2047.0) + 0.5) * 2048.0) + floor((_432.y * 2047.0) + 0.5), (floor((_432.z * 2047.0) + 0.5) * 2048.0) + floor((_432.w * 2047.0) + 0.5));
    float4 _926;
    if (_565.x < 0.0)
    {
        float4 _897 = _632;
        _897.x = (-1.0) - _644;
        _926 = _897;
    }
    else
    {
        _926 = _632;
    }
    float4 _927;
    if (_565.z < 0.0)
    {
        float4 _901 = _926;
        _901.y = (-1.0) - _926.y;
        _927 = _901;
    }
    else
    {
        _927 = _926;
    }
    float4 _928;
    if (_577.x < 0.0)
    {
        float4 _905 = _927;
        _905.z = (-1.0) - _927.z;
        _928 = _905;
    }
    else
    {
        _928 = _927;
    }
    float4 _941;
    if (_577.z < 0.0)
    {
        float4 _909 = _928;
        _909.w = (-1.0) - _928.w;
        _941 = _909;
    }
    else
    {
        _941 = _928;
    }
    float4 _933;
    if (_589.x < 0.0)
    {
        float4 _913 = _711;
        _913.x = (-1.0) - _723;
        _933 = _913;
    }
    else
    {
        _933 = _711;
    }
    float4 _934;
    if (_589.z < 0.0)
    {
        float4 _917 = _933;
        _917.y = (-1.0) - _933.y;
        _934 = _917;
    }
    else
    {
        _934 = _933;
    }
    float4 _935;
    if (_601.x < 0.0)
    {
        float4 _921 = _934;
        _921.z = (-1.0) - _934.z;
        _935 = _921;
    }
    else
    {
        _935 = _934;
    }
    float4 _936;
    if (_601.z < 0.0)
    {
        float4 _925 = _935;
        _925.w = (-1.0) - _935.w;
        _936 = _925;
    }
    else
    {
        _936 = _935;
    }
    out._entryPointOutput_Position = in.v_Position * _108.view_projection;
    out._entryPointOutput_TexCoord = in.v_TexCoord;
    out._entryPointOutput_Fill = _941;
    out._entryPointOutput_Border = _936;
    out._entryPointOutput_FillCoord = in.v_FillCoord;
    out._entryPointOutput_BorderCoord = in.v_BorderCoord;
    out._entryPointOutput_Meta1 = in.v_Meta1;
    out._entryPointOutput_Meta2 = in.v_Meta2;
    out._entryPointOutput_Meta3 = in.v_Meta3;
    out._entryPointOutput_Pos = float4(in.v_Position.xy, in.v_ClipDist.xy);
    out._entryPointOutput_ClipMeta = float4(in.v_ClipDist.zw, in.v_ClipRoundAA);
    return out;
}

