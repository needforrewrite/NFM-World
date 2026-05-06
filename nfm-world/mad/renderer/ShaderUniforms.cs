using System.Runtime.InteropServices;

namespace nfm_world;

// ─── BasicEffect uniforms ───────────────────────────────────────────────────

/// <summary>
/// Vertex uniform for BasicEffect.vert.hlsl (slot 0). 64 bytes.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct BasicEffectVertexUniforms
{
    public Matrix WorldViewProjection;  // offset 0
}

// ─── Shared sub-structs (match HLSL structs in Mad.hlsli) ───────────────────

/// <summary>
/// Matches HLSL <c>FogParams</c> in Mad.hlsli. 32 bytes (2 × float4).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct FogParams
{
    public Vector3 Color;       // offset  0
    public float   Distance;    // offset 12
    public float   Density;     // offset 16
    private Vector3 _fogPad;    // offset 20 — pad to 32
}

/// <summary>
/// Matches HLSL <c>ShadowParams</c> in Mad.hlsli. 208 bytes (13 × float4).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct ShadowParams
{
    public Matrix LightViewProj0;   // offset   0
    public Matrix LightViewProj1;   // offset  64
    public Matrix LightViewProj2;   // offset 128
    public float  DepthBias;        // offset 192
    private Vector3 _shadowPad;     // offset 196 — pad to 208
}

// ─── Poly shader uniforms ───────────────────────────────────────────────────

/// <summary>
/// Vertex uniform for Poly.vert.hlsl (slot 0, vertex stage). 304 bytes.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct PolyVertexUniforms
{
    public Matrix  View;                // offset   0
    public Matrix  Projection;          // offset  64
    public Matrix  ViewProj;            // offset 128
    public Vector3 CameraPosition;      // offset 192
    public float   Alpha;               // offset 204
    public Vector3 SnapColor;           // offset 208
    public float   Darken;              // offset 220
    public Vector3 LightDirection;      // offset 224
    public float   RandomFloat;         // offset 236
    public Vector2 EnvironmentLight;    // offset 240
    public Bool32  IsFullbright;        // offset 248
    public Bool32  UseBaseColor;        // offset 252
    public Vector3 BaseColor;           // offset 256
    public Bool32  Expand;              // offset 268
    public FogParams Fog;               // offset 272 (32 bytes)
}                                       // total: 304

/// <summary>
/// Fragment uniform for Poly.frag.hlsl (slot 0, fragment stage). 208 bytes.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct PolyFragUniforms
{
    public ShadowParams Shadow;    // offset 0, 208 bytes
}

// ─── Line shader uniforms ───────────────────────────────────────────────────

/// <summary>
/// Vertex uniform for Line.vert.hlsl (slot 0, vertex stage). 320 bytes.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct LineVertexUniforms
{
    public Matrix  View;                // offset   0
    public Matrix  Projection;          // offset  64
    public Matrix  ViewProj;            // offset 128
    public Vector3 CameraPosition;      // offset 192
    public float   Alpha;               // offset 204
    public Vector3 SnapColor;           // offset 208
    public float   Darken;              // offset 220
    public Vector3 LightDirection;      // offset 224
    public float   RandomFloat;         // offset 236
    public Vector2 EnvironmentLight;    // offset 240
    public Bool32  IsFullbright;        // offset 248
    public Bool32  UseBaseColor;        // offset 252
    public Vector3 BaseColor;           // offset 256
    public Bool32  Expand;              // offset 268
    public float   HalfThickness;       // offset 272
    public float   ChargedBlinkAmount;  // offset 276
    private Vector2 _pad;               // offset 280 — pad to 288
    public FogParams Fog;               // offset 288 (32 bytes)
}                                       // total: 320

/// <summary>
/// Fragment uniform for Line.frag.hlsl (slot 0, fragment stage). 208 bytes.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct LineFragUniforms
{
    public ShadowParams Shadow;    // offset 0, 208 bytes
}

// ─── Ground shader uniforms ─────────────────────────────────────────────────

/// <summary>
/// Vertex uniform for Ground.vert.hlsl (slot 0, vertex stage). 160 bytes.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct GroundVertexUniforms
{
    public Matrix    WorldView;      // offset   0
    public Matrix    WorldViewProj;  // offset  64
    public FogParams Fog;            // offset 128 (32 bytes)
}                                    // total: 160

/// <summary>
/// Fragment uniform for Ground.frag.hlsl (slot 0, fragment stage). 240 bytes.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct GroundFragUniforms
{
    public FogParams    Fog;       // offset   0 (32 bytes)
    public ShadowParams Shadow;    // offset  32 (208 bytes)
}                                  // total: 240

// ─── Mountains shader uniforms ──────────────────────────────────────────────

/// <summary>
/// Vertex uniform for Mountains.vert.hlsl (slot 0, vertex stage). 160 bytes.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct MountainsVertexUniforms
{
    public Matrix    WorldView;      // offset   0
    public Matrix    WorldViewProj;  // offset  64
    public FogParams Fog;            // offset 128 (32 bytes)
}                                    // total: 160

/// <summary>
/// Fragment uniform for Mountains.frag.hlsl (slot 0, fragment stage). 208 bytes.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct MountainsFragUniforms
{
    public ShadowParams Shadow;    // offset 0, 208 bytes
}

// ─── Sky shader uniforms ────────────────────────────────────────────────────

/// <summary>
/// Vertex uniform for Sky.vert.hlsl (slot 0, vertex stage). 64 bytes.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct SkyVertexUniforms
{
    public Matrix WorldViewProj;   // offset 0, 64 bytes
}

// ─── PolyShadow (shadow map generation) shader uniforms ─────────────────────

/// <summary>
/// Vertex uniform for PolyShadow.vert.hlsl (slot 0, vertex stage). 128 bytes.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct PolyShadowVertexUniforms
{
    public Matrix View;            // offset  0
    public Matrix Projection;     // offset 64
}                                  // total: 128

// ─── Helpers ────────────────────────────────────────────────────────────────

/// <summary>
/// 4-byte bool matching HLSL <c>bool</c> in cbuffers (which is 32 bits).
/// </summary>
[StructLayout(LayoutKind.Sequential, Size = 4)]
public struct Bool32
{
    private int _value;
    
    public Bool32(bool value) => _value = value ? 1 : 0;
    
    public static implicit operator Bool32(bool v) => new(v);
    public static implicit operator bool(Bool32 v) => v._value != 0;
}
