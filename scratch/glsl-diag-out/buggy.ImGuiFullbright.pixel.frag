
#version 410
#ifdef GL_ARB_shading_language_420pack
#extension GL_ARB_shading_language_420pack : require
#endif

uniform mat4 _Projection;




uniform sampler2D Texture;

layout(location = 0) in vec4 input_Color;
layout(location = 1) in vec2 input_TexCoord;
layout(location = 0) out vec4 _entryPointOutput;

void main()
{
    _entryPointOutput = input_Color * texture(Texture, input_TexCoord);
}