cbuffer _Global : register(b0)
{
    column_major float4x4 _325_LightViewProj0 : packoffset(c0);
    column_major float4x4 _325_LightViewProj1 : packoffset(c4);
    column_major float4x4 _325_LightViewProj2 : packoffset(c8);
    float _325_DepthBias : packoffset(c12);
    float _325_NumCascades : packoffset(c12.y);
    float3 _325_LightDirection : packoffset(c13);
    column_major float4x4 _325_View : packoffset(c14);
    column_major float4x4 _325_Projection : packoffset(c18);
    column_major float4x4 _325_ViewProj : packoffset(c22);
    float3 _325_SnapColor : packoffset(c26);
    uint _325_IsFullbright : packoffset(c26.w);
    uint _325_UseBaseColor : packoffset(c27);
    float3 _325_BaseColor : packoffset(c28);
    float3 _325_FogColor : packoffset(c29);
    float _325_FogDistance : packoffset(c29.w);
    float _325_FogLogDensity : packoffset(c30);
    float2 _325_EnvironmentLight : packoffset(c30.z);
    float3 _325_CameraPosition : packoffset(c31);
    float _325_Alpha : packoffset(c31.w);
    uint _325_Expand : packoffset(c32);
    float _325_RandomFloat : packoffset(c32.y);
    float _325_Darken : packoffset(c32.z);
    float _325_ChargedBlinkAmount : packoffset(c32.w);
    float _325_HalfThickness : packoffset(c33);
    float2 _325_Resolution : packoffset(c33.z);
    float _325_DistantOutlineDistanceFalloffWithCutoffMask : packoffset(c34);
    float _325_DistantOutlineClassicCutoffMask : packoffset(c34.y);
    float _325_DistantOutlineDistanceFalloffMask : packoffset(c34.z);
    float _325_OutlineClassicCutoffDistance : packoffset(c34.w);
    float _325_OutlineFalloffStartDistance : packoffset(c35);
    float _325_OutlineFalloffCutoffDistance : packoffset(c35.y);
    float _325_OutlineFalloffLinearFadeStartDistance : packoffset(c35.z);
    float _325_OutlineFalloffLinearFadeStartThickness : packoffset(c35.w);
    float _325_OutlineFalloffInverseLinearFadeLength : packoffset(c36);
};

Texture2D<float4> ShadowMap0 : register(t0);
SamplerState ShadowMapSampler0 : register(s0);
Texture2D<float4> ShadowMap1 : register(t1);
SamplerState ShadowMapSampler1 : register(s1);
Texture2D<float4> ShadowMap2 : register(t2);
SamplerState ShadowMapSampler2 : register(s2);

