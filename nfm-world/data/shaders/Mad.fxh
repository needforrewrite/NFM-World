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

void PS_ApplyShadowing(
    inout float3 diffuse,
    in float4 lightingPosition,
    in sampler ShadowMapSampler,
    in float DepthBias
)
{
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
        float shadowdepth = tex2D(ShadowMapSampler, shadowTexCoord).r;

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
    }
}