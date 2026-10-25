
#version 410
#ifdef GL_ARB_shading_language_420pack
#extension GL_ARB_shading_language_420pack : require
#endif

uniform mat4 _30_transformMat;
uniform mat4 _30_scissorMat;
uniform mat4 _30_paintMat;
uniform vec4 _30_innerCol;
uniform vec4 _30_outerCol;
uniform vec2 _30_scissorExt;
uniform vec2 _30_scissorScale;
uniform vec2 _30_extent;
uniform float _30_radius;
uniform float _30_feather;
uniform float _30_strokeMult;
uniform float _30_strokeThr;




layout(location = 0) in vec2 pt;
layout(location = 1) in vec2 tex;
layout(location = 0) out vec2 _entryPointOutput_ftcoord;
layout(location = 1) out vec2 _entryPointOutput_fpos;

mat4 spvWorkaroundRowMajor(mat4 wrap) { return wrap; }

void main()
{
    gl_Position = spvWorkaroundRowMajor(_30_transformMat) * vec4(pt, 0.0, 1.0);
    _entryPointOutput_ftcoord = tex;
    _entryPointOutput_fpos = pt;
}