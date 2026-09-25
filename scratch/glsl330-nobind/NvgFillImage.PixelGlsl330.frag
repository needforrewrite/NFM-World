
#version 330
#ifdef GL_ARB_shading_language_420pack
#extension GL_ARB_shading_language_420pack : require
#endif

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
} _39;

uniform sampler2D g_texture;

in vec2 varying_0;
in vec2 varying_1;
layout(location = 0) out vec4 _entryPointOutput;

mat4 spvWorkaroundRowMajor(mat4 wrap) { return wrap; }

void main()
{
    vec3 _270 = vec3(varying_1, 1.0);
    vec2 _290 = (-(abs((_270 * mat3(spvWorkaroundRowMajor(_39.scissorMat)[0].xyz, spvWorkaroundRowMajor(_39.scissorMat)[1].xyz, spvWorkaroundRowMajor(_39.scissorMat)[2].xyz)).xy) - _39.scissorExt)) * _39.scissorScale + vec2(0.5);
    float _313 = min(1.0, (1.0 - abs(varying_0.x * 2.0 + (-1.0))) * _39.strokeMult) * min(1.0, varying_0.y);
    if (_313 < _39.strokeThr)
    {
        discard;
    }
    vec4 _240 = texture(g_texture, (_270 * mat3(spvWorkaroundRowMajor(_39.paintMat)[0].xyz, spvWorkaroundRowMajor(_39.paintMat)[1].xyz, spvWorkaroundRowMajor(_39.paintMat)[2].xyz)).xy / _39.extent);
    float _244 = _240.w;
    _entryPointOutput = (vec4(_240.xyz * _244, _244) * _39.innerCol) * (_313 * (clamp(_290.x, 0.0, 1.0) * clamp(_290.y, 0.0, 1.0)));
}