
#version 330
#ifdef GL_ARB_shading_language_420pack
#extension GL_ARB_shading_language_420pack : require
#endif

layout(std140) uniform _Global
{
    layout(row_major) mat4 Projection;
} _60;

uniform sampler2D Texture;

in vec4 varying_0;
in vec2 varying_1;
layout(location = 0) out vec4 _entryPointOutput;

void main()
{
    _entryPointOutput = varying_0 * texture(Texture, varying_1);
}