// NanoVG's per-vertex/per-pixel draw shader, ported from NvgSharp/src/XNA/Resources/Effect.fx.
// The math (scissor mask, box-gradient SDF round-rect, stroke AA mask) is unchanged from the
// original; only the cross-platform Macros.fxh wrapper (DECLARE_TEXTURE/BEGIN_CONSTANTS/TECHNIQUE)
// is dropped in favor of plain HLSL declarations, matching every other nfm-world shader (ImGui.fx,
// Line.fx, Ground.fx) against the same fxc.exe-to-MojoShader pipeline. EDGE_AA is always on here
// (NvgContext's own default), so there is no non-AA variant like the original's Effect_AA split.

float4x4 transformMat;
float4x4 scissorMat;
float4x4 paintMat;

float4 innerCol;
float4 outerCol;
float2 scissorExt;
float2 scissorScale;
float2 extent;
float radius;
float feather;
float strokeMult;
float strokeThr;

#if SM6
Texture2D g_texture : register(t0);
SamplerState g_textureSampler : register(s0);
#else
texture g_texture;
sampler g_textureSampler = sampler_state
{
    Texture = <g_texture>;
    MinFilter = ANISOTROPIC;
    MagFilter = ANISOTROPIC;
    MipFilter = LINEAR;
    AddressU = CLAMP;
    AddressV = CLAMP;
};
#endif

struct VS_OUTPUT
{
    float4 position : SV_POSITION;
    float2 ftcoord   : TEXCOORD0;
    float2 fpos      : TEXCOORD1;
};

struct PS_INPUT
{
    float4 position : SV_POSITION;
    float2 ftcoord   : TEXCOORD0;
    float2 fpos      : TEXCOORD1;
};

VS_OUTPUT VSMain(float2 pt : POSITION0, float2 tex : TEXCOORD0)
{
    VS_OUTPUT Output;
    Output.ftcoord = tex;
    Output.fpos = pt;
    Output.position = mul(float4(pt.x, pt.y, 0, 1), transformMat);
    return Output;
}

float sdroundrect(float2 pt, float2 ext, float rad)
{
    float2 ext2 = ext - float2(rad, rad);
    float2 d = abs(pt) - ext2;
    return min(max(d.x, d.y), 0.0) + length(max(d, 0.0)) - rad;
}

float scissorMask(float2 p)
{
    float2 sc = (abs((mul((float3x3)scissorMat, float3(p.x, p.y, 1.0))).xy) - scissorExt.xy);
    sc = float2(0.5, 0.5) - sc * scissorScale.xy;
    return clamp(sc.x, 0.0, 1.0) * clamp(sc.y, 0.0, 1.0);
}

float strokeMask(float2 ftcoord)
{
    return min(1.0, (1.0 - abs(ftcoord.x * 2.0 - 1.0)) * strokeMult) * min(1.0, ftcoord.y);
}

float4 PSMainFillGradient(PS_INPUT input) : SV_TARGET
{
    float scissor = scissorMask(input.fpos);
    float strokeAlpha = strokeMask(input.ftcoord);
    if (strokeAlpha < strokeThr) discard;

    float2 pt = (mul((float3x3)paintMat, float3(input.fpos, 1.0))).xy;
    float d = clamp((sdroundrect(pt, extent.xy, radius) + feather * 0.5) / feather, 0.0, 1.0);
    float4 color = lerp(innerCol, outerCol, d);

    color *= strokeAlpha * scissor;
    return color;
}

float4 PSMainFillImage(PS_INPUT input) : SV_TARGET
{
    float scissor = scissorMask(input.fpos);
    float strokeAlpha = strokeMask(input.ftcoord);
    if (strokeAlpha < strokeThr) discard;

    float2 pt = (mul((float3x3)paintMat, float3(input.fpos, 1.0))).xy / extent.xy;
    #if SM6
    float4 color = g_texture.Sample(g_textureSampler, pt);
    #else
    float4 color = tex2D(g_textureSampler, pt);
    #endif
    color = float4(color.xyz * color.w, color.w);

    color *= innerCol;
    color *= strokeAlpha * scissor;
    return color;
}

float4 PSMainSimple(PS_INPUT input) : SV_TARGET
{
    float scissor = scissorMask(input.fpos);
    float strokeAlpha = strokeMask(input.ftcoord);
    if (strokeAlpha < strokeThr) discard;

    return float4(1, 1, 1, 1);
}

float4 PSMainTriangles(PS_INPUT input) : SV_TARGET
{
    float scissor = scissorMask(input.fpos);
    float strokeAlpha = strokeMask(input.ftcoord);
    if (strokeAlpha < strokeThr) discard;

    #if SM6
    float4 color = g_texture.Sample(g_textureSampler, input.ftcoord);
    #else
    float4 color = tex2D(g_textureSampler, input.ftcoord);
    #endif
    color *= scissor;
    return color * innerCol;
}

technique FillGradient
{
    pass Pass0
    {
        VertexShader = compile vs_3_0 VSMain();
        PixelShader = compile ps_3_0 PSMainFillGradient();
    }
};

technique FillImage
{
    pass Pass0
    {
        VertexShader = compile vs_3_0 VSMain();
        PixelShader = compile ps_3_0 PSMainFillImage();
    }
};

technique Simple
{
    pass Pass0
    {
        VertexShader = compile vs_3_0 VSMain();
        PixelShader = compile ps_3_0 PSMainSimple();
    }
};

technique Triangles
{
    pass Pass0
    {
        VertexShader = compile vs_3_0 VSMain();
        PixelShader = compile ps_3_0 PSMainTriangles();
    }
};
