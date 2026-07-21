void VS_UnpackParameters(in float4 parameters, out bool getsShadowed, out float alphaOverride, out bool isFullbright, out bool glow)
{
    getsShadowed = parameters.x > 0.0f;
    alphaOverride = parameters.y;
    isFullbright = parameters.z > 0.0f;
    glow = parameters.w > 0.0f;
}

// All components are in the range [0…1], including hue.
float3 rgb2hsv(float3 c)
{
    float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
    float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));

    float d = q.x - min(q.w, q.y);
    float e = 1.0e-10;
    return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

// All components are in the range [0…1], including hue.
float3 hsv2rgb(float3 c)
{
    float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
    return c.z * lerp(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
}

void VS_ColorCorrect(inout float3 color)
{
    // float3 hsv = rgb2hsv(color);
    // hsv.z *= 0.9;
    // color = hsv2rgb(hsv);
}

// Get uniquely looking random float between 0 and 1 from float input without using bitwise operations (not supported in vs_3_0)
float Random(float input)
{
    return frac(sin(input * 12.9898) * 43758.5453);
}

void VS_DecalOffset(
    inout float3 position,
    in float3 normal,
    in float decalOffset
) {
    // DecalOffset is negative to pull away from surface, positive to push into it
    // Multiply by the sign to control direction
    position = position - normal * decalOffset * 0.1;
}

void VS_Expand(
    inout float3 position,
    in float3 acentroid,
    in float randomFloat
) {
    // Translate vertex around centroid by a random factor between -15 and 15 units

    float3 direction = normalize(position - acentroid);
    float3 randomScale = float3(
        15.0 - Random(acentroid.x + randomFloat) * 30.0,
        15.0 - Random(acentroid.y + randomFloat) * 30.0,
        15.0 - Random(acentroid.z + randomFloat) * 30.0
    );
    position = position + direction * randomScale;
}

void VS_Darken(
    inout float3 color,
    in float darken
) {
    float3 hsv = rgb2hsv(color);
    if (hsv.z > darken)
    {
        hsv.z = darken;
        color = hsv2rgb(hsv);
    }
}

void VS_Snap(
    inout float3 color,
    in float3 snapColor
)
{
    color += (color * (snapColor * 255.0 / 100.0));
    // clamp to 1.0
    color = min(color, float3(1.0, 1.0, 1.0));
}

// Returns the geometric diffuse factor in [0..1]. Returns 0 when the camera is
// on the opposite side of the face from the light (a "max shadow" situation),
// otherwise the angle-based term abs(dot(n, L)).
float ComputePolygonDiffuse(
    in float3 CentroidWorld,
    in float3 NormalWorld,
    in float3 LightDirection,
    in float3 CameraPosition
)
{
    float3 c = CentroidWorld;
    float3 n = NormalWorld;
    float diff = 0.0;
    // phy original
    if (sign(dot(n, LightDirection)) == sign(dot(n, c - CameraPosition)))
    {
        diff = abs(dot(n, LightDirection));
    }
    return diff;
}

// Applies a diffuse factor to color exactly once (ambient + directional term).
void ApplyDiffuseFactor(
    inout float3 color,
    in float diff,
    in float2 EnvironmentLight
)
{
    color = (EnvironmentLight.x + EnvironmentLight.y * diff) * color;
}

void VS_ApplyPolygonDiffuse(
    inout float3 color,
    in float3 CentroidWorld,
    in float3 NormalWorld,
    in float3 LightDirection,
    in float3 CameraPosition,
    in float2 EnvironmentLight
)
{
    float diff = ComputePolygonDiffuse(CentroidWorld, NormalWorld, LightDirection, CameraPosition);
    ApplyDiffuseFactor(color, diff, EnvironmentLight);
}

void VS_ApplyFog(
    inout float3 color,
    in float3 viewPos,
    in float3 FogColor,
    in float FogDistance,
    in float FogDensity
)
{

	float d = length(viewPos);
	float f = pow(FogDensity, max((d - FogDistance / 2.0) / FogDistance, 0.0));

	color = color * float3(f, f, f) + FogColor * float3(1.0 - f, 1.0 - f, 1.0 - f);
}

float4x4 LightViewProj0;
texture ShadowMap0;
sampler ShadowMapSampler0 = sampler_state
{
    MipFilter = POINT;
    MinFilter = POINT;
    MagFilter = POINT;
    Texture = <ShadowMap0>;
    AddressU = Clamp;
    AddressV = Clamp;
};
float4x4 LightViewProj1;
texture ShadowMap1;
sampler ShadowMapSampler1 = sampler_state
{
    MipFilter = POINT;
    MinFilter = POINT;
    MagFilter = POINT;
    Texture = <ShadowMap1>;
    AddressU = Clamp;
    AddressV = Clamp;
};
float4x4 LightViewProj2;
texture ShadowMap2;
sampler ShadowMapSampler2 = sampler_state
{
    MipFilter = POINT;
    MinFilter = POINT;
    MagFilter = POINT;
    Texture = <ShadowMap2>;
    AddressU = Clamp;
    AddressV = Clamp;
};
// Shadow-map depth bias, in normalized shadow-map depth (range [0..1]).
//   Too small -> shadow acne (shimmering self-shadow on lit faces).
//   Too large -> peter-panning (shadow detaches / slides off its caster).
// Lowered from 0.25, which was ~1/4 of the whole depth range and made shadows
// detached and inaccurate. Tune in the 0.0005 .. 0.003 range; raise only until
// acne disappears, no further.
// NOTE: if DepthBias is also set from host/C# code, that value overrides this
// default — lower it there too.
float DepthBias = 0.0005f;
float NumCascades = 3;
float3 LightDirection;

void applyShadowingSingle(
    in float4 worldPos,
    in float4x4 lightViewProj,
    in sampler shadowMapSampler,
    out bool isInLight,
    out bool isShadowed
)
{
    isShadowed = false;

    // Find the position of this pixel in light space
    float4 lightingPosition = mul(worldPos, lightViewProj);

    // Find the position in the shadow map for this pixel
    float2 shadowTexCoord = 0.5 * lightingPosition.xy /
                            lightingPosition.w + float2( 0.5, 0.5 );
    shadowTexCoord.y = 1.0f - shadowTexCoord.y;

    // Only apply shadows if we're inside the light's view frustum
    if (shadowTexCoord.x >= 0.0 && shadowTexCoord.x <= 1.0 &&
        shadowTexCoord.y >= 0.0 && shadowTexCoord.y <= 1.0 &&
        lightingPosition.z > 0.0)
    {
        // Get the current depth stored in the shadow map
        float shadowdepth = tex2D(shadowMapSampler, shadowTexCoord).r;

        // Calculate the current pixel depth
        // The bias is used to prevent floating point errors that occur when
        // the pixel of the occluder is being drawn
        float ourdepth = (lightingPosition.z / lightingPosition.w);

        // Slope-scaled bias from light-space depth derivatives
        float dzdx = ddx(ourdepth);
        float dzdy = ddy(ourdepth);
        float slopeFactor = sqrt(dzdx * dzdx + dzdy * dzdy);
        float bias = DepthBias + clamp(slopeFactor * 1.0, 0.0, 0.01); // slope-scaled add-on (0 .. 0.01)

        ourdepth -= bias;

        // Check to see if this pixel is in front or behind the value in the shadow map
        if (shadowdepth < ourdepth)
        {
            // This pixel is occluded from the light
            isShadowed = true;
        }

        isInLight = true;
    } else {
        isInLight = false;
    }
}

bool PS_IsShadowed(
    in float4 worldPos,
    in float3 faceNormal
)
{
    if (NumCascades > 0)
    {
        // How much does this surface face the light?
        // LightDirection points TOWARD the light (e.g. (0,1,0) = light above).
        // A dot product near 0 means the surface is parallel to the light rays
        // — these are the surfaces that flicker. Skip shadowing for them.
        float NdotL = abs(dot(faceNormal, LightDirection));

        // Threshold below which we consider the surface too parallel to shadow reliably.
        // 0.1 ≈ surfaces within ~84° of the light direction are excluded.
        if (NdotL >= 0.05)
        {
            bool isInLight0 = false;
            bool isShadowed0 = false;
            applyShadowingSingle(worldPos, LightViewProj0, ShadowMapSampler0, isInLight0, isShadowed0);
            if (isInLight0) return isShadowed0;

            if (NumCascades > 1)
            {
                bool isInLight1 = false;
                bool isShadowed1 = false;
                applyShadowingSingle(worldPos, LightViewProj1, ShadowMapSampler1, isInLight1, isShadowed1);
                if (isInLight1) return isShadowed1;

                if (NumCascades > 2)
                {
                    bool isInLight2 = false;
                    bool isShadowed2 = false;
                    applyShadowingSingle(worldPos, LightViewProj2, ShadowMapSampler2, isInLight2, isShadowed2);
                    if (isInLight2) return isShadowed2;
                }
            }
        }
    }

    return false;
}

// Legacy path for fullbright shaders (terrain) with no diffuse term: darken
// the color once where the pixel is shadowed. Used by Ground.fx / Mountains.fx.
void PS_ApplyShadowing(
    inout float3 diffuse,
    in float4 worldPos,
    in float3 faceNormal
)
{
    if (PS_IsShadowed(worldPos, faceNormal))
    {
        diffuse = diffuse * float3(0.5, 0.5, 0.5);
    }
}