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
/// This class resolves that by emitting every binding into <em>both</em> stages. That is safe in
/// D3D11, where binding a shader-resource view to a register the shader does not declare is
/// legal (the register is simply ignored), and it keeps the register numbers identical to the
/// merged slots - which is what the emitted HLSL uses, since the shader compiler renumbers the
/// SPIR-V bindings and then generates HLSL from that same SPIR-V.
/// </summary>
internal sealed class ShaderBindings
{
    /// <summary>The two stages a sokol pipeline can hold. Order matters only for readability - every binding is emitted into both.</summary>
    private static readonly sg_shader_stage[] Stages = [sg_shader_stage.SG_SHADERSTAGE_VERTEX, sg_shader_stage.SG_SHADERSTAGE_FRAGMENT];

    public ShaderBindings(ShaderReflection reflection)
    {
        // The merged _Global block, declared once per stage, all reading register(b0). sokol
        // allocates a separate D3D11 constant buffer per entry, so the same bytes are applied to
        // each stage's buffer - which is what UniformBlockSlots drives in the command buffer.
        UniformBlockSize = ComputeUniformBlockSize(reflection);
        if (UniformBlockSize > 0)
        {
            foreach (var stage in Stages)
            {
                UniformBlockStages.Add(stage);
                UniformBlockSlots.Add(UniformBlockSlots.Count);
            }
        }

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
                PairCount++;
            }
        }
    }

    public int UniformBlockSize { get; }

    /// <summary>The sokol uniform-block slots to apply per draw - one per stage.</summary>
    public List<int> UniformBlockSlots { get; } = [];

    public List<sg_shader_stage> UniformBlockStages { get; } = [];

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

    /// <summary>For each abstraction texture slot, the sokol view slots to bind - see <see cref="SokolPipelineState.ViewSlotMap"/>.</summary>
    public int[][] ViewSlotMap { get; }

    /// <summary>The same translation for samplers.</summary>
    public int[][] SamplerSlotMap { get; }

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
