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
        //if (sign(dot(n, LightDirection)) == sign(dot(n, c - CameraPosition))) {
        //  diff = abs(dot(n, LightDirection));
        //}
		diff = abs(dot(n, LightDirection));
		color = (EnvironmentLight.x + EnvironmentLight.y * diff) * color;
	} else {
		color = BaseColor;
	}

	// Apply snap
	color += (color * SnapColor);

	float d = length(viewPos);
	float f = pow(FogDensity, max((d - FogDistance / 2.0) / FogDistance, 0.0));

	output.Color = color * float3(f, f, f) + FogColor * float3(1.0 - f, 1.0 - f, 1.0 - f);

	return output;
}

float4 MainPS(VertexShaderOutput input) : COLOR
{
	return float4(input.Color, 1.0);
}

technique BasicColorDrawing
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};