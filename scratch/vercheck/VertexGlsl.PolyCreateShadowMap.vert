
#version 330
#ifdef GL_ARB_shading_language_420pack
#extension GL_ARB_shading_language_420pack : require
#endif

struct _Global
{
    mat4 LightViewProj0;
    mat4 LightViewProj1;
    mat4 LightViewProj2;
    float DepthBias;
    float NumCascades;
    vec3 LightDirection;
    mat4 View;
    mat4 Projection;
    mat4 ViewProj;
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
};

uniform _Global _32;

layout(location = 0) in vec3 input_Position;
layout(location = 5) in mat4 world;
layout(location = 0) out float _entryPointOutput_Depth;

mat4 spvWorkaroundRowMajor(mat4 wrap) { return wrap; }

void main()
{
    vec4 _132 = spvWorkaroundRowMajor(_32.Projection) * (spvWorkaroundRowMajor(_32.View) * (world * vec4(input_Position, 1.0)));
    gl_Position = _132;
    _entryPointOutput_Depth = _132.z / _132.w;
}