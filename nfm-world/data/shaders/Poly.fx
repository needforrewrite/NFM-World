#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#endif

#include "./Mad.fxh"

float4x4 View;
float4x4 Projection;
float4x4 ViewProj;
float3 SnapColor;
bool IsFullbright;
bool UseBaseColor;
float3 BaseColor;
float3 FogColor;
float FogDistance;
float FogDensity;
float2 EnvironmentLight;
float3 CameraPosition;
float Alpha;


// Damage
bool Expand;
float RandomFloat;
float Darken; // set below 1.0f to adjust brightness

struct VertexShaderInput
{
	float3 Position : POSITION0;
	float3 Normal : NORMAL0;
	float3 Color : COLOR0;
	float3 Centroid : POSITION1;
	float DecalOffset : TEXCOORD0;
};

struct VertexShaderOutput
{
	float4 Position : SV_POSITION;
	float4 Color : COLOR0;
    float4 WorldPos : TEXCOORD2;
    float GetsShadowed : TEXCOORD3;
    float3 NormalWorld : TEXCOORD4;   // world-space face normal
    float3 CentroidWorld : TEXCOORD5; // world-space centroid
    float Lit : TEXCOORD6;            // 1 = apply diffuse/snap, 0 = fullbright
    float Diffuse : TEXCOORD7;        // pre-computed in VS, consumed in PS
};

VertexShaderOutput MainVS(
    in VertexShaderInput input,
    // instance parameters
    in float4x4 world : TEXCOORD3,
    in float4 parameters : TEXCOORD7
)
{
    bool getsShadowed;
    float alphaOverride;
    bool isFullbright;
    bool glow;
    VS_UnpackParameters(parameters, getsShadowed, alphaOverride, isFullbright, glow);

	VertexShaderOutput output = (VertexShaderOutput)0;

    float3 position = input.Position;

    VS_DecalOffset(position, input.Normal, input.DecalOffset);

    if (Expand == true)
    {
        VS_Expand(position, input.Centroid, RandomFloat);
    }

    // Save the vertices postion in world space (for shadow mapping)
    output.WorldPos = mul(float4(position, 1), world);
    output.GetsShadowed = getsShadowed;

    float4 viewPos = mul(output.WorldPos, View);

	float3 color = input.Color;

    // Apply base color
    if (UseBaseColor == true)
    {
        color = BaseColor;
    }

    output.Position = mul(viewPos, Projection);

    if (Darken < 1.0f)
    {
        VS_Darken(color, Darken);
    }

    // Geometric diffuse is computed here (VS, per-face). Snap, fog and the
    // shadow-map darkening are applied per-pixel (see MainPS) so the geometric
    // diffuse and the shadow map fold into a single darkening pass.
    output.NormalWorld = normalize(mul(float4(input.Normal, 0), world).xyz);
    output.CentroidWorld = mul(float4(input.Centroid, 1), world).xyz;
    output.Lit = (IsFullbright == false && isFullbright == false) ? 1.0f : 0.0f;
    output.Diffuse = ComputePolygonDiffuse(output.CentroidWorld, output.NormalWorld, LightDirection, CameraPosition);

    // Ship the UNLIT color; diffuse application + snap + fog happen in PS.
    output.Color = float4(color, min(alphaOverride, Alpha));

	return output;
}

float4 MainPS(VertexShaderOutput input) : SV_TARGET
{
    float3 color = input.Color.rgb;
    float  alpha = input.Color.a;

    if (input.Lit > 0.0)
    {
        // Pre-computed in vertex shader (per-face value, same for all pixels).
        float diff = input.Diffuse;

        // Shadow map: if occluded, force the SAME factor to its minimum.
        // This is what stops pixels being shadowed twice.
        if (input.GetsShadowed > 0.0 && PS_IsShadowed(input.WorldPos, input.NormalWorld))
        {
            diff = 0.0;
        }

        // Apply the combined diffuse exactly once, then snap.
        ApplyDiffuseFactor(color, diff, EnvironmentLight);
        VS_Snap(color, SnapColor);
    }

    // Fog was applied last in the original vertex shader (always).
    float3 viewPos = mul(input.WorldPos, View).xyz;
    VS_ApplyFog(color, viewPos, FogColor, FogDistance, FogDensity);
    VS_ColorCorrect(color);

	return float4(color, alpha);
}

struct CreateShadowMap_VSOut
{
    float4 Position : SV_POSITION;
    float Depth     : TEXCOORD0;
};

// Transforms the model into light space an renders out the depth of the object
CreateShadowMap_VSOut CreateShadowMapVS(
    in VertexShaderInput input,
    float4x4 world : TEXCOORD3
)
{
    CreateShadowMap_VSOut output = (CreateShadowMap_VSOut)0;

    output.Position = mul(mul(mul(float4(input.Position, 1), world), View), Projection);
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
