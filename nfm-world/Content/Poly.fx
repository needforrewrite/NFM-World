#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_4_0_level_9_1
	#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

// BasicEffect
matrix World;
matrix WorldInverseTranspose;
matrix WorldViewProj;
matrix View;
matrix Projection;

// Custom
matrix WorldView;
float3 SnapColor;
bool IsFullbright;
bool UseBaseColor;
float3 BaseColor;
float3 LightDirection;
float3 FogColor;
float FogDistance;
float FogDensity;
float2 EnvironmentLight;
float3 CameraPosition;

// Lighting
matrix LightViewProj;
float DepthBias = 0.25f;

texture ShadowMap;
sampler ShadowMapSampler = sampler_state
{
    Texture = <ShadowMap>;
    AddressU = Clamp;
    AddressV = Clamp;
};

struct VertexShaderInput
{
	float3 Position : POSITION0;
	float3 Normal : NORMAL0;
	float3 Color : COLOR0;
	float3 Centroid : POSITION1;
};

struct VertexShaderOutput
{
	float4 Position : SV_POSITION;
	float3 Color : COLOR0;
    float4 WorldPos : TEXCOORD2;
};

VertexShaderOutput MainVS(in VertexShaderInput input)
{
	VertexShaderOutput output = (VertexShaderOutput)0;

	float3 viewPos = mul(float4(input.Position, 1), WorldView).xyz;
	output.Position = mul(float4(input.Position, 1), WorldViewProj);

	float3 color = lerp(input.Color, BaseColor, UseBaseColor ? 1.0 : 0.0);

	// Apply diffuse lighting
	if (IsFullbright == false) {
		float3 c = mul(float4(input.Centroid, 1), World).xyz;
		float3 n = normalize(mul(float4(input.Normal, 0), WorldInverseTranspose).xyz);
		float diff = 0.0;
		// phy original
        if (sign(dot(n, LightDirection)) == sign(dot(n, c - CameraPosition))) {
          diff = abs(dot(n, LightDirection));
        }
		color = (EnvironmentLight.x + EnvironmentLight.y * diff) * color;
	} else {
		color = BaseColor;
	}

	// Apply snap
	color += (color * SnapColor);

	float d = length(viewPos);
	float f = pow(FogDensity, max((d - FogDistance / 2.0) / FogDistance, 0.0));

	output.Color = color * float3(f, f, f) + FogColor * float3(1.0 - f, 1.0 - f, 1.0 - f);

    // Save the vertices postion in world space (for shadow mapping)
    output.WorldPos = mul(float4(input.Position, 1), World);

	return output;
}

float4 MainPS(VertexShaderOutput input) : COLOR
{
    // Find the position of this pixel in light space
    float4 lightingPosition = mul(input.WorldPos, LightViewProj);

    // Find the position in the shadow map for this pixel
    float2 ShadowTexCoord = 0.5 * lightingPosition.xy /
                            lightingPosition.w + float2( 0.5, 0.5 );
    ShadowTexCoord.y = 1.0f - ShadowTexCoord.y;

    float3 diffuse = input.Color;

    // Only apply shadows if we're inside the light's view frustum
    if (ShadowTexCoord.x >= 0.0 && ShadowTexCoord.x <= 1.0 &&
        ShadowTexCoord.y >= 0.0 && ShadowTexCoord.y <= 1.0 &&
        lightingPosition.z > 0.0)
    {
        // Get the current depth stored in the shadow map
        float shadowdepth = tex2D(ShadowMapSampler, ShadowTexCoord).r;

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

	return float4(diffuse, 1.0);
}

struct CreateShadowMap_VSOut
{
    float4 Position : SV_POSITION;
    float Depth     : TEXCOORD0;
};

// Transforms the model into light space an renders out the depth of the object
CreateShadowMap_VSOut CreateShadowMapVS(in VertexShaderInput input)
{
    CreateShadowMap_VSOut output = (CreateShadowMap_VSOut)0;
    output.Position = mul(float4(input.Position, 1), WorldViewProj);
    output.Depth = output.Position.z / output.Position.w;
    return output;
}

// Saves the depth value out to the 32bit floating point texture
float4 CreateShadowMapPS(CreateShadowMap_VSOut input) : COLOR
{
    return float4(input.Depth, input.Depth, input.Depth, 1.0);
}


technique CreateShadowMap
{
    pass Pass1
    {
        VertexShader = compile vs_2_0 CreateShadowMapVS();
        PixelShader = compile ps_2_0 CreateShadowMapPS();
    }
}

technique Basic
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};