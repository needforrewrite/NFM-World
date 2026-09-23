using System.Runtime.InteropServices;
using NFMWorld.Shaders;
using SharpSokol.Native;

namespace NFMWorld.Graphics.Sokol;

/// <summary>
/// A resource whose CPU-side contents are uploaded to the GPU at draw time rather than at the
/// moment the caller writes them.
///
/// The deferral exists because sokol allows at most one <c>sg_update_buffer</c>/<c>sg_update_image</c>
/// per resource per frame (<c>VALIDATE_UPDATEBUF_ONCE</c>/<c>VALIDATE_UPDIMG_ONCE</c>, keyed on
/// <c>_sg.frame_index</c>), while the abstraction lets a caller update the same resource as often
/// as it likes within one frame. Buffering the writes in a mirror and flushing once just before the
/// draw is what reconciles the two - and it is also what makes a sub-range update possible at all,
/// since neither sokol call takes an offset for the bytes it is leaving alone.
/// </summary>
internal interface IPendingUpload
{
    bool UploadPending { get; }

    /// <summary>Flushes the mirror to the GPU, unless it is already current for <paramref name="frame"/>.</summary>
    void Upload(long frame);
}

/// <summary>
/// sokol_gfx handles are small value structs (a 32-bit id and nothing else), so unlike the FNA3D
/// backend there is no pointer to null out on dispose - "already disposed" is tracked explicitly.
/// Disposal is deferred by sokol rather than immediate: a destroyed resource stays alive until the
/// next <c>sg_commit</c>, so destroying one a frame in flight still referenced is legal.
/// </summary>
internal sealed class SokolBuffer(sg_buffer handle, BufferDesc desc) : IBuffer, IPendingUpload
{
    public sg_buffer Handle { get; private set; } = handle;
    public bool IsValid => Handle.id != 0;

    public BufferKind Kind { get; } = desc.Kind;
    public BufferUsage Usage { get; } = desc.Usage;
    public int SizeInBytes { get; } = desc.SizeInBytes;
    public IndexFormat IndexFormat { get; } = desc.IndexFormat;

    /// <summary>
    /// The buffer's CPU-side contents, kept only for a mutable buffer.
    ///
    /// <c>sg_update_buffer</c> replaces a whole buffer - it has no offset argument - and sokol
    /// offers no readback, so a sub-range update cannot preserve the bytes outside its range
    /// without a copy of them. The mirror is that copy. It is also what lets several updates in
    /// one frame coalesce into the single <c>sg_update_buffer</c> call sokol allows per buffer per
    /// frame (<c>VALIDATE_UPDATEBUF_ONCE</c>).
    /// </summary>
    internal byte[]? Mirror { get; set; }

    public bool UploadPending { get; private set; }

    private long _lastUploadFrame = -1;

    /// <summary>Records that <see cref="Mirror"/> has changes the GPU copy has not seen.</summary>
    internal void MarkDirty() => UploadPending = true;

    public unsafe void Upload(long frame)
    {
        if (!UploadPending || _lastUploadFrame == frame) return;

        var mirror = Mirror ?? throw new InvalidOperationException(
            "A buffer marked for upload has no CPU mirror; only a mutable buffer is ever marked.");
        fixed (byte* pointer = mirror)
        {
            var range = SokolNative.Range(pointer, mirror.Length);
            Gfx.update_buffer(Handle, &range);
        }

        UploadPending = false;
        _lastUploadFrame = frame;
    }

    public void Dispose()
    {
        if (!IsValid) return;
        Gfx.destroy_buffer(Handle);
        Handle = default;
        Mirror = null;
        UploadPending = false;
    }
}

/// <summary>
/// An image, the texture view that binds it for sampling, and a CPU mirror of mip 0.
///
/// The view is created with the image because sokol binds <em>views</em>, never bare images - a
/// texture that can be sampled needs one from the moment it exists. A render target additionally
/// gets attachment views (owned by <see cref="SokolRenderTarget"/>) while sharing this sampling
/// view, which is what lets a rendered-to image also be read as a texture.
///
/// The mirror exists because sokol_gfx has no GPU readback of any kind: there is no
/// <c>sg_read_texture</c>, and the D3D11 backend never issues a staging copy. That also means a
/// sub-rectangle update cannot preserve the pixels outside the updated region by reading them
/// back, so the mirror is what makes <c>UpdateTexture</c> non-destructive.
/// </summary>
internal sealed class SokolTexture(sg_image handle, TextureDesc desc, int mipCount, bool updatable) : ITexture, IPendingUpload
{
    public sg_image Handle { get; private set; } = handle;
    public bool IsValid => Handle.id != 0;