static float4 gl_Position;
static float3 input_PositionA;
static float3 input_PositionB;
static float input_Side;
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
    float3 input_PositionA : POSITION0;
    float3 input_PositionB : POSITION1;
    float input_Side : TEXCOORD0;
    float3 input_Normal : NORMAL0;
    float3 input_Color : COLOR0;
    float3 input_Centroid : POSITION2;
    float input_DecalOffset : TEXCOORD1;
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
    bool _1144 = parameters.w > 0.0f;
    float4 _900 = mul(float4(input_Centroid, 1.0f), world);
    float4 _909 = mul(float4(_900.xyz, 1.0f), _325_View);
    float _910 = _909.z;
    float _911 = -_910;
    float _1171 = _325_HalfThickness * min(1.0f, max(_325_OutlineFalloffStartDistance, 9.9999997473787516355514526367188e-05f) / max(_911, 9.9999997473787516355514526367188e-05f));
    bool3 _924 = (abs(input_Side) > 1.5f).xxx;
    float3 _1238 = float3(_924.x ? input_PositionB.x : input_PositionA.x, _924.y ? input_PositionB.y : input_PositionA.y, _924.z ? input_PositionB.z : input_PositionA.z) - ((input_Normal * input_DecalOffset) * 0.100000001490116119384765625f);
    float3 _1525;
    if (_325_Expand != 0u)
    {
        _1525 = _1238 + (normalize(_1238 - input_Centroid) * float3(15.0f - (frac(sin((input_Centroid.x + _325_RandomFloat) * 12.98980045318603515625f) * 43758.546875f) * 30.0f), 15.0f - (frac(sin((input_Centroid.y + _325_RandomFloat) * 12.98980045318603515625f) * 43758.546875f) * 30.0f), 15.0f - (frac(sin((input_Centroid.z + _325_RandomFloat) * 12.98980045318603515625f) * 43758.546875f) * 30.0f)));
    }
    else
    {
        _1525 = _1238;
    }
    float4 _955 = mul(float4(_1525, 1.0f), world);
    float4 _964 = mul(_955, _325_View);
    float4 _978 = mul(mul(float4(input_PositionA, 1.0f), world), _325_ViewProj);
    float4 _989 = mul(mul(float4(input_PositionB, 1.0f), world), _325_ViewProj);
    float2 _1010 = ((_325_Resolution * _989.xy) / _989.w.xx) - ((_325_Resolution * _978.xy) / _978.w.xx);
    float2 _1017 = normalize(_1010);
    bool2 _1018 = (dot(_1010, _1010) < 9.9999997473787516355514526367188e-05f).xx;
    float2 _1019 = float2(_1018.x ? float2(1.0f, 0.0f).x : _1017.x, _1018.y ? float2(1.0f, 0.0f).y : _1017.y);
    float4 _1029 = mul(_964, _325_Projection);
    float3 _1049 = lerp(input_Color, _325_BaseColor, float(_325_UseBaseColor != 0u).xxx);
    float4 _1058 = _1029 + float4(((((float2(-_1019.y, _1019.x) * lerp(_325_HalfThickness, lerp(_1171, lerp(_1171, _325_OutlineFalloffLinearFadeStartThickness * clamp((_325_OutlineFalloffCutoffDistance + _910) * _325_OutlineFalloffInverseLinearFadeLength, 0.0f, 1.0f), clamp(sign(_911 - _325_OutlineFalloffLinearFadeStartDistance), 0.0f, 1.0f)), _325_DistantOutlineDistanceFalloffWithCutoffMask), _325_DistantOutlineDistanceFalloffMask + _325_DistantOutlineDistanceFalloffWithCutoffMask)) * sign(input_Side)) / _325_Resolution) * 2.0f) * _1029.w, 0.0f, 0.0f);
    _1058.z = _1058.z + (0.00999999977648258209228515625f * parameters2.x);
    float3 _1527;
    if (_325_Darken < 1.0f)
    {
        float _1324 = _1049.z;
        float _1325 = _1049.y;
        float4 _1344 = lerp(float4(_1324, _1325, -1.0f, 0.666666686534881591796875f), float4(_1325, _1324, 0.0f, -0.3333333432674407958984375f), step(_1324, _1325).xxxx);
        float _1348 = _1049.x;
        float _1349 = _1344.x;
        float4 _1367 = lerp(float4(_1349, _1344.yw, _1348), float4(_1348, _1344.yz, _1349), step(_1349, _1348).xxxx);
        float _1369 = _1367.x;
        float _1371 = _1367.w;
        float _1373 = _1367.y;
        float _1375 = _1369 - min(_1371, _1373);
        float _1395 = _1375 / (_1369 + 1.0000000133514319600180897396058e-10f);
        float3 _1526;
        if (_1369 > _325_Darken)
        {
            _1526 = lerp(1.0f.xxx, clamp(abs((frac(float3(abs(_1367.z + ((_1371 - _1373) / ((6.0f * _1375) + 1.0000000133514319600180897396058e-10f))), _1395, _1369).xxx + float3(1.0f, 0.666666686534881591796875f, 0.3333333432674407958984375f)) * 6.0f) - 3.0f.xxx) - 1.0f.xxx, 0.0f.xxx, 1.0f.xxx), _1395.xxx) * _325_Darken;
        }
        else
        {
            _1526 = _1049;
        }
        _1527 = _1526;
    }
    else
    {
        _1527 = _1049;
    }
    float3 _1530;
    if (_1144)
    {
        _1530 = min(_1527 * 1.60000002384185791015625f, 1.0f.xxx);
    }
    else
    {
        _1530 = _1527;
    }
    float3 _1099 = normalize(mul(float4(input_Normal, 0.0f), world).xyz);
    float _1437 = dot(_1099, _325_LightDirection);
    float _1528;
    if (sign(_1437) == sign(dot(_1099, _900.xyz - _325_CameraPosition)))
    {
        _1528 = abs(_1437);
    }
    else
    {
        _1528 = 0.0f;
    }
    gl_Position = lerp(_1058, float4(2.0f, 2.0f, 0.0f, 1.0f), max(_325_DistantOutlineClassicCutoffMask * clamp(sign(_911 - _325_OutlineClassicCutoffDistance), 0.0f, 1.0f), _325_DistantOutlineDistanceFalloffWithCutoffMask * clamp(sign(_911 - _325_OutlineFalloffCutoffDistance), 0.0f, 1.0f)).xxxx);
    _entryPointOutput_Color = float4(_1530, min(parameters.y, _325_Alpha));
    _entryPointOutput_WorldPos = _955;
    _entryPointOutput_GetsShadowed = float(parameters.x > 0.0f);
    _entryPointOutput_NormalWorld = _1099;
    _entryPointOutput_ViewLength = length(_964);
    _entryPointOutput_Lit = float(((_325_IsFullbright == 0u) && (!(parameters.z > 0.0f))) && (!_1144));
    _entryPointOutput_Diffuse = _1528;
}

SPIRV_Cross_Output main(SPIRV_Cross_Input stage_input)
{
    input_PositionA = stage_input.input_PositionA;
    input_PositionB = stage_input.input_PositionB;
    input_Side = stage_input.input_Side;
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
