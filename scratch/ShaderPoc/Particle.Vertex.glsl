#version 410
#ifdef GL_ARB_shading_language_420pack
#extension GL_ARB_shading_language_420pack : require
#endif

struct _Global
{
    mat4 World;
    mat4 View;
    mat4 Projection;
};

uniform _Global _20;

layout(location = 0) in vec4 Position;
layout(location = 1) in vec4 Color;
layout(location = 0) out vec4 _entryPointOutput_Color;

mat4 spvWorkaroundRowMajor(mat4 wrap) { return wrap; }

void main()
{
    gl_Position = spvWorkaroundRowMajor(_20.Projection) * (spvWorkaroundRowMajor(_20.View) * (spvWorkaroundRowMajor(_20.World) * Position));
    _entryPointOutput_Color = Color;
}