    public int Width { get; } = desc.Width;
    public int Height { get; } = desc.Height;
    public TextureFormat Format { get; } = desc.Format;

    /// <summary>The view bound for sampling - see <see cref="ICommandBuffer.SetShaderResource"/>.</summary>
    public sg_view View { get; set; }

    /// <summary>
    /// Levels in the image's chain. Sokol demands data for <em>every</em> level of a multi-level
    /// image on both the descriptor and the update path (<c>_sg_validate_image_data</c> loops to
    /// <c>num_mipmaps</c> and requires each level to be non-empty), so the backend must always
    /// supply a whole chain, never just level 0.
    /// </summary>
    public int MipCount { get; } = mipCount;

    /// <summary>
    /// Whether this texture's image was created with <c>usage.dynamic_update</c>, i.e. whether it
    /// can be re-uploaded through <c>sg_update_image</c> at all. An immutable image took its
    /// contents from its descriptor and can never be updated.
    /// </summary>
    public bool Updatable { get; } = updatable;

    /// <summary>
    /// Whether a CPU mirror is worth keeping. Only plain (non-block-compressed) formats: a
    /// compressed block cannot be partially rewritten without re-encoding it, and a render target's
    /// contents are owned by the GPU, so neither has anything meaningful to mirror.
    /// </summary>
    public bool CanMirror { get; } = !desc.RenderTargetable && IsMirrorableFormat(desc.Format);

    /// <summary>
    /// Whether a format has a byte-addressable CPU mirror. The block-compressed formats do not: a
    /// block cannot be partially rewritten without re-encoding it, so neither a mirror nor a
    /// sub-rectangle update means anything for them.
    ///
    /// <see cref="TextureFormat.Single"/> is included, but only for mirroring - reading back the
    /// raw bytes of a float texel is exact, whereas <em>averaging</em> those bytes to build a mip
    /// level is not (see <see cref="CanGenerateMips"/>). The two capabilities are genuinely
    /// different, which is why they are two predicates rather than one.
    /// </summary>
    internal static bool IsMirrorableFormat(TextureFormat format) => format is
        TextureFormat.Rgba8 or TextureFormat.Bgra8 or TextureFormat.R8 or TextureFormat.Single;

    /// <summary>
    /// Whether a format's mip levels can be built on the CPU by averaging. Only byte-per-channel
    /// formats can: a <see cref="TextureFormat.Single"/> texel's four bytes are one float, so
    /// averaging them averages its encoding rather than its value.
    /// </summary>
    internal static bool CanGenerateMips(TextureFormat format) => format is
        TextureFormat.Rgba8 or TextureFormat.Bgra8 or TextureFormat.R8;

    /// <summary>Mirror of mip 0, or null when this texture cannot or need not be mirrored.</summary>
    internal byte[]? CpuShadow { get; set; }

    public bool UploadPending { get; private set; }

    private long _lastUploadFrame = -1;

    /// <summary>Records that <see cref="CpuShadow"/> has changes the GPU copy has not seen.</summary>
    internal void MarkDirty() => UploadPending = true;

    /// <summary>Bytes per pixel for the mirrorable formats, or 0 for the block-compressed ones.</summary>
    internal int BytesPerPixel => Format switch
    {
        TextureFormat.Rgba8 or TextureFormat.Bgra8 => 4,
        TextureFormat.R8 => 1,
        TextureFormat.Single => 4,
        _ => 0,
    };

