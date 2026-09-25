
#version 330 core

layout(binding = 0, std140) uniform _Global
{
    layout(row_major) mat4 WorldViewProj;
} _39;

in vec4 varying_0;
layout(location = 0) out vec4 _entryPointOutput;

void main()
{
    _entryPointOutput = varying_0;
}