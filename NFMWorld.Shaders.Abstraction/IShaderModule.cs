namespace NFMWorld.Shaders;

public enum ShaderStage
{
    Vertex,
    Pixel,
}

public enum UniformType
{
    Float,
    Vector2,
    Vector3,
    Vector4,
    Matrix4x4,
    Int,
}

public readonly record struct UniformParam(string Name, int Offset, int SizeInBytes, UniformType Type);

public readonly record struct ResourceBinding(string Name, int Slot);

public readonly record struct VertexAttributeDesc(string Semantic, int Slot, int OffsetInBytes, VertexAttributeFormat Format);

public enum VertexAttributeFormat
{
    Float1,
    Float2,
    Float3,
    Float4,
    Byte4Normalized,
}

/// <summary>
/// One vertex buffer stream's layout. A pipeline can bind more than one of these at different
/// slots (see <see cref="NFMWorld.Graphics.PipelineDesc.VertexLayouts"/>) - e.g. per-vertex
/// geometry in slot 0 plus a per-instance stream in slot 1 for hardware instancing, matching how
/// FNA3D_VertexBufferBinding pairs a declaration with an instance step rate per stream.
/// </summary>
public readonly record struct VertexLayoutDesc(IReadOnlyList<VertexAttributeDesc> Attributes, int StrideInBytes, int InstanceStepRate = 0);

/// <summary>
/// Parsed once at shader build/import time - not re-derived from HLSL text at runtime - so
/// consumers (pipeline creation, generated typed wrappers) never need to know the shader's
/// source language or the backend's native bytecode format.
/// </summary>
public sealed class ShaderReflection
{
    public required IReadOnlyList<UniformParam> Uniforms { get; init; }
    public required IReadOnlyList<ResourceBinding> Textures { get; init; }
    public required IReadOnlyList<ResourceBinding> Samplers { get; init; }
    public VertexLayoutDesc? ExpectedVertexLayout { get; init; }
}

/// <summary>
/// A compiled shader module: opaque, backend-specific bytecode plus reflected metadata.
/// For the FNA3D backend this bytecode is (at least initially) an FNA3D/MojoShader Effect
/// blob; a future sokol_gfx backend would store SPIR-V/GLSL/Metal source instead. Callers
/// never need to know which.
/// </summary>
public interface IShaderModule
{
    ShaderStage Stage { get; }
    ReadOnlyMemory<byte> Bytecode { get; }
}
