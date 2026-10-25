
#version 330 core
#ifdef GL_ARB_shading_language_420pack
#extension GL_ARB_shading_language_420pack : require
#endif

layout(binding = 0, std140) uniform _Global
{
    layout(row_major) mat4 LightViewProj0;
    layout(row_major) mat4 LightViewProj1;
    layout(row_major) mat4 LightViewProj2;
    float DepthBias;
    float NumCascades;
    vec3 LightDirection;
    layout(row_major) mat4 WorldView;
    layout(row_major) mat4 WorldViewProj;
    vec3 FogColor;
    float FogDistance;
    float FogLogDensity;
} _30;

layout(location = 0) in vec4 Position;
layout(location = 1) in vec3 Color;
out vec4 varying_0;
out vec4 varying_1;
out float varying_2;

mat4 spvWorkaroundRowMajor(mat4 wrap) { return wrap; }

void main()
{
    gl_Position = spvWorkaroundRowMajor(_30.WorldViewProj) * Position;
    varying_0 = vec4(Color, 1.0);
    varying_1 = Position;
    varying_2 = length((spvWorkaroundRowMajor(_30.WorldView) * Position).xyz);
}