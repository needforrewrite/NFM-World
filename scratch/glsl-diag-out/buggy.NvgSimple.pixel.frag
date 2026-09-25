
#version 410
#ifdef GL_ARB_shading_language_420pack
#extension GL_ARB_shading_language_420pack : require
#endif

uniform mat4 _transformMat;
uniform mat4 _scissorMat;
uniform mat4 _paintMat;
uniform vec4 _innerCol;
uniform vec4 _outerCol;
uniform vec2 _scissorExt;
uniform vec2 _scissorScale;
uniform vec2 _extent;
uniform float _radius;
uniform float _feather;
uniform float _strokeMult;
uniform float _strokeThr;




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