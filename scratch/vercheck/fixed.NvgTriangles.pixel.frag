
#version 420
#ifdef GL_ARB_shading_language_420pack
#extension GL_ARB_shading_language_420pack : require
#endif

uniform mat4 _39_transformMat;
uniform mat4 _39_scissorMat;
uniform mat4 _39_paintMat;
uniform vec4 _39_innerCol;
uniform vec4 _39_outerCol;
uniform vec2 _39_scissorExt;
uniform vec2 _39_scissorScale;
uniform vec2 _39_extent;
uniform float _39_radius;
uniform float _39_feather;
uniform float _39_strokeMult;
uniform float _39_strokeThr;




uniform sampler2D g_texture;

layout(location = 0) in vec2 input_ftcoord;
layout(location = 1) in vec2 input_fpos;
layout(location = 0) out vec4 _entryPointOutput;

mat4 spvWorkaroundRowMajor(mat4 wrap) { return wrap; }

void main()
{
    vec2 _222 = fma(-(abs((vec3(input_fpos, 1.0) * mat3(spvWorkaroundRowMajor(_39_scissorMat)[0].xyz, spvWorkaroundRowMajor(_39_scissorMat)[1].xyz, spvWorkaroundRowMajor(_39_scissorMat)[2].xyz)).xy) - _39_scissorExt), _39_scissorScale, vec2(0.5));
    if ((min(1.0, (1.0 - abs(fma(input_ftcoord.x, 2.0, -1.0))) * _39_strokeMult) * min(1.0, input_ftcoord.y)) < _39_strokeThr)
    {
        discard;
    }
    _entryPointOutput = (texture(g_texture, input_ftcoord) * (clamp(_222.x, 0.0, 1.0) * clamp(_222.y, 0.0, 1.0))) * _39_innerCol;
}