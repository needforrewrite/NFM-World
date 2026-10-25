namespace ShaderPoc;

/// <summary>
/// The reflection model, mirroring sokol-shdc's <c>types/reflection/*.h</c>.
/// Kept deliberately close to the C++ shape so the emitter written against it can
/// stay a line-for-line translation of <c>generators/sokolc.cc</c>.
/// </summary>
public enum ShaderStage { Invalid = -1, Vertex, Fragment, Compute }

/// <summary>Scalar/vector/matrix base type, matching spirv-cross's <c>Basetype</c> subset we care about.</summary>
public enum BaseType { Unknown, Bool, Int, Uint, Float }

/// <summary>One reflected struct member (a uniform block field).</summary>
public sealed record UniformMember(string Name, int Offset, int SizeBytes, BaseType Type, int VectorSize, int Columns);

/// <summary>
/// One vertex-stage input, i.e. an attribute slot in the pipeline's vertex layout.
///
/// <paramref name="Columns"/> is how many consecutive registers the input occupies: a matrix
/// input is expanded by the HLSL compiler into one register per column, exactly as fxc does for
/// a D3D9-era <c>float4x4 world : TEXCOORD3</c> (which is why the app's instance layout lists
/// TEXCOORD3..6 as four separate float4 attributes). Everything else occupies one.
/// </summary>
public sealed record StageAttr(string Name, int Slot, int Location, BaseType Type, int VectorSize, int Columns = 1)
{
    /// <summary>The number of vertex-layout slots (and D3D11 semantic indices) this input spans.</summary>
    public int Registers => Columns > 1 ? Columns : 1;
}

/// <summary>A merged-per-program texture binding (VS and FS namespaces already unified).</summary>
public sealed record TextureBinding(string Name, int Slot, ShaderStage Stage, int SpirvId);

/// <summary>A merged-per-program sampler binding.</summary>
public sealed record SamplerBinding(string Name, int Slot, ShaderStage Stage, int SpirvId);

/// <summary>A single stage's contribution to the program.</summary>
public sealed record StageReflection(
    ShaderStage Stage,
    string EntryPoint,
    IReadOnlyList<StageAttr> Inputs,
    IReadOnlyList<UniformBlock> UniformBlocks)
{
    public IReadOnlyList<TextureBinding> Textures { get; init; } = Array.Empty<TextureBinding>();
    public IReadOnlyList<SamplerBinding> Samplers { get; init; } = Array.Empty<SamplerBinding>();
}

/// <summary>A uniform block as declared in one stage.</summary>
public sealed record UniformBlock(string Name, string InstanceName, int SizeBytes, IReadOnlyList<UniformMember> Members, int Binding = -1);

/// <summary>Everything reflected out of one <c>@program</c>, after slot merging.</summary>
public sealed record ProgramReflection(
    string Name,
    IReadOnlyList<StageReflection> Stages,
    IReadOnlyList<UniformBlock> UniformBlocks,
    IReadOnlyList<TextureBinding> Textures,
    IReadOnlyList<SamplerBinding> Samplers);

/// <summary>
/// One stage's cross-compiled forms, one per backend sokol can be built against.
///
/// All of them are carried because sokol selects the shader at *creation* time - an
/// <c>sg_shader_function</c> has a single <c>source</c> field, not one per backend - so the
/// choice is made by the caller from <c>sg_query_backend()</c> rather than by the bundle. The
/// bundle therefore cannot decide for itself and has to ship everything.
///
/// <see cref="Glsl"/> is the GL-dialect source (combined samplers, a version sokol's GL backend
/// accepts); the separate-texture/sampler GLSL used for reflection is not carried, because it is
/// not a dialect a driver compiles. <see cref="GlslEs"/> is the OpenGL ES 3.0 form, which ANGLE
/// and a GLES3 context compile.
/// </summary>
public sealed record StageSources(byte[] Spirv, string Hlsl, string Msl, string Glsl, string GlslEs);
