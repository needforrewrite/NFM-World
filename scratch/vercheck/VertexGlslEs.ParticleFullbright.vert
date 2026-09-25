
#version 330

layout(std140) uniform _Global
{
    layout(row_major) mat4 World;
    layout(row_major) mat4 View;
    layout(row_major) mat4 Projection;
} _20;

layout(location = 0) in vec4 Position;
layout(location = 1) in vec4 Color;
out vec4 varying_0;

highp mat4 spvWorkaroundRowMajor(highp mat4 wrap) { return wrap; }
mediump mat4 spvWorkaroundRowMajorMP(mediump mat4 wrap) { return wrap; }

void main()
{
    gl_Position = spvWorkaroundRowMajor(_20.Projection) * (spvWorkaroundRowMajor(_20.View) * (spvWorkaroundRowMajor(_20.World) * Position));
    varying_0 = Color;
}