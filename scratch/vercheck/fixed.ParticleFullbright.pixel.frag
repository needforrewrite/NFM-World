
#version 420
#ifdef GL_ARB_shading_language_420pack
#extension GL_ARB_shading_language_420pack : require
#endif

uniform mat4 _39_World;
uniform mat4 _39_View;
uniform mat4 _39_Projection;




layout(location = 0) in vec4 input_Color;
layout(location = 0) out vec4 _entryPointOutput;

void main()
{
    _entryPointOutput = input_Color;
}