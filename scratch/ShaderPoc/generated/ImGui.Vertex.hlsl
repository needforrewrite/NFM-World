cbuffer _Global : register(b0)
{
    column_major float4x4 _24_Projection : packoffset(c0);
};

Texture2D<float4> Texture : register(t0);
SamplerState TextureSampler : register(s0);

static float4 gl_Position;
static float2 input_Position;
static float2 input_TexCoord;
static float4 input_Color;
static float4 _entryPointOutput_Color;
static float2 _entryPointOutput_TexCoord;

struct SPIRV_Cross_Input
{
    float2 input_Position : POSITION0;
    float2 input_TexCoord : TEXCOORD0;
    float4 input_Color : COLOR0;
};

struct SPIRV_Cross_Output
{
    float4 _entryPointOutput_Color : TEXCOORD0;
    float2 _entryPointOutput_TexCoord : TEXCOORD1;
    float4 gl_Position : SV_Position;
};

void vert_main()
{
    gl_Position = mul(float4(input_Position, 0.0f, 1.0f), _24_Projection);
    _entryPointOutput_Color = input_Color;
    _entryPointOutput_TexCoord = input_TexCoord;
}

SPIRV_Cross_Output main(SPIRV_Cross_Input stage_input)
{
    input_Position = stage_input.input_Position;
    input_TexCoord = stage_input.input_TexCoord;
    input_Color = stage_input.input_Color;
    vert_main();
    SPIRV_Cross_Output stage_output;
    stage_output.gl_Position = gl_Position;
    stage_output._entryPointOutput_Color = _entryPointOutput_Color;
    stage_output._entryPointOutput_TexCoord = _entryPointOutput_TexCoord;
    return stage_output;
}
