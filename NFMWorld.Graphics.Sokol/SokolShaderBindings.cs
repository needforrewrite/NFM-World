using NFMWorld.Shaders;
using SharpSokol.Native;

namespace NFMWorld.Graphics.Sokol;

/// <summary>
/// The tables sokol needs to describe a program, derived from a <see cref="ShaderReflection"/>.
///
/// sokol never inspects a shader, so everything a backend needs to bind registers has to be
/// declared up front in <c>sg_shader_desc</c> - and, because that descriptor is also what
/// <c>sg_apply_uniforms</c>' slot and <c>sg_bindings</c>' indices refer to, the same tables are
/// what translate the abstraction's flat slots into sokol's. Deriving them once, here, is what
/// keeps <see cref="SokolGraphicsDevice.CreateShader"/> and the command buffer's binding logic
/// from drifting apart.
///
/// The one genuinely awkward part is stage ownership. The shader compiler emits a program-level
/// reflection: <c>_Global</c> is one merged constant buffer, and the texture/sampler lists are the
/// union of both stages' with pipeline-wide slot numbers. sokol, however, is per-stage - a view
/// declares exactly one stage, and D3D11 binds it into that stage's register file - so a texture
/// read by both stages needs two view entries.
///
/// That is resolved by emitting every binding into <em>both</em> stages. That is safe in
/// D3D11, where binding a shader-resource view to a register the shader does not declare is
/// legal (the register is simply ignored), and it keeps the register numbers identical to the
/// merged slots - which is what the emitted HLSL uses, since the shader compiler renumbers the
/// SPIR-V bindings and then generates HLSL from that same SPIR-V.
///
/// The uniform blocks are the same story with one extra turn: a program keeps one merged
/// <c>_Global</c> cbuffer read by both stages, so each stage gets its own descriptor entry - but on
/// the GL backend that merged block is too large for sokol to describe at all, and is split first.
/// See <see cref="UniformBlock"/>.
/// </summary>
internal sealed class ShaderBindings
{
    /// <summary>The two stages a sokol pipeline can hold. Order matters only for readability - every binding is emitted into both.</summary>
    private static readonly sg_shader_stage[] Stages = [sg_shader_stage.SG_SHADERSTAGE_VERTEX, sg_shader_stage.SG_SHADERSTAGE_FRAGMENT];

