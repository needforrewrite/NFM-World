// ImGui fragment shader for MoonWorks (ShaderCross HLSL)

Texture2D g_texture : register(t0, space2);
SamplerState g_sampler : register(s0, space2);

struct PSInput
{
    float4 Position : SV_Position;
    float2 TexCoord : TEXCOORD0;
    float4 Color    : TEXCOORD1;
};

float4 main(PSInput input) : SV_TARGET
{
    return input.Color * g_texture.Sample(g_sampler, input.TexCoord);
}
