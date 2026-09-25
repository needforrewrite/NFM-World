
#version 330
precision highp float;
precision highp int;

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
} _72;

in vec2 varying_0;
in vec2 varying_1;
layout(location = 0) out vec4 _entryPointOutput;

highp mat4 spvWorkaroundRowMajor(highp mat4 wrap) { return wrap; }
mediump mat4 spvWorkaroundRowMajorMP(mediump mat4 wrap) { return wrap; }

void main()
{
    vec3 _307 = vec3(varying_1, 1.0);
    vec2 _327 = (-(abs((_307 * mat3(spvWorkaroundRowMajor(_72.scissorMat)[0].xyz, spvWorkaroundRowMajor(_72.scissorMat)[1].xyz, spvWorkaroundRowMajor(_72.scissorMat)[2].xyz)).xy) - _72.scissorExt)) * _72.scissorScale + vec2(0.5);
    float _350 = min(1.0, (1.0 - abs(varying_0.x * 2.0 + (-1.0))) * _72.strokeMult) * min(1.0, varying_0.y);
    if (_350 < _72.strokeThr)
    {
        discard;
    }
    vec2 _363 = abs((_307 * mat3(spvWorkaroundRowMajor(_72.paintMat)[0].xyz, spvWorkaroundRowMajor(_72.paintMat)[1].xyz, spvWorkaroundRowMajor(_72.paintMat)[2].xyz)).xy) - (_72.extent - vec2(_72.radius));
    _entryPointOutput = mix(_72.innerCol, _72.outerCol, vec4(clamp((_72.feather * 0.5 + ((min(max(_363.x, _363.y), 0.0) + length(max(_363, vec2(0.0)))) - _72.radius)) / _72.feather, 0.0, 1.0))) * (_350 * (clamp(_327.x, 0.0, 1.0) * clamp(_327.y, 0.0, 1.0)));
}