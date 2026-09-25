
#version 420
#ifdef GL_ARB_shading_language_420pack
#extension GL_ARB_shading_language_420pack : require
#endif

uniform mat4 _20_World;
uniform mat4 _20_View;
uniform mat4 _20_Projection;




layout(location = 0) in vec4 Position;
layout(location = 1) in vec4 Color;
layout(location = 0) out vec4 _entryPointOutput_Color;

mat4 spvWorkaroundRowMajor(mat4 wrap) { return wrap; }

void main()
{
    gl_Position = spvWorkaroundRowMajor(_20_Projection) * (spvWorkaroundRowMajor(_20_View) * (spvWorkaroundRowMajor(_20_World) * Position));
    _entryPointOutput_Color = Color;
}