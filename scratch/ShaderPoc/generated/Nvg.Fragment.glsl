#version 410
#ifdef GL_ARB_shading_language_420pack
#extension GL_ARB_shading_language_420pack : require
#endif

struct _Global
{
    mat4 transformMat;
    mat4 scissorMat;
    mat4 paintMat;
    vec4 innerCol;
    vec4 outerCol;
    vec2 scissorExt;
    vec2 scissorScale;
    vec2 extent;
    float radius;
    float feather;
    float strokeMult;
    float strokeThr;
};

uniform _Global _39;

layout(location = 0) in vec2 input_ftcoord;
layout(location = 0) out vec4 _entryPointOutput;

void main()
{
    if ((min(1.0, (1.0 - abs((input_ftcoord.x * 2.0) - 1.0)) * _39.strokeMult) * min(1.0, input_ftcoord.y)) < _39.strokeThr)
    {
        discard;
    }
    _entryPointOutput = vec4(1.0);
}

