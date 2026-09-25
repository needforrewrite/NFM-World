
#version 330 core

layout(binding = 0, std140) uniform _Global
{
    layout(row_major) mat4 LightViewProj0;
    layout(row_major) mat4 LightViewProj1;
    layout(row_major) mat4 LightViewProj2;
    float DepthBias;
    float NumCascades;
    vec3 LightDirection;
    layout(row_major) mat4 View;
    layout(row_major) mat4 Projection;
    layout(row_major) mat4 ViewProj;
    vec3 SnapColor;
    uint IsFullbright;
    uint UseBaseColor;
    vec3 BaseColor;
    vec3 FogColor;
    float FogDistance;
    float FogLogDensity;
    vec2 EnvironmentLight;
    vec3 CameraPosition;
    float Alpha;
    uint Expand;
    float RandomFloat;
    float Darken;
} _32;

layout(location = 0) in vec3 input_Position;
layout(location = 5) in mat4 world;
out float varying_0;

mat4 spvWorkaroundRowMajor(mat4 wrap) { return wrap; }

void main()
{
    vec4 _132 = spvWorkaroundRowMajor(_32.Projection) * (spvWorkaroundRowMajor(_32.View) * (world * vec4(input_Position, 1.0)));
    gl_Position = _132;
    varying_0 = _132.z / _132.w;
}