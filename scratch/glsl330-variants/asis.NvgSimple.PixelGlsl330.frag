
#version 330 core
#ifdef GL_ARB_shading_language_420pack
#extension GL_ARB_shading_language_420pack : require
#endif

layout(binding = 0, std140) uniform _Global
{
    layout(row_major) mat4 transformMat;
    layout(row_major) mat4 scissorMat;
    layout(row_major) mat4 paintMat;
    vec4 innerCol;
    vec4 outerCol;
    vec2 scissorExt;
    vec2 scissorScale;
    vec2 extent;
    float radius;
    float feather;
    float strokeMult;
    float strokeThr;
} _39;

in vec2 varying_0;
layout(location = 0) out vec4 _entryPointOutput;

void main()
{
    if ((min(1.0, (1.0 - abs(varying_0.x * 2.0 + (-1.0))) * _39.strokeMult) * min(1.0, varying_0.y)) < _39.strokeThr)
    {
        discard;
    }
    _entryPointOutput = vec4(1.0);
}