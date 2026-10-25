// LLM maintained.
//
// Compiles a vertex/pixel pair out of the generated bundles' ES 3.0 GLSL and resolves the
// reflection's names to GL uniform/block locations.
//
// This is where the abstraction's byte-offset uniform model meets GL. The abstraction's
// SetUniform takes a byte offset into a merged block (D3D packoffset numbering, carried in the
// bundle's ShaderReflection); GL has no such block, so this class declares the merged block as a
// std140 UBO and uploads the whole block per draw. Reading the block's offset table out of the
// driver and comparing it against the reflection is what the smoke test does - see
// GlUniformBlock.
using System.Runtime.InteropServices;
using NFMWorld.Shaders;
using Silk.NET.OpenGLES;

namespace NFMWorld.Graphics.OpenGL;

/// <summary>
/// The GL uniform-block binding a shader program's <c>_Global</c> block was given, plus the
/// sizes and offsets the driver actually chose.
/// </summary>
internal sealed class GlUniformBlock
{
    /// <summary>The block's binding point, as passed to <c>glUniformBlockBinding</c>.</summary>
    internal uint Binding { get; }

    /// <summary>The size in bytes the driver gave the block, which is what a UBO has to be.</summary>
    internal int DataSize { get; }

    /// <summary>
    /// The block's members as the driver reports them, keyed by name. Recorded so the smoke test
    /// can check the driver's layout against the reflection rather than trusting that std140 and
    /// packoffset agree.
    /// </summary>
    internal IReadOnlyDictionary<string, int> MemberOffsets { get; }

    internal GlUniformBlock(uint binding, int dataSize, IReadOnlyDictionary<string, int> memberOffsets)
    {
        Binding = binding;
        DataSize = dataSize;
        MemberOffsets = memberOffsets;
    }
}

/// <summary>
/// A linked GL program built from one vertex and one pixel ES 3.0 source, with the reflection's
/// names resolved to GL locations.
/// </summary>
internal sealed class GlShaderProgram : IDisposable
{
    private readonly GL _gl;
    private readonly GlDeletionQueue _deletions;

    internal uint Handle { get; }

    /// <summary>
    /// The <c>_Global</c> uniform block, or null when neither stage declares one (the particle
    /// shader has no uniforms at all, for example).
    /// </summary>
    internal GlUniformBlock? UniformBlock { get; }

    /// <summary>
    /// Each reflected texture's sampler-uniform location, indexed the same as
    /// <see cref="ShaderReflection.Textures"/>.
    ///
    /// This list is what the sampler rename in the shader compiler exists for. The ES GLSL used to
    /// declare its combined samplers as <c>_1062</c> - spirv-cross's fallback name, taken from the
    /// SPIR-V id - while the reflection called the same texture <c>ShadowMap0</c>, so a lookup by
    /// the reflected name returned -1 and the sampler silently kept texture unit 0.
    /// </summary>
    internal IReadOnlyList<int> TextureLocations { get; }

    /// <summary>
    /// How many sampler uniforms the linked program declares, across every block.
    ///
    /// Not the same as <c>reflection.Textures.Count</c>, and the difference is the whole reason
    /// this exists. A reflection lists the resources the <em>source</em> declares; GLSL reports the
    /// ones the <em>linked program</em> still references, because the emitter drops a sampler the
    /// compiled entry point never samples. Nvg is the case in point: its pixel stage has four entry
    /// points, and the bundle is built from <c>PSMainSimple</c> - which reads no texture - while the
    /// reflection still names <c>g_texture</c>. A caller comparing the two counts can tell "this
    /// texture was optimised out" apart from "this texture's sampler is misnamed", which is
    /// otherwise indistinguishable, since both give a location of -1.
    /// </summary>
    internal int SamplerUniformCount { get; }

    /// <summary>
    /// The vertex inputs the driver actually kept, keyed by the location the compiled GLSL
    /// declared. Read out of the linked program rather than assumed from the layout qualifiers.
    ///
    /// This is a set rather than a mapping, because <c>layout(location = N)</c> pins the two
    /// together: the name exists so a missing location can be reported legibly by the smoke test.
    /// An input the compiler optimised away is absent, which is legal and is why this is a lookup
    /// rather than an assertion.
    /// </summary>
    internal IReadOnlyDictionary<int, string> AttributeLocations { get; }

