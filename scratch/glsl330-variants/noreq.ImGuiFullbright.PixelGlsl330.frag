
#version 330 core

layout(binding = 0, std140) uniform _Global
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