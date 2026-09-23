#version 300 es

layout(std140) uniform _Global
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
} _67;

layout(location = 0) in vec4 Position;
layout(location = 1) in vec3 Color;
out vec4 varying_0;
out vec4 varying_1;

highp mat4 spvWorkaroundRowMajor(highp mat4 wrap) { return wrap; }
mediump mat4 spvWorkaroundRowMajorMP(mediump mat4 wrap) { return wrap; }

void main()
{
    float _216 = exp2(max((length((spvWorkaroundRowMajor(_67.WorldView) * Position).xyz) - (_67.FogDistance * 0.5)) / _67.FogDistance, 0.0) * _67.FogLogDensity);
    gl_Position = spvWorkaroundRowMajor(_67.WorldViewProj) * Position;
    varying_0 = vec4((Color * vec3(_216)) + (_67.FogColor * vec3(1.0 - _216)), 1.0);
    varying_1 = Position;
}

