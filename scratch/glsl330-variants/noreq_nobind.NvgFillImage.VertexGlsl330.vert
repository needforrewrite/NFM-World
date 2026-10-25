
#version 330 core

layout(std140) uniform _Global
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
} _30;

layout(location = 0) in vec2 pt;
layout(location = 1) in vec2 tex;
out vec2 varying_0;
out vec2 varying_1;

mat4 spvWorkaroundRowMajor(mat4 wrap) { return wrap; }

void main()
{
    gl_Position = spvWorkaroundRowMajor(_30.transformMat) * vec4(pt, 0.0, 1.0);
    varying_0 = tex;
    varying_1 = pt;
}