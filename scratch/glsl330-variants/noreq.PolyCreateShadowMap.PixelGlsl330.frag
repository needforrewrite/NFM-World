
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
} _51;

in float varying_0;
layout(location = 1) out vec4 _entryPointOutput;

void main()
{
    _entryPointOutput = vec4(varying_0, varying_0, varying_0, 1.0);
}