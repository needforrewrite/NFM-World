// Line.frag.hlsl — Line fragment shader (replaces Line.fx "Basic" technique PS)

#include "Mad.hlsli"
#include "SDLGPU.hlsli"

// ─── Resources (fragment stage) ─────────────────────────────────────────────

Texture2D      ShadowMap0        : SDL_PS_TEXTURE(0);
SamplerState   ShadowMapSampler0 : SDL_PS_SAMPLER(0);
Texture2D      ShadowMap1        : SDL_PS_TEXTURE(1);
SamplerState   ShadowMapSampler1 : SDL_PS_SAMPLER(1);
Texture2D      ShadowMap2        : SDL_PS_TEXTURE(2);
SamplerState   ShadowMapSampler2 : SDL_PS_SAMPLER(2);

cbuffer LineFragUniforms : SDL_PS_UNIFORM(0)
{
    ShadowParams Shadow;
};

struct PSInput
{
    float4 Position     : SV_POSITION;
    float4 Color        : COLOR0;
    float4 WorldPos     : ATTRIBUTE(2);
    float  GetsShadowed : ATTRIBUTE(3);
    float3 WorldNormal  : ATTRIBUTE(4);
};

float4 main(PSInput input) : SV_TARGET
{
    float4 diffuse = input.Color;

    if (input.GetsShadowed > 0.0)
    {
        float3 diffuseRGB = diffuse.xyz;
        PS_ApplyShadowing(diffuseRGB, input.WorldPos, input.WorldNormal, Shadow,
            ShadowMap0, ShadowMapSampler0,
            ShadowMap1, ShadowMapSampler1,
            ShadowMap2, ShadowMapSampler2);
        diffuse = float4(diffuseRGB, diffuse.w);
    }

    return diffuse;
}
