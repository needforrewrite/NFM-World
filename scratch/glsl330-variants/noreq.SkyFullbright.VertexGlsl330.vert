
#version 330 core

layout(binding = 0, std140) uniform _Global
{
    layout(row_major) mat4 WorldViewProj;
} _23;

layout(location = 0) in vec4 Position;
layout(location = 1) in vec4 Color;
out vec4 varying_0;

mat4 spvWorkaroundRowMajor(mat4 wrap) { return wrap; }

void main()
{
    gl_Position = spvWorkaroundRowMajor(_23.WorldViewProj) * Position;
    varying_0 = Color;
}