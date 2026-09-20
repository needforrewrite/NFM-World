using System.Runtime.CompilerServices;
using NFMWorld.Graphics;
using NFMWorld.Shaders;

namespace NFMWorld;

public struct InstanceData(Matrix world, long layer = 0, bool getsShadowed = false, float alphaOverride = 1.0f, bool isFullbright = false, bool glow = false) : IEquatable<InstanceData>
{
    /// <summary>This struct's managed size in bytes: a Matrix4x4 (64) plus two Vector4s (16 each).</summary>
    public const int Stride = 96;

    /// <summary>
    /// Six TEXCOORD3-8 float4 registers: a transposed world matrix's four rows, then
    /// <see cref="AdditionalData"/>/<see cref="AdditionalData2"/>, expressed against the graphics
    /// abstraction for <see cref="Graphics.PipelineDesc.VertexLayouts"/>.
    /// </summary>
    public static readonly VertexLayoutDesc VertexLayout = new(
        Attributes:
        [
            new VertexAttributeDesc("TEXCOORD", 3, 0, VertexAttributeFormat.Float4),
            new VertexAttributeDesc("TEXCOORD", 4, 16, VertexAttributeFormat.Float4),
            new VertexAttributeDesc("TEXCOORD", 5, 32, VertexAttributeFormat.Float4),
            new VertexAttributeDesc("TEXCOORD", 6, 48, VertexAttributeFormat.Float4),
            new VertexAttributeDesc("TEXCOORD", 7, 64, VertexAttributeFormat.Float4),
            new VertexAttributeDesc("TEXCOORD", 8, 80, VertexAttributeFormat.Float4),
        ],
        StrideInBytes: 96,
        InstanceStepRate: 1);

    public Matrix World = Matrix.Transpose(world);
    public Vector4 AdditionalData = new(getsShadowed ? 1.0f : 0.0f, alphaOverride, isFullbright ? 1.0f : 0.0f, glow ? 1.0f : 0.0f); // x: GetsShadowed (1.0 or 0.0), y: AlphaOverride, z: IsFullbright (1.0 or 0.0), w: Glow (1.0 or 0.0)
    public Vector4 AdditionalData2 = new(layer, 0, 0, 0); // x: Layer
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(InstanceData other) =>
        World.Equals(other.World) && AdditionalData.Equals(other.AdditionalData) && AdditionalData2.Equals(other.AdditionalData2);

    public readonly override bool Equals(object? obj) => obj is InstanceData other && Equals(other);

    public readonly override int GetHashCode() => HashCode.Combine(World, AdditionalData, AdditionalData2);

    public static bool operator ==(InstanceData left, InstanceData right) => left.Equals(right);

    public static bool operator !=(InstanceData left, InstanceData right) => !(left == right);
}