    /// <summary>
    /// Builds the tables for a program.
    ///
    /// <paramref name="vertexGlsl"/>/<paramref name="pixelGlsl"/> are only read on the GL backend,
    /// and only to split the uniform block; on every other backend they are unused and the single
    /// merged block the compiler emits is described as-is.
    /// </summary>
    public ShaderBindings(ShaderReflection reflection, string? vertexGlsl = null, string? pixelGlsl = null)
    {
        // The flat buffer's total size is a property of the reflection alone and does not depend on
        // how the block is later described. Everything that indexes it - SetUniform's byte offsets,
        // the transposed-matrix lookup, the shared accumulation buffer - is therefore unchanged by
        // the split below, which is what makes the split a pure descriptor concern.
        UniformBlockSize = ComputeUniformBlockSize(reflection);

        if (UniformBlockSize > 0)
            DescribeUniformBlocks(reflection, vertexGlsl, pixelGlsl);

        // One view/sampler/pair set per stage, both pointing at the same registers. Views and
        // samplers are separate namespaces in sokol, so they are counted independently even
        // though the abstraction pairs them by slot.
        ViewSlotMap = new int[reflection.Textures.Count][];
        for (var i = 0; i < reflection.Textures.Count; i++)
        {
            var slots = new int[Stages.Length];
            for (var s = 0; s < Stages.Length; s++)
            {
                slots[s] = ViewCount;
                ViewStages.Add(Stages[s]);
                ViewRegisters.Add((byte)reflection.Textures[i].Slot);
                ViewCount++;
            }
            ViewSlotMap[i] = slots;
        }

        SamplerSlotMap = new int[reflection.Samplers.Count][];
        for (var i = 0; i < reflection.Samplers.Count; i++)
        {
            var slots = new int[Stages.Length];
            for (var s = 0; s < Stages.Length; s++)
            {
                slots[s] = SamplerCount;
                SamplerStages.Add(Stages[s]);
                SamplerRegisters.Add((byte)reflection.Samplers[i].Slot);
                SamplerCount++;
            }
            SamplerSlotMap[i] = slots;
        }

        // Every declared view and sampler must be referenced by a texture-sampler pair whose
        // stage matches both (_sg_validate_shader_desc enforces this with an exact slot-mask
        // comparison), so the pairs are the cross product of the two tables per stage.
        for (var i = 0; i < reflection.Textures.Count && i < reflection.Samplers.Count; i++)
        {
            for (var s = 0; s < Stages.Length; s++)
            {
                PairStages.Add(Stages[s]);
                PairViewSlots.Add(ViewSlotMap[i][s]);
                PairSamplerSlots.Add(SamplerSlotMap[i][s]);

                // GL resolves a combined sampler by name through glGetUniformLocation, and rejects a
                // pair without one (VALIDATE_SHADERDESC_TEXTURE_SAMPLER_PAIR_GLSL_NAME). The name is
                // the texture's own, which is what NameCombinedSamplers renames the GLSL variable to
                // - so the descriptor and the source agree. Only GL reads this field.
                PairGlslNames.Add(reflection.Textures[i].Name);
                PairCount++;
            }
        }

        // Every view and sampler slot the descriptor declares must be named, and _sg_validate_shader_desc
        // walks the declarations rather than the pairs - so the named set has to cover the full range of
        // slots, which ShaderBindings numbers sequentially from zero.
        DeclaredViewSlots = ViewCount;
        DeclaredSamplerSlots = SamplerCount;
    }

    /// <summary>
    /// One uniform block as the shader descriptor will declare it: which stage reads it, where its
    /// members sit in the program's flat uniform buffer, and what the GL backend needs to resolve them.
    /// </summary>
    /// <param name="Stage">The stage this entry belongs to. sokol's descriptor holds one stage per entry.</param>
    /// <param name="Slot">
    /// The descriptor index this entry occupies, which is the slot <c>sg_apply_uniforms</c> takes.
    /// It is <em>not</em> the HLSL register: the two coincide on GL, where one program serves both
    /// stages, but not on D3D11, where the vertex and fragment blocks are separate descriptor slots
    /// that both bind <c>b0</c> because a register is a per-stage namespace. Handing the register to
    /// <c>sg_apply_uniforms</c> there would apply the vertex block twice and leave the fragment
    /// block's slot unfilled, and sokol's draw validation requires an apply per <em>slot</em>
    /// (<c>_sg_shader_common_init</c> sets the required mask bit from the loop index, and
    /// <c>VALIDATE_DRAW_REQUIRED_BINDINGS_OR_UNIFORMS_MISSING</c> compares it for equality).
    /// </param>
    /// <param name="Register">The HLSL register <c>bn</c> - D3D11's binding, unused by GL.</param>
    /// <param name="Offset">
    /// Where this block starts in the flat buffer. <c>sg_apply_uniforms</c> is handed
    /// <c>_uniformBlock + Offset</c> for exactly <paramref name="Size"/> bytes.
    /// </param>
    /// <param name="Size">The block's declared size, which sokol requires the applied range to match exactly.</param>
    /// <param name="Members">
    /// The members the GL backend must resolve by name, or null on a backend that reads the merged
    /// block without needing names (D3D11 and Metal bind by register; Vulkan reads only the set/binding
    /// number). Null therefore means "declare the block, but with no member table".
    /// </param>
    internal sealed record UniformBlock(
        sg_shader_stage Stage,
        int Slot,
        byte Register,
        int Offset,
        int Size,
        IReadOnlyList<SokolGlslUniformBlocks.Member>? Members);

    public int UniformBlockSize { get; }

