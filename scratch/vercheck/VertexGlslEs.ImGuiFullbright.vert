
#version 330

layout(std140) uniform _Global
{
    layout(row_major) mat4 Projection;
} _24;

layout(location = 0) in vec2 input_Position;
layout(location = 1) in vec2 input_TexCoord;
layout(location = 2) in vec4 input_Color;
out vec4 varying_0;
out vec2 varying_1;

highp mat4 spvWorkaroundRowMajor(highp mat4 wrap) { return wrap; }
mediump mat4 spvWorkaroundRowMajorMP(mediump mat4 wrap) { return wrap; }

void main()
{
    gl_Position = spvWorkaroundRowMajor(_24.Projection) * vec4(input_Position, 0.0, 1.0);
    varying_0 = input_Color;
    varying_1 = input_TexCoord;
}