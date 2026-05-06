// Mountains.frag.hlsl — Mountains fragment shader (replaces Mountains.fx "Fullbright" technique PS)

#include "Mad.hlsli"
#include "SDLGPU.hlsli"

// ─── Resources (fragment stage) ─────────────────────────────────────────────

Texture2D      ShadowMap0        : SDL_PS_TEXTURE(0);
SamplerState   ShadowMapSampler0 : SDL_PS_SAMPLER(0);
Texture2D      ShadowMap1        : SDL_PS_TEXTURE(1);
SamplerState   ShadowMapSampler1 : SDL_PS_SAMPLER(1);
Texture2D      ShadowMap2        : SDL_PS_TEXTURE(2);
SamplerState   ShadowMapSampler2 : SDL_PS_SAMPLER(2);

cbuffer MountainsFragUniforms : SDL_PS_UNIFORM(0)
{
    ShadowParams Shadow;
};

struct PSInput
{
    float4 Position : SV_POSITION;
    float4 Color    : COLOR0;
    float4 WorldPos : ATTRIBUTE(2);
};

float4 main(PSInput input) : SV_TARGET
{
    float3 diffuse = input.Color.xyz;

    PS_ApplyShadowing(diffuse, float4(input.WorldPos.xyz, 1), Shadow,
        ShadowMap0, ShadowMapSampler0,
        ShadowMap1, ShadowMapSampler1,
        ShadowMap2, ShadowMapSampler2);

    return float4(diffuse, input.Color.w);
}
