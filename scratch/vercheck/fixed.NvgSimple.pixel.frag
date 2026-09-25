
#version 420
#ifdef GL_ARB_shading_language_420pack
#extension GL_ARB_shading_language_420pack : require
#endif

uniform mat4 _39_transformMat;
uniform mat4 _39_scissorMat;
uniform mat4 _39_paintMat;
uniform vec4 _39_innerCol;
uniform vec4 _39_outerCol;
uniform vec2 _39_scissorExt;
uniform vec2 _39_scissorScale;
uniform vec2 _39_extent;
uniform float _39_radius;
uniform float _39_feather;
uniform float _39_strokeMult;
uniform float _39_strokeThr;




layout(location = 0) in vec2 input_ftcoord;
layout(location = 0) out vec4 _entryPointOutput;

void main()
{
    if ((min(1.0, (1.0 - abs(fma(input_ftcoord.x, 2.0, -1.0))) * _39_strokeMult) * min(1.0, input_ftcoord.y)) < _39_strokeThr)
    {
        discard;
    }
    _entryPointOutput = vec4(1.0);
}