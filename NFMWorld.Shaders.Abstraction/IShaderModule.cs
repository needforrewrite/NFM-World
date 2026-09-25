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

    /// <summary>
    /// Four signed 16-bit integers, each scaled by the backend to [-1, 1] on the way to the shader.
    /// This is XNA's <c>VertexElementFormat.NormalizedShort4</c>. It exists alongside
    /// <see cref="Byte4Normalized"/> because a vertex that packs colors into 5-5-5-1 needs the same
    /// four lanes as <c>Byte4Normalized</c> in half the bytes, and because 16 bits of mantissa per
    /// lane is what a value that is read back out of a vertex by arithmetic - rather than sampled as
    /// a color - needs to survive the trip.
    /// </summary>
    Short4Normalized,
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

/// <summary>
/// One shader stage in every form a backend might need, as produced by the shader compiler.
///
/// All of them are carried because sokol selects the shader at *creation* time - an
/// <c>sg_shader_function</c> holds a single <c>source</c> field rather than one per backend, so
/// the choice is made from <c>sg_query_backend()</c> at shader-creation time and the bundle
/// cannot make it in advance. A backend ignores whichever forms it does not use: D3D11 compiles
/// <see cref="Hlsl"/>, Metal compiles <see cref="Msl"/>, OpenGL compiles <see cref="Glsl"/>, and
/// Vulkan consumes <see cref="Spirv"/> as bytecode.
///
/// <see cref="GlslEs"/> is the fourth GLSL flavour and the one a GLES3 context or ANGLE compiles.
/// It is a separate field from <see cref="Glsl"/> rather than a shared "GL source" because the two
/// are genuinely different dialects: ES wants <c>#version 300 es</c> with explicit precision
/// qualifiers, and it links varyings by name, so the ES pass renames them and the desktop pass
/// does not.
/// </summary>
public sealed record ShaderStageSources(
    ReadOnlyMemory<byte> Spirv,
    string Hlsl,
    string Msl,
    string Glsl,
    string GlslEs,
    string Glsl330);
