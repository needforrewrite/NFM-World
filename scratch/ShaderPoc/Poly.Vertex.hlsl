cbuffer _Global : register(b0)
{
    column_major float4x4 _360_LightViewProj0 : packoffset(c0);
    column_major float4x4 _360_LightViewProj1 : packoffset(c4);
    column_major float4x4 _360_LightViewProj2 : packoffset(c8);
    float _360_DepthBias : packoffset(c12);
    float _360_NumCascades : packoffset(c12.y);
    float3 _360_LightDirection : packoffset(c13);
    column_major float4x4 _360_View : packoffset(c14);
    column_major float4x4 _360_Projection : packoffset(c18);
    column_major float4x4 _360_ViewProj : packoffset(c22);
    float3 _360_SnapColor : packoffset(c26);
    uint _360_IsFullbright : packoffset(c26.w);
    uint _360_UseBaseColor : packoffset(c27);
    float3 _360_BaseColor : packoffset(c28);
    float3 _360_FogColor : packoffset(c29);
    float _360_FogDistance : packoffset(c29.w);
    float _360_FogLogDensity : packoffset(c30);
    float2 _360_EnvironmentLight : packoffset(c30.z);
    float3 _360_CameraPosition : packoffset(c31);
    float _360_Alpha : packoffset(c31.w);
    uint _360_Expand : packoffset(c32);
    float _360_RandomFloat : packoffset(c32.y);
    float _360_Darken : packoffset(c32.z);
};

Texture2D<float4> ShadowMap0 : register(t0);
SamplerState ShadowMapSampler0 : register(s0);
Texture2D<float4> ShadowMap1 : register(t1);
SamplerState ShadowMapSampler1 : register(s1);
Texture2D<float4> ShadowMap2 : register(t2);
SamplerState ShadowMapSampler2 : register(s2);

static float4 gl_Position;
static float3 input_Position;
static float3 input_Normal;
static float3 input_Color;
static float3 input_Centroid;
static float input_DecalOffset;
static float4x4 world;
static float4 parameters;
static float4 parameters2;
static float4 _entryPointOutput_Color;
static float4 _entryPointOutput_WorldPos;
static float _entryPointOutput_GetsShadowed;
static float3 _entryPointOutput_NormalWorld;
static float _entryPointOutput_ViewLength;
static float _entryPointOutput_Lit;
static float _entryPointOutput_Diffuse;

struct SPIRV_Cross_Input
{
    float3 input_Position : POSITION0;
    float3 input_Normal : NORMAL0;
    float3 input_Color : COLOR0;
    float3 input_Centroid : POSITION1;
    float input_DecalOffset : TEXCOORD0;
    float4 world_0 : TEXCOORD3;
    float4 world_1 : TEXCOORD4;
    float4 world_2 : TEXCOORD5;
    float4 world_3 : TEXCOORD6;
    float4 parameters : TEXCOORD7;
    float4 parameters2 : TEXCOORD8;
};

struct SPIRV_Cross_Output
{
    float4 _entryPointOutput_Color : TEXCOORD0;
    float4 _entryPointOutput_WorldPos : TEXCOORD1;
    float _entryPointOutput_GetsShadowed : TEXCOORD2;
    float3 _entryPointOutput_NormalWorld : TEXCOORD3;
    float _entryPointOutput_ViewLength : TEXCOORD4;
    float _entryPointOutput_Lit : TEXCOORD5;
    float _entryPointOutput_Diffuse : TEXCOORD6;
    float4 gl_Position : SV_Position;
};

