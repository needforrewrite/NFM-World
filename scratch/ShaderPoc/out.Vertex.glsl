#version 450

layout(set = 0, binding = 0, std140) uniform _Global
{
    layout(row_major) mat4 view_projection;
    vec2 half_viewport;
    float dither_scale;
    float dither_mode;
    vec2 ramp_texel;
    vec2 band_tex_size;
    vec2 band_texel;
    vec2 curve_texel;
} _108;

layout(set = 0, binding = 0) uniform texture2D TextureTex;
layout(set = 0, binding = 0) uniform sampler TextureSampler;
layout(set = 0, binding = 1) uniform texture2D BandTex;
layout(set = 0, binding = 1) uniform sampler BandSampler;
layout(set = 0, binding = 2) uniform texture2D CurveTex;
layout(set = 0, binding = 2) uniform sampler CurveSampler;
layout(set = 0, binding = 3) uniform texture2D ArcTex;
layout(set = 0, binding = 3) uniform sampler ArcSampler;
layout(set = 0, binding = 4) uniform texture2D BlueNoiseTex;
layout(set = 0, binding = 4) uniform sampler BlueNoiseSampler;
layout(set = 0, binding = 5) uniform texture2D RampTex;
layout(set = 0, binding = 5) uniform sampler RampSampler;

layout(location = 0) in vec4 v_Position;
layout(location = 1) in vec4 v_TexCoord;
layout(location = 2) in vec4 v_FillA;
layout(location = 3) in vec4 v_FillB;
layout(location = 4) in vec4 v_BorderA;
layout(location = 5) in vec4 v_BorderB;
layout(location = 6) in vec4 v_FillCoord;
layout(location = 7) in vec4 v_BorderCoord;
layout(location = 8) in vec4 v_Meta1;
layout(location = 9) in vec4 v_Meta2;
layout(location = 10) in vec4 v_Meta3;
layout(location = 11) in vec4 v_ClipDist;
layout(location = 12) in vec2 v_ClipRoundAA;
layout(location = 0) out vec4 _entryPointOutput_Position;
layout(location = 1) out vec4 _entryPointOutput_TexCoord;
layout(location = 2) out vec4 _entryPointOutput_Fill;
layout(location = 3) out vec4 _entryPointOutput_Border;
layout(location = 4) out vec4 _entryPointOutput_FillCoord;
layout(location = 5) out vec4 _entryPointOutput_BorderCoord;
layout(location = 6) out vec4 _entryPointOutput_Meta1;
layout(location = 7) out vec4 _entryPointOutput_Meta2;
layout(location = 8) out vec4 _entryPointOutput_Meta3;
layout(location = 9) out vec4 _entryPointOutput_Pos;
layout(location = 10) out vec4 _entryPointOutput_ClipMeta;

void main()
{
    vec4 _565 = mix(v_FillA, v_FillA * vec4(3.0518509447574615478515625e-05), bvec4(any(greaterThan(abs(v_FillA), vec4(1.5)))));
    vec4 _577 = mix(v_FillB, v_FillB * vec4(3.0518509447574615478515625e-05), bvec4(any(greaterThan(abs(v_FillB), vec4(1.5)))));
    vec4 _589 = mix(v_BorderA, v_BorderA * vec4(3.0518509447574615478515625e-05), bvec4(any(greaterThan(abs(v_BorderA), vec4(1.5)))));
    vec4 _601 = mix(v_BorderB, v_BorderB * vec4(3.0518509447574615478515625e-05), bvec4(any(greaterThan(abs(v_BorderB), vec4(1.5)))));
    vec4 _424 = abs(_565);
    vec4 _426 = abs(_577);
    float _644 = (floor((_424.x * 2047.0) + 0.5) * 2048.0) + floor((_424.y * 2047.0) + 0.5);
    vec4 _632 = vec4(_644, (floor((_424.z * 2047.0) + 0.5) * 2048.0) + floor((_424.w * 2047.0) + 0.5), (floor((_426.x * 2047.0) + 0.5) * 2048.0) + floor((_426.y * 2047.0) + 0.5), (floor((_426.z * 2047.0) + 0.5) * 2048.0) + floor((_426.w * 2047.0) + 0.5));
    vec4 _430 = abs(_589);
    vec4 _432 = abs(_601);
    float _723 = (floor((_430.x * 2047.0) + 0.5) * 2048.0) + floor((_430.y * 2047.0) + 0.5);
    vec4 _711 = vec4(_723, (floor((_430.z * 2047.0) + 0.5) * 2048.0) + floor((_430.w * 2047.0) + 0.5), (floor((_432.x * 2047.0) + 0.5) * 2048.0) + floor((_432.y * 2047.0) + 0.5), (floor((_432.z * 2047.0) + 0.5) * 2048.0) + floor((_432.w * 2047.0) + 0.5));
    vec4 _926;
    if (_565.x < 0.0)
    {
        vec4 _897 = _632;
        _897.x = (-1.0) - _644;
        _926 = _897;
    }
    else
    {
        _926 = _632;
    }
    vec4 _927;
    if (_565.z < 0.0)
    {
        vec4 _901 = _926;
        _901.y = (-1.0) - _926.y;
        _927 = _901;
    }
    else
    {
        _927 = _926;
    }
    vec4 _928;
    if (_577.x < 0.0)
    {
        vec4 _905 = _927;
        _905.z = (-1.0) - _927.z;
        _928 = _905;
    }
    else
    {
        _928 = _927;
    }
    vec4 _941;
    if (_577.z < 0.0)
    {
        vec4 _909 = _928;
        _909.w = (-1.0) - _928.w;
        _941 = _909;
    }
    else
    {
        _941 = _928;
    }
    vec4 _933;
    if (_589.x < 0.0)
    {
        vec4 _913 = _711;
        _913.x = (-1.0) - _723;
        _933 = _913;
    }
    else
    {
        _933 = _711;
    }
    vec4 _934;
    if (_589.z < 0.0)
    {
        vec4 _917 = _933;
        _917.y = (-1.0) - _933.y;
        _934 = _917;
    }
    else
    {
        _934 = _933;
    }
    vec4 _935;
    if (_601.x < 0.0)
    {
        vec4 _921 = _934;
        _921.z = (-1.0) - _934.z;
        _935 = _921;
    }
    else
    {
        _935 = _934;
    }
    vec4 _936;
    if (_601.z < 0.0)
    {
        vec4 _925 = _935;
        _925.w = (-1.0) - _935.w;
        _936 = _925;
    }
    else
    {
        _936 = _935;
    }
    _entryPointOutput_Position = _108.view_projection * v_Position;
    _entryPointOutput_TexCoord = v_TexCoord;
    _entryPointOutput_Fill = _941;
    _entryPointOutput_Border = _936;
    _entryPointOutput_FillCoord = v_FillCoord;
    _entryPointOutput_BorderCoord = v_BorderCoord;
    _entryPointOutput_Meta1 = v_Meta1;
    _entryPointOutput_Meta2 = v_Meta2;
    _entryPointOutput_Meta3 = v_Meta3;
    _entryPointOutput_Pos = vec4(v_Position.xy, v_ClipDist.xy);
    _entryPointOutput_ClipMeta = vec4(v_ClipDist.zw, v_ClipRoundAA);
}

