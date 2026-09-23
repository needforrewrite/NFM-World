cbuffer _Global : register(b0)
{
    column_major float4x4 _20_World : packoffset(c0);
    column_major float4x4 _20_View : packoffset(c4);
    column_major float4x4 _20_Projection : packoffset(c8);
};


static float4 gl_Position;
static float4 Position;
static float4 Color;
static float4 _entryPointOutput_Color;

struct SPIRV_Cross_Input
{
    float4 Position : POSITION;
    float4 Color : COLOR0;
};

struct SPIRV_Cross_Output
{
    float4 _entryPointOutput_Color : TEXCOORD0;
    float4 gl_Position : SV_Position;
};

void vert_main()
{
    gl_Position = mul(mul(mul(Position, _20_World), _20_View), _20_Projection);
    _entryPointOutput_Color = Color;
}

SPIRV_Cross_Output main(SPIRV_Cross_Input stage_input)
{
    Position = stage_input.Position;
    Color = stage_input.Color;
    vert_main();
    SPIRV_Cross_Output stage_output;
    stage_output.gl_Position = gl_Position;
    stage_output._entryPointOutput_Color = _entryPointOutput_Color;
    return stage_output;
}
