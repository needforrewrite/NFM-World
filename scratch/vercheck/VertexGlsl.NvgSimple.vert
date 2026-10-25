
#version 330
#ifdef GL_ARB_shading_language_420pack
#extension GL_ARB_shading_language_420pack : require
#endif

struct _Global
{
    mat4 transformMat;
    mat4 scissorMat;
    mat4 paintMat;
    vec4 innerCol;
    vec4 outerCol;
    vec2 scissorExt;
    vec2 scissorScale;
    vec2 extent;
    float radius;
    float feather;
    float strokeMult;
    float strokeThr;
};

uniform _Global _30;

layout(location = 0) in vec2 pt;
layout(location = 1) in vec2 tex;
layout(location = 0) out vec2 _entryPointOutput_ftcoord;
layout(location = 1) out vec2 _entryPointOutput_fpos;

mat4 spvWorkaroundRowMajor(mat4 wrap) { return wrap; }

void main()
{
    gl_Position = spvWorkaroundRowMajor(_30.transformMat) * vec4(pt, 0.0, 1.0);
    _entryPointOutput_ftcoord = tex;
    _entryPointOutput_fpos = pt;
}