#if OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0_level_9_1
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif

// Matrices
matrix World;
matrix View;
matrix WorldView;
matrix Projection;

// Line parameters
float LineWidth;
float2 Resolution;
bool WorldUnits;

// Material
float3 DiffuseColor;
float Opacity;

struct VertexShaderInput
{
    float4 Position : POSITION0;
    float2 UV : TEXCOORD0;
    float4 Color : COLOR0;
    float3 InstanceStart : TEXCOORD1;
    float3 InstanceEnd : TEXCOORD2;
    float3 InstanceColorStart : TEXCOORD3;
    float3 InstanceColorEnd : TEXCOORD4;
    // float InstanceDistanceStart : TEXCOORD5;
    // float InstanceDistanceEnd : TEXCOORD6;
	float3 Normal : NORMAL0;
	float3 Centroid : POSITION1;
};

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 UV : TEXCOORD0;
    float LineDistance : TEXCOORD1;
    float4 WorldPos : TEXCOORD2;
    float3 WorldStart : TEXCOORD3;
    float3 WorldEnd : TEXCOORD4;
};

void trimSegment(in float4 start, inout float4 end)
{
    // Trim end segment so it terminates between the camera plane and the near plane
    // Conservative estimate of the near plane
    float a = Projection[2][2];
    float b = Projection[3][2];
    float nearEstimate = -0.5 * b / a;

    float alpha = (nearEstimate - start.z) / (end.z - start.z);
    end.xyz = lerp(start.xyz, end.xyz, alpha);
}

VertexShaderOutput MainVS(in VertexShaderInput input)
{
    VertexShaderOutput output = (VertexShaderOutput)0;

    // Vertex color based on position
    output.Color.rgb = (input.Position.y < 0.5) ? input.InstanceColorStart : input.InstanceColorEnd;
    output.Color.a = 1.0;

    float aspect = Resolution.x / Resolution.y;

    // Camera space
    float4 start = mul(WorldView, float4(input.InstanceStart, 1.0));
    float4 end = mul(WorldView, float4(input.InstanceEnd, 1.0));

    if (WorldUnits)
    {
        output.WorldStart = start.xyz;
        output.WorldEnd = end.xyz;
    }
    else
    {
        output.UV = input.UV;
    }

    // Check for perspective projection
    bool perspective = (Projection[2][3] == -1.0);

    if (perspective)
    {
        if (start.z < 0.0 && end.z >= 0.0)
        {
            trimSegment(start, end);
        }
        else if (end.z < 0.0 && start.z >= 0.0)
        {
            trimSegment(end, start);
        }
    }

    // Clip space
    float4 clipStart = mul(Projection, start);
    float4 clipEnd = mul(Projection, end);

    // NDC space
    float3 ndcStart = clipStart.xyz / clipStart.w;
    float3 ndcEnd = clipEnd.xyz / clipEnd.w;

    // Direction
    float2 dir = ndcEnd.xy - ndcStart.xy;
    dir.x *= aspect;
    dir = normalize(dir);

    float4 clip;

    if (WorldUnits)
    {
        float3 worldDir = normalize(end.xyz - start.xyz);
        float3 tmpFwd = normalize(lerp(start.xyz, end.xyz, 0.5));
        float3 worldUp = normalize(cross(worldDir, tmpFwd));
        float3 worldFwd = cross(worldDir, worldUp);

        output.WorldPos = (input.Position.y < 0.5) ? start : end;

        // Height offset
        float hw = LineWidth * 0.5;
        output.WorldPos.xyz += (input.Position.x < 0.0) ? hw * worldUp : -hw * worldUp;

		// Cap extension
		output.WorldPos.xyz += (input.Position.y < 0.5) ? -hw * worldDir : hw * worldDir;

		// Add width to the box
		output.WorldPos.xyz += worldFwd * hw;

		// Endcaps
		if (input.Position.y > 1.0 || input.Position.y < 0.0)
		{
			output.WorldPos.xyz -= worldFwd * 2.0 * hw;
		}

        // Project the world position
        clip = mul(Projection, output.WorldPos);

        // Shift depth so line segments overlap neatly
        float3 clipPose = (input.Position.y < 0.5) ? ndcStart : ndcEnd;
        clip.z = clipPose.z * clip.w;
    }
    else
    {
        float2 offset = float2(dir.y, -dir.x);

        // Undo aspect ratio adjustment
        dir.x /= aspect;
        offset.x /= aspect;

        // Sign flip
        if (input.Position.x < 0.0)
            offset *= -1.0;

        // Endcaps
        if (input.Position.y < 0.0)
        {
            offset += -dir;
        }
        else if (input.Position.y > 1.0)
        {
            offset += dir;
        }

        // Adjust for line width
        offset *= LineWidth;

        // Adjust for clip-space to screen-space conversion
        offset /= Resolution.y;

        // Select end
        clip = (input.Position.y < 0.5) ? clipStart : clipEnd;

        // Back to clip space
        offset *= clip.w;
        clip.xy += offset;
    }

    output.Position = clip;

    return output;
}

float2 closestLineToLine(float3 p1, float3 p2, float3 p3, float3 p4)
{
    float3 p13 = p1 - p3;
    float3 p43 = p4 - p3;
    float3 p21 = p2 - p1;

    float d1343 = dot(p13, p43);
    float d4321 = dot(p43, p21);
    float d1321 = dot(p13, p21);
    float d4343 = dot(p43, p43);
    float d2121 = dot(p21, p21);

    float denom = d2121 * d4343 - d4321 * d4321;
    float numer = d1343 * d4321 - d1321 * d4343;

    float mua = clamp(numer / denom, 0.0, 1.0);
    float mub = clamp((d1343 + d4321 * mua) / d4343, 0.0, 1.0);

    return float2(mua, mub);
}

float4 MainPS(VertexShaderOutput input) : COLOR
{
    float alpha = Opacity;
    float3 color = DiffuseColor * input.Color.rgb;

    if (WorldUnits)
    {
        // Find the closest points on the view ray and the line segment
        float3 rayEnd = normalize(input.WorldPos.xyz) * 100000.0;
        float3 lineDir = input.WorldEnd - input.WorldStart;
        float2 params = closestLineToLine(input.WorldStart, input.WorldEnd, float3(0.0, 0.0, 0.0), rayEnd);

        float3 p1 = input.WorldStart + lineDir * params.x;
        float3 p2 = rayEnd * params.y;
        float3 delta = p1 - p2;
        float len = length(delta);
        float norm = len / LineWidth;

		if (norm > 0.5)
			discard;
    }
    else
    {
        // Discard outside rounded endcaps
        if (abs(input.UV.y) > 1.0)
        {
            float a = input.UV.x;
            float b = (input.UV.y > 0.0) ? input.UV.y - 1.0 : input.UV.y + 1.0;
            float len2 = a * a + b * b;

            if (len2 > 1.0)
                discard;
        }
    }

    return float4(color, alpha);
}

technique ScreenSpaceLine
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
};