
#version 330
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

uniform sampler2D g_texture;

layout(location = 0) in vec2 input_ftcoord;
layout(location = 1) in vec2 input_fpos;
layout(location = 0) out vec4 _entryPointOutput;

mat4 spvWorkaroundRowMajor(mat4 wrap) { return wrap; }

void main()
{
    vec3 _270 = vec3(input_fpos, 1.0);
    vec2 _290 = fma(-(abs((_270 * mat3(spvWorkaroundRowMajor(_39.scissorMat)[0].xyz, spvWorkaroundRowMajor(_39.scissorMat)[1].xyz, spvWorkaroundRowMajor(_39.scissorMat)[2].xyz)).xy) - _39.scissorExt), _39.scissorScale, vec2(0.5));
    float _313 = min(1.0, (1.0 - abs(fma(input_ftcoord.x, 2.0, -1.0))) * _39.strokeMult) * min(1.0, input_ftcoord.y);
    if (_313 < _39.strokeThr)
    {
        discard;
    }
    vec4 _240 = texture(g_texture, (_270 * mat3(spvWorkaroundRowMajor(_39.paintMat)[0].xyz, spvWorkaroundRowMajor(_39.paintMat)[1].xyz, spvWorkaroundRowMajor(_39.paintMat)[2].xyz)).xy / _39.extent);
    float _244 = _240.w;
    _entryPointOutput = (vec4(_240.xyz * _244, _244) * _39.innerCol) * (_313 * (clamp(_290.x, 0.0, 1.0) * clamp(_290.y, 0.0, 1.0)));
}