    /// <summary>
    /// Every location the vertex stage's inputs occupy, expanded from <see cref="AttributeLocations"/>.
    ///
    /// A matrix input is one attribute occupying several consecutive locations - <c>mat4 world</c>
    /// is a single <c>glGetActiveAttrib</c> entry at location 5 that consumes 5, 6, 7 and 8. Only
    /// the base location comes back from the query, so a caller checking its layout against the
    /// shader has to expand the span itself or it will report the matrix's other three rows as
    /// missing. Recorded here rather than derived at the call site because the matrix ordering that
    /// decides the span (column-major consumes consecutive locations, row-major does not) is a
    /// property of this program, not of whoever is asking.
    /// </summary>
    internal IReadOnlySet<int> AttributeLocationSpans { get; }

    /// <summary>The uniform block's member offsets as the reflection declares them, for the smoke test to compare against the driver's.</summary>
    internal ShaderReflection Reflection { get; }

    internal GlShaderProgram(GL gl, GlDeletionQueue deletions, ShaderStageSources vertex, ShaderStageSources pixel, ShaderReflection reflection, GlProgramCache? cache = null)
    {
        _gl = gl;
        _deletions = deletions;
        Reflection = reflection;

        // The cache is consulted before anything is compiled, because a hit makes the compile and the
        // link both unnecessary - which is the entire point, given that one program in this tree costs
        // minutes to link. A miss falls through to the normal path and the result is saved after.
        var restored = cache?.TryLoad(vertex, pixel);
        if (restored is { } program)
        {
            Handle = program;
        }
        else
        {
            var vertexShader = Compile(ShaderType.VertexShader, vertex.GlslEs, "vertex");
            var pixelShader = Compile(ShaderType.FragmentShader, pixel.GlslEs, "pixel");
            try
            {
                Handle = Link(vertexShader, pixelShader);
            }
            finally
            {
                // The program keeps the compiled result; the shader objects are only needed until the
                // link. Deleting them here still leaves the program usable - the linked program is a
                // separate object in GL.
                _gl.DeleteShader(vertexShader);
                _gl.DeleteShader(pixelShader);
            }

            cache?.TrySave(Handle, vertex, pixel);
        }

        UniformBlock = ResolveUniformBlock(reflection);
        TextureLocations = ResolveTextureLocations(reflection);
        (AttributeLocations, AttributeLocationSpans) = ResolveAttributeLocations();
        SamplerUniformCount = CountSamplerUniforms();
    }

    private uint Compile(ShaderType type, string source, string what)
    {
        var shader = _gl.CreateShader(type);
        _gl.ShaderSource(shader, source);
        _gl.CompileShader(shader);

        var status = _gl.GetShader(shader, GLEnum.CompileStatus);
        if (status == 0)
        {
            // The full info log, not just a status: an ES 3.0 compile failure is usually a
            // precision or qualifier problem, and the log names the line.
            var log = _gl.GetShaderInfoLog(shader);
            _gl.DeleteShader(shader);
            throw new InvalidOperationException($"{what} shader failed to compile:\n{log}");
        }

        return shader;
    }

    private uint Link(uint vertexShader, uint pixelShader)
    {
        var program = _gl.CreateProgram();
        _gl.AttachShader(program, vertexShader);
        _gl.AttachShader(program, pixelShader);
        _gl.LinkProgram(program);

        var status = _gl.GetProgram(program, GLEnum.LinkStatus);
        if (status == 0)
        {
            // The link is the step that catches a varying mismatch, which is the one failure mode
            // the ES dialect adds over desktop GL - see the shader compiler's HarmonizeVaryingNames.
            var log = _gl.GetProgramInfoLog(program);
            _gl.DeleteProgram(program);
            throw new InvalidOperationException($"program failed to link:\n{log}");
        }

        return program;
    }

