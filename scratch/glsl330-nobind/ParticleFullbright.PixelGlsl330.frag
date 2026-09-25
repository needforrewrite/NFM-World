
#version 330
#ifdef GL_ARB_shading_language_420pack
#extension GL_ARB_shading_language_420pack : require
#endif

layout(std140) uniform _Global
{
    layout(row_major) mat4 World;
    layout(row_major) mat4 View;
    layout(row_major) mat4 Projection;
} _39;

in vec4 varying_0;
layout(location = 0) out vec4 _entryPointOutput;

void main()
{
    _entryPointOutput = varying_0;
}