    /// <summary>
    /// Sends the whole chain, regenerating the levels below mip 0 from the mirror first.
    ///
    /// Sokol has no sub-rectangle update for a sealed image, so the smallest upload is one whole
    /// level - and since a level below 0 must be regenerated anyway, every level goes every time.
    /// That is why updates write into <see cref="CpuShadow"/> first: the bytes outside the updated
    /// rectangle are the ones the mirror already holds, where a fresh staging buffer would silently
    /// zero them.
    /// </summary>
    public unsafe void Upload(long frame)
    {
        if (!UploadPending || _lastUploadFrame == frame) return;

        if (!Updatable)
            throw new NotSupportedException(
                $"{Format} texture was created immutable, so it cannot be updated. A texture is only " +
                "updatable when it is single-level and its format has a CPU mirror - see CreateTexture.");

        // sg_image_data holds each level by pointer and nothing about the array is pinned, so the
        // levels are pinned one at a time and kept alive until after the call.
        var chain = BuildMipChain(Width, Height, MipCount, Format, CpuShadow ?? throw new InvalidOperationException(
            "A texture with no CPU mirror has nothing to upload."));
        var handles = new GCHandle[chain.Length];
        try
        {
            var data = new sg_image_data();
            for (var level = 0; level < chain.Length; level++)
            {
                handles[level] = GCHandle.Alloc(chain[level], GCHandleType.Pinned);
                var pointer = (byte*)handles[level].AddrOfPinnedObject();
                data.mip_levels[level] = SokolNative.Range(pointer, chain[level].Length);
            }
            Gfx.update_image(Handle, &data);
        }
        finally
        {
            foreach (var handle in handles)
                if (handle.IsAllocated) handle.Free();
        }

        UploadPending = false;
        _lastUploadFrame = frame;
    }

    /// <summary>
    /// Level 0 (the caller's bytes) plus every generated level beneath it. Each following level is
    /// a 2x2 box filter of the one above, which is what FNA3D's own generated chain does and is
    /// close enough to D3D11's generator for the chain's only job here (letting a minified sample
    /// pick a smaller level).
    /// </summary>
    internal static byte[][] BuildMipChain(int width, int height, int mipCount, TextureFormat format, ReadOnlySpan<byte> level0)
    {
        var levels = new byte[mipCount][];
        levels[0] = level0.ToArray();
        if (mipCount == 1) return levels;

        var bpp = format switch
        {
            TextureFormat.Rgba8 or TextureFormat.Bgra8 => 4,
            TextureFormat.R8 => 1,
            _ => 0,
        };
        if (bpp == 0)
            throw new NotSupportedException($"{format} has no byte-per-channel layout, so its mip chain cannot be generated on the CPU.");

        var sourceWidth = width;
        var sourceHeight = height;
        for (var level = 1; level < mipCount; level++)
        {
            var levelWidth = Math.Max(sourceWidth >> 1, 1);
            var levelHeight = Math.Max(sourceHeight >> 1, 1);
            var source = levels[level - 1];
            var destination = new byte[levelWidth * levelHeight * bpp];

            for (var y = 0; y < levelHeight; y++)
            {
                for (var x = 0; x < levelWidth; x++)
                {
                    for (var channel = 0; channel < bpp; channel++)
                    {
                        var sum = 0;
                        for (var dy = 0; dy < 2; dy++)
                        {
                            for (var dx = 0; dx < 2; dx++)
                            {
                                // Clamped rather than skipped so an odd dimension still averages
                                // four taps, reusing the edge texel - dropping the tap instead
                                // would darken every edge of an odd-sized level.
                                var sx = Math.Min(x * 2 + dx, sourceWidth - 1);
                                var sy = Math.Min(y * 2 + dy, sourceHeight - 1);
                                sum += source[(sy * sourceWidth + sx) * bpp + channel];
                            }
                        }
                        destination[(y * levelWidth + x) * bpp + channel] = (byte)((sum + 2) >> 2);
                    }
                }
            }

            levels[level] = destination;
            sourceWidth = levelWidth;
            sourceHeight = levelHeight;
        }

        return levels;
    }

    public void Dispose()
    {
        if (!IsValid) return;
        // The sampling view references the image, so it has to go first.
        if (View.id != 0) { Gfx.destroy_view(View); View = default; }
        Gfx.destroy_image(Handle);
        Handle = default;
        CpuShadow = null;
        UploadPending = false;
    }
}

