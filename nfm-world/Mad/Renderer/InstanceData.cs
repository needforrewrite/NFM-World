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

    /// <summary>
    /// The per-instance world matrix, stored <em>untransposed</em>.
    ///
    /// This is the one matrix in the app that is not transposed. Uniform matrices are, by the
    /// backend (see <c>SokolCommandBuffer.SetUniform</c>), because the emitted HLSL declares its
    /// cbuffer members <c>column_major</c> while callers hold row-major matrices. This matrix is not
    /// a uniform - it is a vertex input (<c>in float4x4 world : TEXCOORD3</c> in Poly.fx and
    /// Line.fx) - so no backend's <c>SetUniform</c> can reach it and its storage order is settled
    /// entirely by how the compiled shader reads those four float4 registers.
    ///
    /// What is established: the emitted HLSL splits the input into four float4s and assigns them
    /// into a matrix variable (<c>world[0] = stage_input.world_0;</c> ... <c>world[3]</c>) before
    /// <c>mul(float4(p, 1), world)</c>, and the emitted ES GLSL binds those same four registers as
    /// the four columns of an <c>in mat4</c> and multiplies <c>world * vec4(p, 1)</c>. Those select
    /// transposes of each other, which is why this field's correct storage order cannot be reasoned
    /// about from the shared shader source alone - and why the pre-migration transpose, which the
    /// previous shader path did require, must not be assumed to still be required here.
    ///
    /// A doubled transpose is not a subtle error: a row-vector matrix keeps its translation in the
    /// fourth <em>column</em>, so transposing twice reads the translation as the w component, and
    /// instances divide by a position rather than being offset by it. Instanced geometry then
    /// collapses or flies apart while every non-instanced draw stays correct.
    ///
    /// Both backends consume the same SPIR-V module and this same field, so if instanced geometry
    /// ever looks wrong under exactly one of them, this field is the wrong suspect - the divergence
    /// would have to be in how that backend binds the four registers.
    /// </summary>
    public Matrix World = world;
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