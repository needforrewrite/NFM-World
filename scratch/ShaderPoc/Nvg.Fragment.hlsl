cbuffer _Global : register(b0)
{
    column_major float4x4 _39_transformMat : packoffset(c0);
    column_major float4x4 _39_scissorMat : packoffset(c4);
    column_major float4x4 _39_paintMat : packoffset(c8);
    float4 _39_innerCol : packoffset(c12);
    float4 _39_outerCol : packoffset(c13);
    float2 _39_scissorExt : packoffset(c14);
    float2 _39_scissorScale : packoffset(c14.z);
    float2 _39_extent : packoffset(c15);
    float _39_radius : packoffset(c15.z);
    float _39_feather : packoffset(c15.w);
    float _39_strokeMult : packoffset(c16);
    float _39_strokeThr : packoffset(c16.y);
};

Texture2D<float4> g_texture : register(t0);
SamplerState g_textureSampler : register(s0);

static float2 input_ftcoord;
static float4 _entryPointOutput;

struct SPIRV_Cross_Input
{
    float2 input_ftcoord : TEXCOORD0;
};

struct SPIRV_Cross_Output
{
    float4 _entryPointOutput : SV_Target0;
};

void frag_main()
{
    if ((min(1.0f, (1.0f - abs((input_ftcoord.x * 2.0f) - 1.0f)) * _39_strokeMult) * min(1.0f, input_ftcoord.y)) < _39_strokeThr)
    {
        discard;
    }
    _entryPointOutput = 1.0f.xxxx;
}

SPIRV_Cross_Output main(SPIRV_Cross_Input stage_input)
{
    input_ftcoord = stage_input.input_ftcoord;
    frag_main();
    SPIRV_Cross_Output stage_output;
    stage_output._entryPointOutput = _entryPointOutput;
    return stage_output;
}
