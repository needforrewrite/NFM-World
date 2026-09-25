
#version 410
#ifdef GL_ARB_shading_language_420pack
#extension GL_ARB_shading_language_420pack : require
#endif

uniform mat4 _LightViewProj0;
uniform mat4 _LightViewProj1;
uniform mat4 _LightViewProj2;
uniform float _DepthBias;
uniform float _NumCascades;
uniform vec3 _LightDirection;
uniform mat4 _WorldView;
uniform mat4 _WorldViewProj;
uniform vec3 _FogColor;
uniform float _FogDistance;
uniform float _FogLogDensity;




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