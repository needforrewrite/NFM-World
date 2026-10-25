#version 330

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
} _71;

in vec2 varying_0;
in vec2 varying_1;
layout(location = 0) out vec4 _entryPointOutput;

mat4 spvWorkaroundRowMajor(mat4 wrap) { return wrap; }

void main()
{
    vec2 _326 = vec2(0.5) - ((abs((vec3(varying_1.x, varying_1.y, 1.0) * mat3(spvWorkaroundRowMajor(_71.scissorMat)[0].xyz, spvWorkaroundRowMajor(_71.scissorMat)[1].xyz, spvWorkaroundRowMajor(_71.scissorMat)[2].xyz)).xy) - _71.scissorExt) * _71.scissorScale);
    float _349 = min(1.0, (1.0 - abs((varying_0.x * 2.0) - 1.0)) * _71.strokeMult) * min(1.0, varying_0.y);
    if (_349 < _71.strokeThr)
    {
        discard;
    }
    vec2 _361 = abs((vec3(varying_1, 1.0) * mat3(spvWorkaroundRowMajor(_71.paintMat)[0].xyz, spvWorkaroundRowMajor(_71.paintMat)[1].xyz, spvWorkaroundRowMajor(_71.paintMat)[2].xyz)).xy) - (_71.extent - vec2(_71.radius));
    _entryPointOutput = mix(_71.innerCol, _71.outerCol, vec4(clamp((((min(max(_361.x, _361.y), 0.0) + length(max(_361, vec2(0.0)))) - _71.radius) + (_71.feather * 0.5)) / _71.feather, 0.0, 1.0))) * (_349 * (clamp(_326.x, 0.0, 1.0) * clamp(_326.y, 0.0, 1.0)));
}
