struct VSInput
{
    float3 v_Position : TEXCOORD0;
    float4 v_Color : TEXCOORD1;
    float4 v_Meta : TEXCOORD2;
};

struct VSOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
};

VSOutput main(VSInput input)
{
    VSOutput output;
    output.Position = float4(input.v_Position + input.v_Meta.xyz, 1.0);
    output.Color = input.v_Color;
    return output;
}