    /// <summary>
    /// Finds the merged <c>_Global</c> block, assigns it a binding point, and reads back the size
    /// and member offsets the driver chose.
    ///
    /// The reflection's block name comes from the HLSL cbuffer, which the ES pass keeps as
    /// <c>_Global</c>, so the lookup is by that name. A program with no block returns null: the
    /// particle shader's reflection has no uniforms, and looking up a block that is not there
    /// would be a silent <c>GL_INVALID_INDEX</c>.
    /// </summary>
    private GlUniformBlock? ResolveUniformBlock(ShaderReflection reflection)
    {
        if (reflection.Uniforms.Count == 0)
            return null;

        var blockName = "_Global";
        var index = _gl.GetUniformBlockIndex(Handle, blockName);
        if (index == InvalidIndex)
        {
            throw new InvalidOperationException(
                $"The reflection declares {reflection.Uniforms.Count} uniform(s) but the linked program " +
                $"has no uniform block named '{blockName}'. The reflection and the compiled GLSL disagree.");
        }

        // Binding 0 is free here because this program owns its own block; the command buffer
        // rebinds the UBO to the same point before each upload.
        const uint binding = 0;
        _gl.UniformBlockBinding(Handle, index, binding);

        var size = _gl.GetActiveUniformBlock(Handle, index, UniformBlockPName.DataSize);
        var active = _gl.GetActiveUniformBlock(Handle, index, UniformBlockPName.ActiveUniforms);

        // Querying the block's members takes two calls: the block reports how many uniforms it has
        // and their indices, then the offset of each index comes from the block-level query.
        // Reading a member by glGetUniformLocation would work too, but that resolves through the
        // default block and is not what glGetActiveUniforms describes.
        var offsets = new Dictionary<string, int>();
        if (active > 0)
        {
            var indices = new uint[active];
            var blockOffsets = new int[active];

            // The array-taking overloads are the raw-pointer ones, so both the index list and the
            // result list have to be pinned across the pair of calls.
            unsafe
            {
                fixed (uint* indexPointer = indices)
                {
                    _gl.GetActiveUniformBlock(Handle, index, UniformBlockPName.ActiveUniformIndices, (int*)indexPointer);
                    fixed (int* offsetPointer = blockOffsets)
                        _gl.GetActiveUniforms(Handle, (uint)active, indexPointer, UniformPName.Offset, offsetPointer);
                }
            }

            for (var i = 0; i < active; i++)
            {
                var name = _gl.GetActiveUniform(Handle, indices[i], out _, out _);
                var member = MemberName(name, blockName);
                if (member.Length > 0)
                    offsets[member] = blockOffsets[i];
            }
        }

        return new GlUniformBlock(binding, size, offsets);
    }

    /// <summary>
    /// Resolves each reflected texture to its sampler uniform location, in reflection order.
    ///
    /// The name looked up here is the reflected one (<c>ShadowMap0</c>), which only works because
    /// the shader compiler renames the combined samplers to match. A location of -1 is recorded
    /// rather than thrown on, because a texture the compiler optimised out is legitimately absent -
    /// but <see cref="GlCommandBuffer"/> treats a negative location as "bind nothing", so a
    /// mismatch shows up as a missing texture rather than a crash mid-frame.
    /// </summary>
    private int[] ResolveTextureLocations(ShaderReflection reflection)
    {
        var locations = new int[reflection.Textures.Count];
        for (var i = 0; i < reflection.Textures.Count; i++)
            locations[i] = _gl.GetUniformLocation(Handle, reflection.Textures[i].Name);
        return locations;
    }

