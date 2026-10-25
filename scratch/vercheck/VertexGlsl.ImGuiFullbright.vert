
#version 330
#ifdef GL_ARB_shading_language_420pack
#extension GL_ARB_shading_language_420pack : require
#endif

struct _Global
{
    mat4 Projection;
};

uniform _Global _24;

layout(location = 0) in vec2 input_Position;
layout(location = 1) in vec2 input_TexCoord;
layout(location = 2) in vec4 input_Color;
layout(location = 0) out vec4 _entryPointOutput_Color;
layout(location = 1) out vec2 _entryPointOutput_TexCoord;

mat4 spvWorkaroundRowMajor(mat4 wrap) { return wrap; }

void main()
{
    gl_Position = spvWorkaroundRowMajor(_24.Projection) * vec4(input_Position, 0.0, 1.0);
    _entryPointOutput_Color = input_Color;
    _entryPointOutput_TexCoord = input_TexCoord;
}