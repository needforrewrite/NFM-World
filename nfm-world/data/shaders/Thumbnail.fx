float2 TexelSize;

#if SM6
Texture2D Texture : register(t0);
SamplerState TextureSampler : register(s0);
#else
texture Texture;
sampler TextureSampler = sampler_state
{
    Texture = <Texture>;
    MinFilter = LINEAR;
    MagFilter = LINEAR;
    AddressU = CLAMP;
    AddressV = CLAMP;
};
#endif

struct VertexInput
{
    float2 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
};

struct VertexOutput
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
};

VertexOutput MainVS(VertexInput input)
{
    VertexOutput output;
    output.Position = float4(input.Position, 0, 1);
    output.TexCoord = input.TexCoord;
    return output;
}

float4 SampleSource(float2 uv)
{
#if SM6
    return Texture.Sample(TextureSampler, uv);
#else
    return tex2D(TextureSampler, uv);
#endif
}

float4 MainPS(VertexOutput input) : SV_TARGET
{
    // Four bilinear samples average a 4x4 block of source texels.
    return (SampleSource(input.TexCoord + TexelSize * float2(-1, -1))
          + SampleSource(input.TexCoord + TexelSize * float2(1, -1))
          + SampleSource(input.TexCoord + TexelSize * float2(-1, 1))
          + SampleSource(input.TexCoord + TexelSize * float2(1, 1))) * 0.25;
}

technique Downsample
{
    pass Pass0
    {
        VertexShader = compile vs_3_0 MainVS();
        PixelShader = compile ps_3_0 MainPS();
    }
};
