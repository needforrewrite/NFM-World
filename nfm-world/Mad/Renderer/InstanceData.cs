using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework.Graphics;

namespace NFMWorld;

public struct InstanceData(Matrix world, long layer = 0, bool getsShadowed = false, float alphaOverride = 1.0f, bool isFullbright = false, bool glow = false) : IEquatable<InstanceData>
{
    public static VertexDeclaration InstanceDeclaration { get; } = new VertexDeclaration
    (
        new VertexElement(0,  VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 3),
        new VertexElement(16, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 4),
        new VertexElement(32, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 5),
        new VertexElement(48, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 6),
        new VertexElement(64, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 7),
        new VertexElement(80, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 8)
    );

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