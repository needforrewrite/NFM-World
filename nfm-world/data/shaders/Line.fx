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

// Charged line blink
float ChargedBlinkAmount;

float HalfThickness;
float2 Resolution;

struct VertexShaderInput
{
	float3 PositionA : POSITION0;
	float3 PositionB : POSITION1;
	float Side : TEXCOORD0; // -1 or 1
	float3 Normal : NORMAL0;
	float3 Color : COLOR0;
	float3 Centroid : POSITION2;
	float DecalOffset : TEXCOORD1;
};

struct VertexShaderOutput
{
	float4 Position : SV_POSITION;
	float4 Color : COLOR0;
    float4 WorldPos : TEXCOORD2;
    float GetsShadowed : TEXCOORD3;
    float3 NormalWorld : TEXCOORD4;   // world-space face normal
    float3 CentroidWorld : TEXCOORD5; // world-space centroid
    float Lit : TEXCOORD6;            // 1 = apply diffuse/snap, 0 = fullbright/glow
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

    // Decode Side: abs > 1.5 means endpoint B, sign gives offset direction
    float3 position = (abs(input.Side) > 1.5) ? input.PositionB : input.PositionA;
    float sideSign = sign(input.Side);

    VS_DecalOffset(position, input.Normal, input.DecalOffset);

    if (Expand == true)
    {
        VS_Expand(position, input.Centroid, RandomFloat);
    }

    // Save the vertices position in world space (for shadow mapping)
    output.WorldPos = mul(float4(position, 1), world);
    output.GetsShadowed = getsShadowed;

    float4 viewPos = mul(output.WorldPos, View);

    // Transform both endpoints to clip space for screen-space line direction
    float4 clipA = mul(mul(float4(input.PositionA, 1), world), ViewProj);
    float4 clipB = mul(mul(float4(input.PositionB, 1), world), ViewProj);

    float2 screenA = Resolution * clipA.xy / clipA.w;
    float2 screenB = Resolution * clipB.xy / clipB.w;

    // Guard against NaN from normalize((0,0)) when endpoints project to the
    // same screen pixel (near-degenerate lines). Fallback to horizontal.
    float2 delta = screenB - screenA;
    float deltaLenSq = dot(delta, delta);
    float2 dir = deltaLenSq < 0.0001 ? float2(1, 0) : normalize(delta);
    float2 normal = float2(-dir.y, dir.x);

    // Screen-space offset for line thickness
    float4 clipPos = mul(viewPos, Projection);
    float2 offset = normal * HalfThickness * sideSign / Resolution * 2.0;

	float3 color = input.Color;

    // Apply base color
    if (UseBaseColor == true)
    {
        color = BaseColor;
    }

    output.Position = clipPos + float4(offset * clipPos.w, 0, 0);

    // Nudge outlines toward the camera so they render on top of the geometry they outline
    output.Position.z -= 0.1;

    if (Darken < 1.0f)
    {
        VS_Darken(color, Darken);
    }

    if (glow == true)
    {
        color = color * 1.6;
        // clamp to 1.0
        color = min(color, float3(1.0, 1.0, 1.0));
    }

    // Geometric diffuse is computed here (VS, per-face). Snap, charged-blink
    // and fog are applied per-pixel (see MainPS) so the geometric diffuse and
    // the shadow map fold into one darkening pass.
    output.NormalWorld = normalize(mul(float4(input.Normal, 0), world).xyz);
    output.CentroidWorld = mul(float4(input.Centroid, 1), world).xyz;
    output.Lit = (IsFullbright == false && isFullbright == false && glow == false) ? 1.0f : 0.0f;
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

    // Charged line blink overrides the color (matches original ordering).
    if (ChargedBlinkAmount > 0.0f)
    {
        color.r = (25.5 * ChargedBlinkAmount) / 255.0;
        color.g = (128.0 + 12.8 * ChargedBlinkAmount) / 255.0;
        color.b = 1.0;
    }

    // Fog was applied last in the original vertex shader (always).
    float3 viewPos = mul(input.WorldPos, View).xyz;
    VS_ApplyFog(color, viewPos, FogColor, FogDistance, FogDensity);
    VS_ColorCorrect(color);

	return float4(color, alpha);
}

technique Basic
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};
