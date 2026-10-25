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

uniform _Global _67;

layout(location = 0) in vec4 Position;
layout(location = 1) in vec3 Color;
layout(location = 0) out vec4 _entryPointOutput_Color;
layout(location = 1) out vec4 _entryPointOutput_WorldPos;

mat4 spvWorkaroundRowMajor(mat4 wrap) { return wrap; }

void main()
{
    float _216 = exp2(max((length((spvWorkaroundRowMajor(_67.WorldView) * Position).xyz) - (_67.FogDistance * 0.5)) / _67.FogDistance, 0.0) * _67.FogLogDensity);
    gl_Position = spvWorkaroundRowMajor(_67.WorldViewProj) * Position;
    _entryPointOutput_Color = vec4((Color * vec3(_216)) + (_67.FogColor * vec3(1.0 - _216)), 1.0);
    _entryPointOutput_WorldPos = Position;
}

