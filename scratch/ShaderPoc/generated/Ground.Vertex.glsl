#version 410
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
    mat4 WorldView;
    mat4 WorldViewProj;
    vec3 FogColor;
    float FogDistance;
    float FogLogDensity;
};

uniform _Global _30;

layout(location = 0) in vec4 Position;
layout(location = 1) in vec3 Color;
layout(location = 0) out vec4 _entryPointOutput_Color;
layout(location = 1) out vec4 _entryPointOutput_WorldPos;
layout(location = 2) out float _entryPointOutput_ViewLength;

mat4 spvWorkaroundRowMajor(mat4 wrap) { return wrap; }

void main()
{
    gl_Position = spvWorkaroundRowMajor(_30.WorldViewProj) * Position;
    _entryPointOutput_Color = vec4(Color, 1.0);
    _entryPointOutput_WorldPos = Position;
    _entryPointOutput_ViewLength = length((spvWorkaroundRowMajor(_30.WorldView) * Position).xyz);
}

