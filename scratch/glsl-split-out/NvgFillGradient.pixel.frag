
#version 410
#ifdef GL_ARB_shading_language_420pack
#extension GL_ARB_shading_language_420pack : require
#endif

uniform mat4 _72_transformMat;
uniform mat4 _72_scissorMat;
uniform mat4 _72_paintMat;
uniform vec4 _72_innerCol;
uniform vec4 _72_outerCol;
uniform vec2 _72_scissorExt;
uniform vec2 _72_scissorScale;
uniform vec2 _72_extent;
uniform float _72_radius;
uniform float _72_feather;
uniform float _72_strokeMult;
uniform float _72_strokeThr;




layout(location = 0) in vec2 input_ftcoord;
layout(location = 1) in vec2 input_fpos;
layout(location = 0) out vec4 _entryPointOutput;

mat4 spvWorkaroundRowMajor(mat4 wrap) { return wrap; }

void main()
{
    vec3 _307 = vec3(input_fpos, 1.0);
    vec2 _327 = fma(-(abs((_307 * mat3(spvWorkaroundRowMajor(_72_scissorMat)[0].xyz, spvWorkaroundRowMajor(_72_scissorMat)[1].xyz, spvWorkaroundRowMajor(_72_scissorMat)[2].xyz)).xy) - _72_scissorExt), _72_scissorScale, vec2(0.5));
    float _350 = min(1.0, (1.0 - abs(fma(input_ftcoord.x, 2.0, -1.0))) * _72_strokeMult) * min(1.0, input_ftcoord.y);
    if (_350 < _72_strokeThr)
    {
        discard;
    }
    vec2 _363 = abs((_307 * mat3(spvWorkaroundRowMajor(_72_paintMat)[0].xyz, spvWorkaroundRowMajor(_72_paintMat)[1].xyz, spvWorkaroundRowMajor(_72_paintMat)[2].xyz)).xy) - (_72_extent - vec2(_72_radius));
    _entryPointOutput = mix(_72_innerCol, _72_outerCol, vec4(clamp(fma(_72_feather, 0.5, (min(max(_363.x, _363.y), 0.0) + length(max(_363, vec2(0.0)))) - _72_radius) / _72_feather, 0.0, 1.0))) * (_350 * (clamp(_327.x, 0.0, 1.0) * clamp(_327.y, 0.0, 1.0)));
}