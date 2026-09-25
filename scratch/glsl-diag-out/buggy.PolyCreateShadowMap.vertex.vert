
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




layout(location = 0) in vec3 input_Position;
layout(location = 5) in mat4 world;
layout(location = 0) out float _entryPointOutput_Depth;

mat4 spvWorkaroundRowMajor(mat4 wrap) { return wrap; }

void main()
{
    vec4 _132 = spvWorkaroundRowMajor(_32_Projection) * (spvWorkaroundRowMajor(_32_View) * (world * vec4(input_Position, 1.0)));
    gl_Position = _132;
    _entryPointOutput_Depth = _132.z / _132.w;
}