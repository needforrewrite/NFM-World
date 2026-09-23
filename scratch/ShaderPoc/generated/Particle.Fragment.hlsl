cbuffer _Global : register(b0)
{
    column_major float4x4 _39_World : packoffset(c0);
    column_major float4x4 _39_View : packoffset(c4);
    column_major float4x4 _39_Projection : packoffset(c8);
};


static float4 input_Color;
static float4 _entryPointOutput;

struct SPIRV_Cross_Input
{
    float4 input_Color : TEXCOORD0;
};

struct SPIRV_Cross_Output
{
    float4 _entryPointOutput : SV_Target0;
};

void frag_main()
{
    _entryPointOutput = input_Color;
}

SPIRV_Cross_Output main(SPIRV_Cross_Input stage_input)
{
    input_Color = stage_input.input_Color;
    frag_main();
    SPIRV_Cross_Output stage_output;
    stage_output._entryPointOutput = _entryPointOutput;
    return stage_output;
}
