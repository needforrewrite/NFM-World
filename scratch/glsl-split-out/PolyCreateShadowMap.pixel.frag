
#version 410
#ifdef GL_ARB_shading_language_420pack
#extension GL_ARB_shading_language_420pack : require
#endif

uniform mat4 _51_LightViewProj0;
uniform mat4 _51_LightViewProj1;
uniform mat4 _51_LightViewProj2;
uniform float _51_DepthBias;
uniform float _51_NumCascades;
uniform vec3 _51_LightDirection;
uniform mat4 _51_View;
uniform mat4 _51_Projection;
uniform mat4 _51_ViewProj;
uniform vec3 _51_SnapColor;
uniform uint _51_IsFullbright;
uniform uint _51_UseBaseColor;
uniform vec3 _51_BaseColor;
uniform vec3 _51_FogColor;
uniform float _51_FogDistance;
uniform float _51_g1_FogLogDensity;
uniform vec2 _51_g1_EnvironmentLight;
uniform vec3 _51_g1_CameraPosition;
uniform float _51_g1_Alpha;
uniform uint _51_g1_Expand;
uniform float _51_g1_RandomFloat;
uniform float _51_g1_Darken;




layout(location = 0) in float input_Depth;
layout(location = 1) out vec4 _entryPointOutput;

void main()
{
    _entryPointOutput = vec4(input_Depth, input_Depth, input_Depth, 1.0);
}