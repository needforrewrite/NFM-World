cbuffer _Global : register(b0)
{
    column_major float4x4 _30_LightViewProj0 : packoffset(c0);
    column_major float4x4 _30_LightViewProj1 : packoffset(c4);
    column_major float4x4 _30_LightViewProj2 : packoffset(c8);
    float _30_DepthBias : packoffset(c12);
    float _30_NumCascades : packoffset(c12.y);
    float3 _30_LightDirection : packoffset(c13);
    column_major float4x4 _30_WorldView : packoffset(c14);
    column_major float4x4 _30_WorldViewProj : packoffset(c18);
    float3 _30_FogColor : packoffset(c22);
    float _30_FogDistance : packoffset(c22.w);
    float _30_FogLogDensity : packoffset(c23);
};

Texture2D<float4> ShadowMap0 : register(t0);
SamplerState ShadowMapSampler0 : register(s0);
Texture2D<float4> ShadowMap1 : register(t1);
SamplerState ShadowMapSampler1 : register(s1);
Texture2D<float4> ShadowMap2 : register(t2);
SamplerState ShadowMapSampler2 : register(s2);

static float4 gl_Position;
static float4 Position;
static float3 Color;
static float4 _entryPointOutput_Color;
static float4 _entryPointOutput_WorldPos;
static float _entryPointOutput_ViewLength;

struct SPIRV_Cross_Input
{
    float4 Position : POSITION;
    float3 Color : COLOR0;
};

struct SPIRV_Cross_Output
{
    float4 _entryPointOutput_Color : TEXCOORD0;
    float4 _entryPointOutput_WorldPos : TEXCOORD1;
    float _entryPointOutput_ViewLength : TEXCOORD2;
    float4 gl_Position : SV_Position;
};

void vert_main()
{
    gl_Position = mul(Position, _30_WorldViewProj);
    _entryPointOutput_Color = float4(Color, 1.0f);
    _entryPointOutput_WorldPos = Position;
    _entryPointOutput_ViewLength = length(mul(Position, _30_WorldView).xyz);
}

SPIRV_Cross_Output main(SPIRV_Cross_Input stage_input)
{
    Position = stage_input.Position;
    Color = stage_input.Color;
    vert_main();
    SPIRV_Cross_Output stage_output;
    stage_output.gl_Position = gl_Position;
    stage_output._entryPointOutput_Color = _entryPointOutput_Color;
    stage_output._entryPointOutput_WorldPos = _entryPointOutput_WorldPos;
    stage_output._entryPointOutput_ViewLength = _entryPointOutput_ViewLength;
    return stage_output;
}