    /// <summary>
    /// The vertex stage's GLSL as it must be compiled, with the uniform block split to match the
    /// descriptor built here - and <see langword="null"/> on every backend but GL, which is the only
    /// one that compiles GLSL at all. A GL caller has to compile <em>this</em> rather than the source
    /// it passed in: the two differ exactly where the block was rewritten, and compiling the
    /// original is what leaves the descriptor naming members the GLSL never declares.
    /// </summary>
    public string? VertexSource { get; private set; }

    /// <summary>The pixel stage's compile-ready GLSL - see <see cref="VertexSource"/>.</summary>
    public string? PixelSource { get; private set; }

    /// <summary>
    /// The uniform blocks to declare, in descriptor order. One per stage normally; several per stage
    /// on GL, where the merged block is split to fit sokol's 16-member cap. The command buffer draws
    /// the uploaded bytes for each one from <see cref="UniformBlock.Offset"/>.
    /// </summary>
    public List<UniformBlock> UniformBlocks { get; } = [];

    public int ViewCount;
    public List<sg_shader_stage> ViewStages { get; } = [];
    public List<byte> ViewRegisters { get; } = [];

    public int SamplerCount;
    public List<sg_shader_stage> SamplerStages { get; } = [];
    public List<byte> SamplerRegisters { get; } = [];

    public int PairCount;
    public List<sg_shader_stage> PairStages { get; } = [];
    public List<int> PairViewSlots { get; } = [];
    public List<int> PairSamplerSlots { get; } = [];

    /// <summary>
    /// Each pair's GL sampler name, parallel to <see cref="PairStages"/>. GL resolves a combined
    /// sampler by this name; the other backends bind the pair by slot and ignore it.
    /// </summary>
    public List<string> PairGlslNames { get; } = [];

    public int DeclaredViewSlots { get; }
    public int DeclaredSamplerSlots { get; }

    /// <summary>For each abstraction texture slot, the sokol view slots to bind - see <see cref="SokolPipelineState.ViewSlotMap"/>.</summary>
    public int[][] ViewSlotMap { get; }

    /// <summary>The same translation for samplers.</summary>
    public int[][] SamplerSlotMap { get; }