/// <summary>
/// Unlike FNA3D - which applies sampler state per bind through <c>FNA3D_VerifySampler</c> - sokol
/// has a real persistent sampler object. It cannot be bound on its own, though: a shader declares
/// texture-sampler <em>pairs</em> and bindings apply as one set, so a sampler is only ever useful
/// next to the texture it is paired with in <see cref="ICommandBuffer.SetShaderResource"/>.
/// </summary>
internal sealed class SokolSampler(sg_sampler handle, SamplerDesc desc) : ISampler
{
    public sg_sampler Handle { get; private set; } = handle;
    public bool IsValid => Handle.id != 0;

    public SamplerDesc Desc { get; } = desc;

    public void Dispose()
    {
        if (!IsValid) return;
        Gfx.destroy_sampler(Handle);
        Handle = default;
    }
}

/// <summary>
/// In this sokol version a render target is a pair of attachment <em>views</em> rather than a pass
/// object: the textures own the images, and these views are what <c>sg_begin_pass</c> attaches.
/// They reference their images, so they are destroyed first.
/// </summary>
internal sealed class SokolRenderTarget(
    ITexture colorTexture,
    ITexture? depthStencilTexture,
    sg_view colorAttachmentView,
    sg_view depthStencilAttachmentView) : IRenderTarget
{
    /// <summary>The colour attachment view - see <see cref="SokolCommandBuffer"/>'s pass handling.</summary>
    public sg_view ColorAttachmentView { get; private set; } = colorAttachmentView;

    /// <summary>The depth-stencil attachment view, or a null view when there is no depth buffer.</summary>
    public sg_view DepthStencilAttachmentView { get; private set; } = depthStencilAttachmentView;

    public ITexture ColorTexture { get; } = colorTexture;
    public ITexture? DepthStencilTexture { get; } = depthStencilTexture;

    public void Dispose()
    {
        // The attachment views are owned here; the textures own their sampling views and images.
        if (DepthStencilAttachmentView.id != 0) { Gfx.destroy_view(DepthStencilAttachmentView); DepthStencilAttachmentView = default; }
        if (ColorAttachmentView.id != 0) { Gfx.destroy_view(ColorAttachmentView); ColorAttachmentView = default; }
        ColorTexture.Dispose();
        DepthStencilTexture?.Dispose();
    }
}

/// <summary>
/// One shader program's two stages, as a pipeline's <c>VertexShader</c>/<c>PixelShader</c> pair.
///
/// The FNA3D backend's module is a whole compiled Effect, because that is all FNA3D accepts. sokol
/// is the other shape: <c>sg_make_shader</c> takes the vertex and fragment functions together in
/// one call, because a pipeline can only be built from a matched pair. So the natural unit here is
/// the program, not the stage - and the abstraction lets the same instance be passed as both
/// pipeline shaders, which is exactly how the app already uses it (see
/// <c>Effects.Initialize</c>: <c>VertexShader: Line.Module, PixelShader: Line.Module</c>).
///
/// <see cref="ShaderStage"/> is therefore fixed to <see cref="ShaderStage.Vertex"/>, matching the
/// FNA3D module's convention of carrying a stage field that the backend does not actually vary on.
///
/// The reflection is a property of the program rather than of a stage for the same reason: the
/// shader compiler merges both stages into one uniform block and one texture/sampler namespace.
/// </summary>
internal sealed class SokolShaderProgram(
    ReadOnlyMemory<byte> vertexBytecode,
    ReadOnlyMemory<byte> pixelBytecode,
    ShaderReflection reflection,
    string? vertexSource,
    string? pixelSource) : IShaderModule
{
    public ShaderStage Stage => ShaderStage.Vertex;
    public ReadOnlyMemory<byte> Bytecode { get; } = vertexBytecode;

    /// <summary>The vertex stage's SPIR-V, or empty when the program was loaded from source only.</summary>
    public ReadOnlyMemory<byte> VertexBytecode { get; } = vertexBytecode;

    /// <summary>The pixel stage's SPIR-V, or empty when the program was loaded from source only.</summary>
    public ReadOnlyMemory<byte> PixelBytecode { get; } = pixelBytecode;

    /// <summary>
    /// Vertex-stage HLSL to compile at <c>sg_make_shader</c> time, or null to hand
    /// <see cref="VertexBytecode"/> to the backend instead.
    ///
    /// This branch exists because sokol's D3D11 backend does not translate bytecode. It passes
    /// <c>bytecode.ptr</c> straight to <c>CreateVertexShader</c> as a DXBC blob, or - when
    /// <c>source</c> is set instead - calls D3DCompile on it (sokol_gfx.h:14318 and :14343). The
    /// shader compiler's emitted bundles carry SPIR-V, which a D3D11-only build of the native
    /// library cannot consume, so on this backend a program must also carry the HLSL that
    /// spirv-cross emits alongside the SPIR-V (see <c>SpirvCrossReflector.StageResult.Hlsl</c>).
    /// </summary>
    public string? VertexSource { get; } = vertexSource;

    /// <summary>Pixel-stage HLSL - see <see cref="VertexSource"/>.</summary>
    public string? PixelSource { get; } = pixelSource;

    /// <summary>
    /// Entry point name in the source/bytecode. spirv-cross renames the entry point to
    /// <c>main</c> in the HLSL it emits, and sokol's own default is also <c>main</c>.
    /// </summary>
    public string EntryPoint { get; } = "main";

    /// <summary>The program's reflection - sokol never inspects a shader, so every binding number comes from here.</summary>
    public ShaderReflection Reflection { get; } = reflection;
}