    /// <summary>
    /// Counts the program's active sampler uniforms by walking the active uniform list.
    ///
    /// The type is the only discriminator that works here. A sampler has no
    /// <c>glGetUniformLocation</c> entry that survives - which is why the count is needed at all -
    /// and the ES 3.0 <c>UniformType</c> enum lays every sampler in one consecutive run
    /// (<c>Sampler1D</c> through <c>SamplerCubeShadow</c> and their array/shadow variants), so the
    /// run is checked as a range rather than by listing the handful of kinds this shader set
    /// happens to use. A new shader reading a sampler buffer would otherwise silently stop being
    /// counted, and the smoke test's check would lose its reference point rather than fail.
    /// </summary>
    private int CountSamplerUniforms()
    {
        var count = 0;
        var active = _gl.GetProgram(Handle, GLEnum.ActiveUniforms);
        for (var i = 0u; i < active; i++)
        {
            _gl.GetActiveUniform(Handle, i, out _, out var type);

            if (type >= Silk.NET.OpenGLES.UniformType.Sampler1D &&
                type <= Silk.NET.OpenGLES.UniformType.SamplerCubeShadow)
            {
                count++;
            }
        }

        return count;
    }

    private (Dictionary<int, string> Names, HashSet<int> Spans) ResolveAttributeLocations()
    {
        // Read from the linked program rather than assumed from the layout qualifiers: the two
        // must agree, and this is the side the driver decides.
        var names = new Dictionary<int, string>();
        var spans = new HashSet<int>();
        var count = _gl.GetProgram(Handle, GLEnum.ActiveAttributes);

        for (var i = 0u; i < count; i++)
        {
            var name = _gl.GetActiveAttrib(Handle, i, out _, out var type);
            if (string.IsNullOrEmpty(name))
                continue;

            var location = _gl.GetAttribLocation(Handle, name);
            names[location] = name;

            for (var occupied = 0; occupied < AttributeSpan(type); occupied++)
                spans.Add(location + occupied);
        }

        return (names, spans);
    }

    /// <summary>
    /// How many consecutive locations an input of this type occupies.
    ///
    /// Only a matrix takes more than one, and only a column-major one at that: the ES GLSL the
    /// bundle carries is column-major (spirv-cross's <c>layout(row_major)</c> workaround functions
    /// exist precisely to undo the HLSL's row-major storage), so a <c>mat4</c> input consumes four
    /// locations start to finish.
    ///
    /// The non-square types are counted from their column count. Only the six core float types are
    /// listed: the ARB and NV spellings are aliases of those with identical values, and the double
    /// and rectangle variants do not exist in ES 3.0 at all - so naming them here would either not
    /// compile or never match.
    /// </summary>
    private static int AttributeSpan(AttributeType type) => type switch
    {
        AttributeType.FloatMat2 => 2,
        AttributeType.FloatMat3 => 3,
        AttributeType.FloatMat4 => 4,
        AttributeType.FloatMat2x3 or AttributeType.FloatMat2x4 => 2,
        AttributeType.FloatMat3x2 or AttributeType.FloatMat3x4 => 3,
        AttributeType.FloatMat4x2 or AttributeType.FloatMat4x3 => 4,
        _ => 1,
    };

    /// <summary>
    /// Strips the block qualifier the driver prefixes onto a uniform-block member's name.
    ///
    /// A member of a named block is reported as <c>&lt;block&gt;.&lt;member&gt;</c> - ANGLE returns
    /// <c>_Global.Projection</c> for a member the reflection and the GLSL both call plain
    /// <c>Projection</c>. The reflection's names are bare, so the prefix has to go or every lookup
    /// misses, which is exactly what happened before this existed: the block size matched, every
    /// offset the driver reported was right, and the smoke test still reported that nothing was in
    /// the block at all.
    ///
    /// Only a leading <c>&lt;blockName&gt;.</c> is removed. A member that is itself an array keeps its
    /// own suffix - <c>_Global.Weights[0]</c> becomes <c>Weights[0]</c> - so the array case stays
    /// distinguishable rather than being quietly folded onto the scalar name.
    /// </summary>
    private static string MemberName(string reported, string blockName)
    {
        if (string.IsNullOrEmpty(reported))
            return "";

        var prefix = blockName + ".";
        return reported.StartsWith(prefix, StringComparison.Ordinal)
            ? reported[prefix.Length..]
            : reported;
    }

    /// <summary>GL's sentinel for "no such index", returned by <c>glGetUniformBlockIndex</c>.</summary>
    private const uint InvalidIndex = 0xFFFFFFFF;

    public void Dispose() => _deletions.Request(GlObjectKind.Program, Handle);
}
