#include <metal_stdlib>
#include <simd/simd.h>

using namespace metal;

struct _Global
{
    float4x4 Projection;
};

struct main0_out
{
    float4 _entryPointOutput [[color(0)]];
};

struct main0_in
{
    float4 input_Color [[user(locn0)]];
    float2 input_TexCoord [[user(locn1)]];
};

fragment main0_out main0(main0_in in [[stage_in]], texture2d<float> Texture [[texture(0)]], sampler TextureSampler [[sampler(0)]])
{
    main0_out out = {};
    out._entryPointOutput = in.input_Color * Texture.sample(TextureSampler, in.input_TexCoord);
    return out;
}

