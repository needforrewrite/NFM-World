
#version 410
#ifdef GL_ARB_shading_language_420pack
#extension GL_ARB_shading_language_420pack : require
#endif

uniform mat4 _30_LightViewProj0;
uniform mat4 _30_LightViewProj1;
uniform mat4 _30_LightViewProj2;
uniform float _30_DepthBias;
uniform float _30_NumCascades;
uniform vec3 _30_LightDirection;
uniform mat4 _30_WorldView;
uniform mat4 _30_WorldViewProj;
uniform vec3 _30_FogColor;
uniform float _30_FogDistance;
uniform float _30_FogLogDensity;




layout(location = 0) in vec4 Position;
layout(location = 1) in vec3 Color;
layout(location = 0) out vec4 _entryPointOutput_Color;
layout(location = 1) out vec4 _entryPointOutput_WorldPos;
layout(location = 2) out float _entryPointOutput_ViewLength;

mat4 spvWorkaroundRowMajor(mat4 wrap) { return wrap; }

void main()
{
    gl_Position = spvWorkaroundRowMajor(_30_WorldViewProj) * Position;
    _entryPointOutput_Color = vec4(Color, 1.0);
    _entryPointOutput_WorldPos = Position;
    _entryPointOutput_ViewLength = length((spvWorkaroundRowMajor(_30_WorldView) * Position).xyz);
}