/// <summary>
/// A built pipeline: sokol's pipeline handle, the shader backing it, and the tables that translate
/// the abstraction's binding slots into sokol's.
///
/// sokol has no ownership relation between a pipeline and its shader, but the pipeline's validation
/// reads the shader's reflection on every draw, so the shader must outlive the pipeline - and since
/// <see cref="SokolGraphicsDevice.CreatePipeline"/> creates it, the pipeline is its sole owner.
/// </summary>
internal sealed class SokolPipelineState(
    sg_pipeline handle,
    sg_shader shader,
    PipelineDesc desc,
    ShaderReflection reflection,
    int uniformBlockSize,
    IReadOnlyList<int> uniformBlockSlots,
    int[][] viewSlotMap,
    int[][] samplerSlotMap) : IPipelineState
{
    public sg_pipeline Handle { get; private set; } = handle;
    public sg_shader ShaderHandle { get; } = shader;

    public PipelineDesc Desc { get; } = desc;
    public ShaderReflection Reflection { get; } = reflection;

    /// <summary>
    /// Total size in bytes of the program's uniform block, rounded up to a 16-byte constant-buffer
    /// boundary. A caller's uniform writes are byte offsets into this block - see
    /// <see cref="ICommandBuffer.SetUniform"/> - and sokol requires the range handed to
    /// <c>sg_apply_uniforms</c> to be exactly the size declared in the shader desc.
    /// </summary>
    public int UniformBlockSize { get; } = uniformBlockSize;

    /// <summary>
    /// The sokol uniform-block slots this pipeline's shader declares. A program keeps one
    /// <c>_Global</c> cbuffer at <c>register(b0)</c> read by both stages, so the same bytes are
    /// applied once per stage slot.
    /// </summary>
    public IReadOnlyList<int> UniformBlockSlots { get; } = uniformBlockSlots;

    /// <summary>
    /// For each of the abstraction's texture slots, the sokol view slots it feeds. One abstraction
    /// slot maps to two sokol slots (one per stage) because the shader compiler merges the stages'
    /// texture lists into a single namespace, while sokol needs a separate view entry per stage.
    /// </summary>
    public int[][] ViewSlotMap { get; } = viewSlotMap;

    /// <summary>The same translation for samplers - see <see cref="ViewSlotMap"/>.</summary>
    public int[][] SamplerSlotMap { get; } = samplerSlotMap;

    public void Dispose()
    {
        if (Handle.id == 0) return;
        Gfx.destroy_pipeline(Handle);
        Handle = default;
        if (ShaderHandle.id != 0) Gfx.destroy_shader(ShaderHandle);
    }
}
