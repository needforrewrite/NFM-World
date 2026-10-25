cbuffer _Global : register(b0)
{
    column_major float4x4 _60_Projection : packoffset(c0);
};

Texture2D<float4> Texture : register(t0);
SamplerState TextureSampler : register(s0);

static float4 input_Color;
static float2 input_TexCoord;
static float4 _entryPointOutput;

struct SPIRV_Cross_Input
{
    float4 input_Color : TEXCOORD0;
    float2 input_TexCoord : TEXCOORD1;
};

struct SPIRV_Cross_Output
{
    float4 _entryPointOutput : SV_Target0;
};

void frag_main()
{
    _entryPointOutput = input_Color * Texture.Sample(TextureSampler, input_TexCoord);
}

SPIRV_Cross_Output main(SPIRV_Cross_Input stage_input)
{
    input_Color = stage_input.input_Color;
    input_TexCoord = stage_input.input_TexCoord;
    frag_main();
    SPIRV_Cross_Output stage_output;
    stage_output._entryPointOutput = _entryPointOutput;
    return stage_output;
}
