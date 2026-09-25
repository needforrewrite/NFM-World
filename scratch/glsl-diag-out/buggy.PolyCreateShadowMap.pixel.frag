
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
uniform mat4 _View;
uniform mat4 _Projection;
uniform mat4 _ViewProj;
uniform vec3 _SnapColor;
uniform uint _IsFullbright;
uniform uint _UseBaseColor;
uniform vec3 _BaseColor;
uniform vec3 _FogColor;
uniform float _FogDistance;
uniform float _g1_FogLogDensity;
uniform vec2 _g1_EnvironmentLight;
uniform vec3 _g1_CameraPosition;
uniform float _g1_Alpha;
uniform uint _g1_Expand;
uniform float _g1_RandomFloat;
uniform float _g1_Darken;




layout(location = 0) in float input_Depth;
layout(location = 1) out vec4 _entryPointOutput;

void main()
{
    _entryPointOutput = vec4(input_Depth, input_Depth, input_Depth, 1.0);
}