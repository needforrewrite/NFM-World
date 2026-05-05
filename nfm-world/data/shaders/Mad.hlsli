// Mad.hlsli — Shared utilities for NFM-World shaders (MoonWorks / ShaderCross)
// This replaces Mad.fxh for the non-Effect HLSL pipeline.
//
// This header declares NO resources (textures, samplers, cbuffers).
// Each material shader controls its own resource layout.
// Shared uniform sub-structs are provided for embedding in per-shader cbuffers.

#ifndef MAD_HLSLI
#define MAD_HLSLI

// ─── Shared uniform structs ─────────────────────────────────────────────────
// Embed these in your per-shader cbuffer. They are padded to float4 boundaries
// so C# LayoutKind.Sequential structs match without FieldOffset.

struct FogParams
{
    float3 Color;       // offset  0 (12 bytes)
    float  Distance;    // offset 12 (4 bytes)  — completes row 0
    float  Density;     // offset 16 (4 bytes)
    float3 _fogPad;     // offset 20 (12 bytes) — completes row 1
};                      // total: 32 bytes (2 × float4)

struct ShadowParams
{
    float4x4 LightViewProj0;   // offset   0 (64 bytes)
    float4x4 LightViewProj1;   // offset  64 (64 bytes)
    float4x4 LightViewProj2;   // offset 128 (64 bytes)
    float    DepthBias;         // offset 192 (4 bytes)
    float3   _shadowPad;        // offset 196 (12 bytes) — completes row
};                              // total: 208 bytes (13 × float4)

// ─── Helpers ─────────────────────────────────────────────────────────────────

void VS_UnpackParameters(
    in float4 parameters,
    out bool getsShadowed,
    out float alphaOverride,
    out bool isFullbright,
    out bool glow)
{
    getsShadowed = parameters.x > 0.0f;
    alphaOverride = parameters.y;
    isFullbright = parameters.z > 0.0f;
    glow = parameters.w > 0.0f;
}

float3 rgb2hsv(float3 c)
{
    float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
    float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
    float d = q.x - min(q.w, q.y);
    float e = 1.0e-10;
    return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

float3 hsv2rgb(float3 c)
{
    float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
    return c.z * lerp(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
}

void VS_ColorCorrect(inout float3 color)
{
    // Placeholder for future color correction
}

float Random(float input)
{
    return frac(sin(input * 12.9898) * 43758.5453);
}

void VS_DecalOffset(
    inout float3 position,
    in float3 normal,
    in float decalOffset)
{
    position = position - normal * decalOffset * 0.1;
}

void VS_Expand(
    inout float3 position,
    in float3 acentroid,
    in float randomFloat)
{
    float3 direction = normalize(position - acentroid);
    float3 randomScale = float3(
        15.0 - Random(acentroid.x + randomFloat) * 30.0,
        15.0 - Random(acentroid.y + randomFloat) * 30.0,
        15.0 - Random(acentroid.z + randomFloat) * 30.0);
    position = position + direction * randomScale;
}

void VS_Darken(inout float3 color, in float darken)
{
    float3 hsv = rgb2hsv(color);
    if (hsv.z > darken)
    {
        hsv.z = darken;
        color = hsv2rgb(hsv);
    }
}

void VS_Snap(inout float3 color, in float3 snapColor)
{
    color += (color * (snapColor * 255.0 / 100.0));
    color = min(color, float3(1.0, 1.0, 1.0));
}

void VS_ApplyPolygonDiffuse(
    inout float3 color,
    in float3 CentroidWorld,
    in float3 NormalWorld,
    in float3 LightDir,
    in float3 CamPosition,
    in float2 EnvLight)
{
    float3 c = CentroidWorld;
    float3 n = NormalWorld;
    float diff = 0.0;
    if (sign(dot(n, LightDir)) == sign(dot(n, c - CamPosition)))
    {
        diff = abs(dot(n, LightDir));
    }
    color = (EnvLight.x + EnvLight.y * diff) * color;
}

void VS_ApplyFog(
    inout float3 color,
    in float3 viewPos,
    in float3 FogCol,
    in float FogDist,
    in float FogDens)
{
    float d = length(viewPos);
    float f = pow(FogDens, max((d - FogDist / 2.0) / FogDist, 0.0));
    color = color * f + FogCol * (1.0 - f);
}

// Overload: scalar distance version (used by Ground pixel shader)
void VS_ApplyFog(
    inout float3 color,
    in float dist,
    in float3 FogCol,
    in float FogDist,
    in float FogDens)
{
    float f = pow(FogDens, max((dist - FogDist / 2.0) / FogDist, 0.0));
    color = color * f + FogCol * (1.0 - f);
}

// ─── Shadow mapping (pixel shader) ──────────────────────────────────────────
// All resources and uniforms are passed in — no global declarations.

void applyShadowingSingle(
    inout float3 diffuse,
    in float4 worldPos,
    in float4x4 lightViewProj,
    in Texture2D shadowMap,
    in SamplerState shadowSampler,
    in float depthBias,
    out bool isInLight)
{
    float4 lightingPosition = mul(worldPos, lightViewProj);

    float2 shadowTexCoord = 0.5 * lightingPosition.xy /
                            lightingPosition.w + float2(0.5, 0.5);
    shadowTexCoord.y = 1.0f - shadowTexCoord.y;

    if (shadowTexCoord.x >= 0.0 && shadowTexCoord.x <= 1.0 &&
        shadowTexCoord.y >= 0.0 && shadowTexCoord.y <= 1.0 &&
        lightingPosition.z > 0.0)
    {
        float shadowdepth = shadowMap.Sample(shadowSampler, shadowTexCoord).r;
        float ourdepth = (lightingPosition.z / lightingPosition.w) - depthBias;

        if (shadowdepth < ourdepth)
        {
            diffuse = diffuse * 0.5;
        }
        isInLight = true;
    }
    else
    {
        isInLight = false;
    }
}

void PS_ApplyShadowing(
    inout float3 diffuse,
    in float4 worldPos,
    in ShadowParams shadow,
    in Texture2D shadowMap0, in SamplerState shadowSampler0,
    in Texture2D shadowMap1, in SamplerState shadowSampler1,
    in Texture2D shadowMap2, in SamplerState shadowSampler2)
{
    bool isInLight0 = false;
    applyShadowingSingle(diffuse, worldPos, shadow.LightViewProj0, shadowMap0, shadowSampler0, shadow.DepthBias, isInLight0);

    if (!isInLight0)
    {
        bool isInLight1 = false;
        applyShadowingSingle(diffuse, worldPos, shadow.LightViewProj1, shadowMap1, shadowSampler1, shadow.DepthBias, isInLight1);

        if (!isInLight1)
        {
            bool isInLight2 = false;
            applyShadowingSingle(diffuse, worldPos, shadow.LightViewProj2, shadowMap2, shadowSampler2, shadow.DepthBias, isInLight2);
        }
    }
}

#endif // MAD_HLSLI
