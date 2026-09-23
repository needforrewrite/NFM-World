cbuffer _Global : register(b0)
{
    column_major float4x4 _30_transformMat : packoffset(c0);
    column_major float4x4 _30_scissorMat : packoffset(c4);
    column_major float4x4 _30_paintMat : packoffset(c8);
    float4 _30_innerCol : packoffset(c12);
    float4 _30_outerCol : packoffset(c13);
    float2 _30_scissorExt : packoffset(c14);
    float2 _30_scissorScale : packoffset(c14.z);
    float2 _30_extent : packoffset(c15);
    float _30_radius : packoffset(c15.z);
    float _30_feather : packoffset(c15.w);
    float _30_strokeMult : packoffset(c16);
    float _30_strokeThr : packoffset(c16.y);
};

Texture2D<float4> g_texture : register(t0);
SamplerState g_textureSampler : register(s0);

static float4 gl_Position;
static float2 pt;
static float2 tex;
static float2 _entryPointOutput_ftcoord;
static float2 _entryPointOutput_fpos;

struct SPIRV_Cross_Input
{
    float2 pt : POSITION0;
    float2 tex : TEXCOORD0;
};

struct SPIRV_Cross_Output
{
    float2 _entryPointOutput_ftcoord : TEXCOORD0;
    float2 _entryPointOutput_fpos : TEXCOORD1;
    float4 gl_Position : SV_Position;
};

void vert_main()
{
    gl_Position = mul(float4(pt.x, pt.y, 0.0f, 1.0f), _30_transformMat);
    _entryPointOutput_ftcoord = tex;
    _entryPointOutput_fpos = pt;
}

SPIRV_Cross_Output main(SPIRV_Cross_Input stage_input)
{
    pt = stage_input.pt;
    tex = stage_input.tex;
    vert_main();
    SPIRV_Cross_Output stage_output;
    stage_output.gl_Position = gl_Position;
    stage_output._entryPointOutput_ftcoord = _entryPointOutput_ftcoord;
    stage_output._entryPointOutput_fpos = _entryPointOutput_fpos;
    return stage_output;
}
