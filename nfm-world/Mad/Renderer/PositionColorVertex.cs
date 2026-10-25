using System.Runtime.InteropServices;
using NFMWorld.Shaders;

namespace NFMWorld;

/// <summary>
/// A plain "POSITION float3 + COLOR0 byte4" vertex - the shape Ground.fx/Mountains.fx/Sky.fx all
/// expect (matches what XNA's <c>VertexPositionColor</c> used to provide). Shared by
/// <see cref="Ground"/>, <see cref="GroundPolys"/>, <see cref="Mountains"/> and <see cref="Sky"/>,
/// the same way <see cref="LineMesh.LineMeshVertexAttribute"/> is scoped to Line.fx's consumers.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly record struct PositionColorVertex(Vector3 Position, Color Color)
{
    public const int Stride = 16;

    public static readonly VertexLayoutDesc VertexLayout = new(
        Attributes:
        [
            new VertexAttributeDesc("POSITION", 0, 0, VertexAttributeFormat.Float3),
            new VertexAttributeDesc("COLOR", 0, 12, VertexAttributeFormat.Byte4Normalized),
        ],
        StrideInBytes: Stride);
}