    /// <summary>
    /// Fills <see cref="UniformBlocks"/>: either the merged block, once per stage, or - on GL - the
    /// split of it, once per stage per piece.
    ///
    /// <para>
    /// The split is decided by the backend rather than by the reflection because it exists solely to
    /// satisfy sokol's GL descriptor. D3D11, Metal and Vulkan all describe the merged block fine, and
    /// D3D11 in particular <em>requires</em> the merge: the HLSL the compiler emits reads one
    /// <c>_Global</c> at <c>register(b0)</c>, so splitting the descriptor there would leave every
    /// uniform past the first block unbound.
    /// </para>
    /// </summary>
    private void DescribeUniformBlocks(ShaderReflection reflection, string? vertexGlsl, string? pixelGlsl)
    {
        if (Gfx.query_backend() != sg_backend.SG_BACKEND_GLCORE)
        {
            // Both stages read the same merged cbuffer, so the same bytes are applied to each
            // stage's block - but each is a descriptor slot of its own, so the slot advances while
            // the register stays at b0 (a register is a per-stage namespace, so VS and FS both
            // declaring b0 is not a collision).
            for (var slot = 0; slot < Stages.Length; slot++)
                UniformBlocks.Add(new UniformBlock(Stages[slot], slot, 0, 0, UniformBlockSize, null));
            return;
        }

        // The two stages declare identical member lists (verified for every program in this repo),
        // so splitting both should yield the same block geometry - the same count, offsets and sizes
        // - while the member <em>names</em> differ, because the block's instance name is a SPIR-V id
        // that is unique per stage.
        var vertex = Split(vertexGlsl, reflection, ShaderStage.Vertex, "vertex");
        var pixel = Split(pixelGlsl, reflection, ShaderStage.Pixel, "pixel");

        // The descriptor's member names are the split ones (`_Global_g1.LightViewProj0`), so the
        // source that gets compiled has to be the rewritten one - handing sokol the original would
        // leave the descriptor naming members that do not exist anywhere in the GLSL, and every
        // lookup would come back -1. That is not a loud failure: sokol warns once per name and then
        // skips the uniform, so the symptom is wrong rendering with the log as the only clue.
        VertexSource = vertex.Source;
        PixelSource = pixel.Source;

        if (vertex.Blocks.Count != pixel.Blocks.Count)
            throw new InvalidOperationException(
                $"The vertex and pixel stages of this program split into {vertex.Blocks.Count} and " +
                $"{pixel.Blocks.Count} uniform blocks. sokol applies a block to one stage at a time, so the " +
                "two stages have to agree on how many there are - which they do as long as they " +
                "declare the same members, as every program here does.");

        for (var i = 0; i < vertex.Blocks.Count; i++)
        {
            var vertexBlock = vertex.Blocks[i];
            var pixelBlock = pixel.Blocks[i];
            if (vertexBlock.BlockOffset != pixelBlock.BlockOffset || vertexBlock.SizeInBytes != pixelBlock.SizeInBytes)
                throw new InvalidOperationException(
                    $"The vertex and pixel stages disagree about uniform block {i}: " +
                    $"offset/size {vertexBlock.BlockOffset}/{vertexBlock.SizeInBytes} against " +
                    $"{pixelBlock.BlockOffset}/{pixelBlock.SizeInBytes}.");

            // Each stage gets its own entry, because a descriptor entry names exactly one stage -
            // so a program split into three blocks declares six entries, three per stage. The
            // register is the split index; the slot is simply where the entry lands.
            if (vertexBlock.Members.Count > 0)
                UniformBlocks.Add(Block(Stages[0], UniformBlocks.Count, i, vertexBlock));
            if (pixelBlock.Members.Count > 0)
                UniformBlocks.Add(Block(Stages[1], UniformBlocks.Count, i, pixelBlock));
        }
    }

    private static UniformBlock Block(
        sg_shader_stage stage, int slot, int index, SokolGlslUniformBlocks.Block block) =>
        new(stage, slot, (byte)index, block.BlockOffset, block.SizeInBytes, block.Members);

    private static SokolGlslUniformBlocks.Split Split(
        string? source, ShaderReflection reflection, ShaderStage stage, string which)
    {
        if (string.IsNullOrEmpty(source))
            throw new InvalidOperationException(
                $"The {which} stage has no GLSL source, but the GL backend compiles GLSL and has " +
                "nothing else it can use. LoadProgram should have selected the Glsl form for this " +
                "backend - see its GLCORE branch.");

        var split = SokolGlslUniformBlocks.Apply(source, reflection, stage);
        if (split.Blocks.Count == 0)
            throw new InvalidOperationException(
                $"The {which} stage's GLSL declares no uniform block, but the program's reflection " +
                "has uniforms. Every reflected uniform has to be declared by the shader it is " +
                "uploaded to, or sokol has nothing to describe and the values are silently dropped.");

        return split;
    }

    /// <summary>
    /// The constant buffer's size, rounded up to D3D11's 16-byte constant-buffer boundary.
    ///
    /// <c>sg_apply_uniforms</c> asserts the range it is given is <em>exactly</em>
    /// <c>uniform_blocks[slot].size</c>, and sokol allocates the D3D11 buffer as
    /// <c>_sg_roundup_pow2(size, 16)</c> - so the declared size is the block's highest write plus
    /// padding, which is what a caller's byte offsets index into.
    /// </summary>
    private static int ComputeUniformBlockSize(ShaderReflection reflection)
    {
        var end = 0;
        foreach (var uniform in reflection.Uniforms)
            end = Math.Max(end, uniform.Offset + uniform.SizeInBytes);

        // A block size of zero is rejected by sokol (_SG_VALIDATE(size > 0)), so a program with no
        // uniforms simply declares no block at all.
        return end == 0 ? 0 : (end + 15) & ~15;
    }
}
