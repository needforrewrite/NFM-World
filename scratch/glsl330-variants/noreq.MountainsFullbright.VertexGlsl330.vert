
#version 330 core

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
} _69;

layout(location = 0) in vec4 Position;
layout(location = 1) in vec3 Color;
out vec4 varying_0;
out vec4 varying_1;

mat4 spvWorkaroundRowMajor(mat4 wrap) { return wrap; }

void main()
{
    float _218 = exp2(max(((-_69.FogDistance) * 0.5 + length((spvWorkaroundRowMajor(_69.WorldView) * Position).xyz)) / _69.FogDistance, 0.0) * _69.FogLogDensity);
    gl_Position = spvWorkaroundRowMajor(_69.WorldViewProj) * Position;
    varying_0 = vec4(Color * vec3(_218) + (_69.FogColor * vec3(1.0 - _218)), 1.0);
    varying_1 = Position;
}