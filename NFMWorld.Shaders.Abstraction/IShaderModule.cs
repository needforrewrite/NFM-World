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
public readonly record struct VertexLayoutDesc(
    IReadOnlyList<VertexAttributeDesc> Attributes,
    int StrideInBytes,
    int InstanceStepRate = 0
);

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

    /// <summary>
    /// The vertex shader's declared inputs, resolved back to the semantics a vertex layout names.
    ///
    /// Only WebGPU needs this. D3D11 matches an input layout against the compiled shader's own
    /// signature and GL binds attributes by name or explicit location, so both can work from the
    /// semantic they are given; WGSL has no semantics at all, only <c>@location</c> numbers, and the
    /// caller's layout says <c>TEXCOORD4</c>. This is the table that bridges the two.
    /// </summary>
    public IReadOnlyList<ShaderVertexInput> VertexInputs { get; init; } = Array.Empty<ShaderVertexInput>();
}

/// <summary>
/// One vertex shader input: the semantic its declaring source used, and the WGSL location it landed
/// on.
///
/// A matrix input is one entry spanning several locations, because that is how it reaches the shader
/// - one <c>mat4x4</c> attribute at location L occupies L..L+3 - while the caller's layout may still
/// list the four registers separately, as four <c>TEXCOORD</c> slots.
/// </summary>
public readonly record struct ShaderVertexInput(string Semantic, int SemanticIndex, int Location, int Registers);

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
    string Glsl330
)
{
    /// <summary>
    /// WGSL, as the WebGPU backend hands it to <c>wgpu</c>.
    ///
    /// The last form rather than the first, and translated from the same SPIR-V every other form
    /// comes from, so there is one shader source of truth. Its binding numbers are the backend's
    /// own (see <c>WgslCompiler</c>), not HLSL's register indices.
    /// </summary>
    public string Wgsl { get; init; } = "";

    /// <summary>
    /// The same WGSL with tint's <c>diagnostic(off, derivative_uniformity);</c> line left in, which is
    /// the only form the browser's Tint accepts. Desktop naga rejects that directive, so the two are
    /// carried separately; the WebGPU backend picks by target.
    /// </summary>
    public string WgslBrowser { get; init; } = "";

    /// <summary>
    /// The entry point's name in <see cref="Wgsl"/>.
    ///
    /// WebGPU names the stage's entry point explicitly instead of defaulting to <c>main</c>, and the
    /// name survives from the HLSL function, so the backend cannot know it without being told.
    /// </summary>
    public string EntryPoint { get; init; } = "main";
}
