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

void VS_ApplyPolygonDiffuse(
    inout float3 color,
    in float3 CentroidWorld,
    in float3 NormalWorld,
    in float3 LightDirection,
    in float3 CameraPosition,
    in float2 EnvironmentLight
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
    color = (EnvironmentLight.x + EnvironmentLight.y * diff) * color;
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

matrix LightViewProj0;
texture ShadowMap0;
sampler ShadowMapSampler0 = sampler_state
{
    Filter = MIN_MAG_MIP_POINT;
    Texture = <ShadowMap0>;
    AddressU = Clamp;
    AddressV = Clamp;
};
matrix LightViewProj1;
texture ShadowMap1;
sampler ShadowMapSampler1 = sampler_state
{
    Filter = MIN_MAG_MIP_POINT;
    Texture = <ShadowMap1>;
    AddressU = Clamp;
    AddressV = Clamp;
};
matrix LightViewProj2;
texture ShadowMap2;
sampler ShadowMapSampler2 = sampler_state
{
    Filter = MIN_MAG_MIP_POINT;
    Texture = <ShadowMap2>;
    AddressU = Clamp;
    AddressV = Clamp;
};
float DepthBias = 0.25f;

void applyShadowingSingle(
    inout float3 diffuse,
    in float4 worldPos,
    in matrix lightViewProj,
    in sampler shadowMapSampler,
    out bool isInLight
)
{
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
        float ourdepth = (lightingPosition.z / lightingPosition.w) - DepthBias;

        // Check to see if this pixel is in front or behind the value in the shadow map
        if (shadowdepth < ourdepth)
        {
            // Shadow the pixel by lowering the intensity
            diffuse = diffuse * float3(0.5, 0.5, 0.5);
        }

        isInLight = true;
    } else {
        isInLight = false;
    }
}

void PS_ApplyShadowing(
    inout float3 diffuse,
    in float4 worldPos
)
{
    bool isInLight0 = false;
    applyShadowingSingle(diffuse, worldPos, LightViewProj0, ShadowMapSampler0, isInLight0);

    if (isInLight0 == false)
    {
        bool isInLight1 = false;
        applyShadowingSingle(diffuse, worldPos, LightViewProj1, ShadowMapSampler1, isInLight1);

        if (isInLight1 == false)
        {
            bool isInLight2 = false;
            applyShadowingSingle(diffuse, worldPos, LightViewProj2, ShadowMapSampler2, isInLight2);
        }
    }
}