void vert_main()
{
    float3 _775 = input_Position - ((input_Normal * input_DecalOffset) * 0.100000001490116119384765625f);
    float3 _1056;
    if (_360_Expand != 0u)
    {
        _1056 = _775 + (normalize(_775 - input_Centroid) * float3(15.0f - (frac(sin((input_Centroid.x + _360_RandomFloat) * 12.98980045318603515625f) * 43758.546875f) * 30.0f), 15.0f - (frac(sin((input_Centroid.y + _360_RandomFloat) * 12.98980045318603515625f) * 43758.546875f) * 30.0f), 15.0f - (frac(sin((input_Centroid.z + _360_RandomFloat) * 12.98980045318603515625f) * 43758.546875f) * 30.0f)));
    }
    else
    {
        _1056 = _775;
    }
    float4 _656 = mul(float4(_1056, 1.0f), world);
    float4 _665 = mul(_656, _360_View);
    float3 _679 = lerp(input_Color, _360_BaseColor, float(_360_UseBaseColor != 0u).xxx);
    float4 _683 = mul(_665, _360_Projection);
    _683.z = (_683.z + 0.100000001490116119384765625f) + (0.00999999977648258209228515625f * parameters2.x);
    float3 _1060;
    if (_360_Darken < 1.0f)
    {
        float _861 = _679.z;
        float _862 = _679.y;
        float4 _881 = lerp(float4(_861, _862, -1.0f, 0.666666686534881591796875f), float4(_862, _861, 0.0f, -0.3333333432674407958984375f), step(_861, _862).xxxx);
        float _885 = _679.x;
        float _886 = _881.x;
        float4 _904 = lerp(float4(_886, _881.yw, _885), float4(_885, _881.yz, _886), step(_886, _885).xxxx);
        float _906 = _904.x;
        float _908 = _904.w;
        float _910 = _904.y;
        float _912 = _906 - min(_908, _910);
        float _932 = _912 / (_906 + 1.0000000133514319600180897396058e-10f);
        float3 _1057;
        if (_906 > _360_Darken)
        {
            _1057 = lerp(1.0f.xxx, clamp(abs((frac(float3(abs(_904.z + ((_908 - _910) / ((6.0f * _912) + 1.0000000133514319600180897396058e-10f))), _932, _906).xxx + float3(1.0f, 0.666666686534881591796875f, 0.3333333432674407958984375f)) * 6.0f) - 3.0f.xxx) - 1.0f.xxx, 0.0f.xxx, 1.0f.xxx), _932.xxx) * _360_Darken;
        }
        else
        {
            _1057 = _679;
        }
        _1060 = _1057;
    }
    else
    {
        _1060 = _679;
    }
    float3 _714 = normalize(mul(float4(input_Normal, 0.0f), world).xyz);
    float _974 = dot(_714, _360_LightDirection);
    float _1058;
    if (sign(_974) == sign(dot(_714, mul(float4(input_Centroid, 1.0f), world).xyz - _360_CameraPosition)))
    {
        _1058 = abs(_974);
    }
    else
    {
        _1058 = 0.0f;
    }
    gl_Position = _683;
    _entryPointOutput_Color = float4(_1060, min(parameters.y, _360_Alpha));
    _entryPointOutput_WorldPos = _656;
    _entryPointOutput_GetsShadowed = float(parameters.x > 0.0f);
    _entryPointOutput_NormalWorld = _714;
    _entryPointOutput_ViewLength = length(_665);
    _entryPointOutput_Lit = float((_360_IsFullbright == 0u) && (!(parameters.z > 0.0f)));
    _entryPointOutput_Diffuse = _1058;
}

SPIRV_Cross_Output main(SPIRV_Cross_Input stage_input)
{
    input_Position = stage_input.input_Position;
    input_Normal = stage_input.input_Normal;
    input_Color = stage_input.input_Color;
    input_Centroid = stage_input.input_Centroid;
    input_DecalOffset = stage_input.input_DecalOffset;
    world[0] = stage_input.world_0;
    world[1] = stage_input.world_1;
    world[2] = stage_input.world_2;
    world[3] = stage_input.world_3;
    parameters = stage_input.parameters;
    parameters2 = stage_input.parameters2;
    vert_main();
    SPIRV_Cross_Output stage_output;
    stage_output.gl_Position = gl_Position;
    stage_output._entryPointOutput_Color = _entryPointOutput_Color;
    stage_output._entryPointOutput_WorldPos = _entryPointOutput_WorldPos;
    stage_output._entryPointOutput_GetsShadowed = _entryPointOutput_GetsShadowed;
    stage_output._entryPointOutput_NormalWorld = _entryPointOutput_NormalWorld;
    stage_output._entryPointOutput_ViewLength = _entryPointOutput_ViewLength;
    stage_output._entryPointOutput_Lit = _entryPointOutput_Lit;
    stage_output._entryPointOutput_Diffuse = _entryPointOutput_Diffuse;
    return stage_output;
}
