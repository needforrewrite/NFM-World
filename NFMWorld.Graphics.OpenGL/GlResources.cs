// LLM maintained.
//
// The resource types behind the GLES/ANGLE backend: buffers, textures, samplers, shader programs
// and pipelines. These are thin wrappers over GL object names - the interesting work is in what
// the device does at creation time and in GlShaderProgram's binding tables, which is where this
// backend's one real piece of indirection lives.
using System.Runtime.InteropServices;
using NFMWorld.Shaders;
using Silk.NET.OpenGLES;

namespace NFMWorld.Graphics.OpenGL;

/// <summary>
/// A GL buffer object. Both vertex and index buffers are the same thing in GL - only the target
/// they are bound to differs - so the kind is recorded rather than encoded.
/// </summary>
internal sealed class GlBuffer : IBuffer
{
    private readonly GL _gl;
    internal uint Handle { get; }

    public BufferKind Kind { get; }
    public BufferUsage Usage { get; }
    public int SizeInBytes { get; }

    /// <summary>
    /// The index format this buffer was created with. GL bakes the index width into the draw call
    /// rather than into the buffer, but the abstraction's <see cref="BufferDesc"/> carries it and
    /// <see cref="GlCommandBuffer.SetIndexBuffer"/> has to remember it, because
    /// <c>ICommandBuffer.SetIndexBuffer</c> passes only the buffer.
    /// </summary>
    internal IndexFormat IndexFormat { get; }

    internal GlBuffer(GL gl, BufferDesc desc, ReadOnlySpan<byte> initialData)
    {
        _gl = gl;
        Kind = desc.Kind;
        Usage = desc.Usage;
        SizeInBytes = desc.SizeInBytes;
        IndexFormat = desc.IndexFormat;

        Handle = _gl.GenBuffer();
        var target = desc.Kind.ToTarget();
        _gl.BindBuffer(target, Handle);
        _gl.BufferData(target, (nuint)desc.SizeInBytes, initialData, desc.Usage.ToUsage());
    }

    /// <summary>Uploads into an existing buffer, without reallocating it - this is what makes
    /// <see cref="ICommandBuffer.UpdateBuffer"/> a sub-range write rather than a replacement.</summary>
    internal void Update(GL gl, ReadOnlySpan<byte> data, int offsetBytes)
    {
        if (Usage != BufferUsage.Dynamic)
        {
            throw new NotSupportedException(
                $"This buffer was created with {nameof(BufferUsage)}.{Usage}; only " +
                $"{nameof(BufferUsage)}.{nameof(BufferUsage.Dynamic)} buffers can be updated after creation.");
        }

        gl.BindBuffer(Kind.ToTarget(), Handle);
        gl.BufferSubData(Kind.ToTarget(), offsetBytes, data);
    }

    public void Dispose() => _gl.DeleteBuffer(Handle);
}

/// <summary>
/// A GL texture. Held together with the sampler that reads it: ES 3.0 has sampler objects, so the
/// two are separate - see <see cref="GlSampler"/>.
/// </summary>
internal sealed class GlTexture : ITexture
{
    private readonly GL _gl;
    internal uint Handle { get; }

    public int Width { get; }
    public int Height { get; }
    public TextureFormat Format { get; }

    /// <summary>Whether the storage was allocated with a full mip chain.</summary>
    internal bool MipMapped { get; }

    /// <summary>
    /// Whether this texture is a render target's attachment.
    ///
    /// Recorded because it changes what the texture can be used for: a render target's colour
    /// attachment must not be sampled while that target is bound for drawing, and a depth-stencil
    /// one cannot be read back at all. Nothing here enforces either - the abstraction's contract is
    /// the caller's to keep - but <c>ReadTexture</c> consults it to reject the depth case by name
    /// rather than by format alone.
    /// </summary>
    public bool RenderTargetable { get; }

    internal GlTexture(GL gl, TextureDesc desc, ReadOnlySpan<byte> initialData)
    {
        _gl = gl;
        Width = desc.Width;
        Height = desc.Height;
        Format = desc.Format;
        MipMapped = desc.MipMapped;
        RenderTargetable = desc.RenderTargetable;

        Handle = _gl.GenTexture();
        Allocate(desc, initialData);
    }

    /// <summary>
    /// Allocates storage and uploads level 0.
    ///
    /// Immutable storage (<c>glTexStorage2D</c>) would be the better modern choice, but it cannot
    /// be combined with the initial-data path the abstraction's <see cref="TextureDesc"/> implies
    /// without a second call, and a texture created <c>RenderTargetable</c> starts with none of
    /// its own. <c>glTexImage2D</c> covers both, so that is what this uses - matching what ANGLE's
    /// D3D11 backend does anyway, since it has no immutable-texture concept to preserve.
    /// </summary>
    private void Allocate(TextureDesc desc, ReadOnlySpan<byte> initialData)
    {
        _gl.BindTexture(TextureTarget.Texture2D, Handle);

        // Rows are tightly packed. GL's default unpack alignment is 4, which inserts padding after
        // any row whose byte length is not a multiple of four - so an R8 texture of odd width would
        // otherwise be uploaded sheared, silently, because the driver reads past the end of the
        // row into the next one. This is the same reason ReadFromFramebuffer sets the pack
        // alignment on the way out.
        _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);

        var levels = desc.MipMapped ? MipLevels(desc.Width, desc.Height) : 1;
        // A texture with a mip chain is filled by GenerateMips once level 0 is present; allocating
        // and uploading level 0 only is what makes that possible.
        _gl.TexImage2D(TextureTarget.Texture2D, 0, desc.Format.ToInternalFormat(),
            (uint)desc.Width, (uint)desc.Height, 0,
            desc.Format.ToUploadFormat(), desc.Format.ToUploadType(), initialData);

        // The remaining levels are allocated empty so the texture is mip-complete: a sampler set to
        // a mipmapping min filter reads an incomplete texture as transparent black, which is the
        // silent-wrong-pixels failure mode this avoids.
        if (desc.MipMapped)
        {
            for (var level = 1; level < levels; level++)
            {
                var w = Math.Max(1, desc.Width >> level);
                var h = Math.Max(1, desc.Height >> level);
                _gl.TexImage2D(TextureTarget.Texture2D, level, desc.Format.ToInternalFormat(),
                    (uint)w, (uint)h, 0, desc.Format.ToUploadFormat(), desc.Format.ToUploadType(), ReadOnlySpan<byte>.Empty);
            }
        }

        _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 4);

        // No mipmapping for a single-level texture, or the sampler's min filter would select
        // levels that do not exist.
        _gl.TexParameter(TextureTarget.Texture2D, GLEnum.TextureBaseLevel, 0);
        _gl.TexParameter(TextureTarget.Texture2D, GLEnum.TextureMaxLevel, levels - 1);
    }

    private static int MipLevels(int width, int height)
    {
        var levels = 1;
        while (width > 1 || height > 1)
        {
            width = Math.Max(1, width >> 1);
            height = Math.Max(1, height >> 1);
            levels++;
        }
        return levels;
    }

    /// <summary>Uploads into a sub-rectangle of level 0.</summary>
    internal void Update(GL gl, int x, int y, int width, int height, ReadOnlySpan<byte> data)
    {
        if (MipMapped)
        {
            throw new NotSupportedException(
                "Updating a mipmapped texture would leave every level past 0 stale, and regenerating " +
                "the chain on every update is a cost this POC does not take on. Create it single-level.");
        }

        gl.BindTexture(TextureTarget.Texture2D, Handle);
        gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
        gl.TexSubImage2D(TextureTarget.Texture2D, 0, x, y, (uint)width, (uint)height,
            Format.ToUploadFormat(), Format.ToUploadType(), data);
        gl.PixelStore(PixelStoreParameter.UnpackAlignment, 4);
    }

    internal void GenerateMips(GL gl)
    {
        gl.BindTexture(TextureTarget.Texture2D, Handle);
        gl.GenerateMipmap(TextureTarget.Texture2D);
    }

    public void Dispose() => _gl.DeleteTexture(Handle);
}

/// <summary>
/// A GL sampler object.
///
/// ES 3.0 keeps sampler state in a separate object bound to a texture unit, rather than the
/// desktop-GL/D3D9 model of baking it into the texture. That is what lets one texture be read with
/// two different filter modes, and it is why this backend pairs a texture and a sampler at bind
/// time (see <c>GlCommandBuffer.SetShaderResource</c>) instead of at creation time.
/// </summary>
internal sealed class GlSampler : ISampler
{
    private readonly GL _gl;
    internal uint Handle { get; }

    public SamplerDesc Desc { get; }

    internal GlSampler(GL gl, SamplerDesc desc, bool mipMapped)
    {
        _gl = gl;
        Desc = desc;

        Handle = _gl.GenSampler();
        _gl.SamplerParameter(Handle, GLEnum.TextureMinFilter, (int)desc.Filter.ToMinFilter(mipMapped));
        _gl.SamplerParameter(Handle, GLEnum.TextureMagFilter, (int)desc.Filter.ToMagFilter());
        _gl.SamplerParameter(Handle, GLEnum.TextureWrapS, (int)desc.AddressU.ToWrap());
        _gl.SamplerParameter(Handle, GLEnum.TextureWrapT, (int)desc.AddressV.ToWrap());
    }

    public void Dispose() => _gl.DeleteSampler(Handle);
}

/// <summary>
/// A render target: an FBO with a colour attachment and, optionally, a depth-stencil one.
///
/// The attachments are owned as ordinary <see cref="GlTexture"/>s rather than renderbuffers, so
/// they can be sampled afterwards - which is the whole point of an off-screen target for the
/// shadow-cascade passes the abstraction is built around.
/// </summary>
internal sealed class GlRenderTarget : IRenderTarget
{
    private readonly GL _gl;
    internal uint Handle { get; }

    public GlTexture ColorTexture { get; }
    public GlTexture? DepthStencilTexture { get; }

    ITexture IRenderTarget.ColorTexture => ColorTexture;
    ITexture? IRenderTarget.DepthStencilTexture => DepthStencilTexture;

    internal GlRenderTarget(GL gl, RenderTargetDesc desc)
    {
        _gl = gl;
        ColorTexture = new GlTexture(gl, new TextureDesc(desc.Width, desc.Height, desc.ColorFormat, RenderTargetable: true), default);
        if (desc.HasDepthStencil)
            DepthStencilTexture = new GlTexture(gl, new TextureDesc(desc.Width, desc.Height, desc.DepthStencilFormat, RenderTargetable: true), default);

        Handle = _gl.GenFramebuffer();
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, Handle);

        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
            TextureTarget.Texture2D, ColorTexture.Handle, 0);
        if (DepthStencilTexture is not null)
            _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment,
                TextureTarget.Texture2D, DepthStencilTexture.Handle, 0);

        // Checked here rather than at first draw: an FBO that is incomplete fails every draw
        // against it with GL_INVALID_FRAMEBUFFER_OPERATION, which is a state error far from its
        // cause. Naming the attachment formats at creation is what makes the real problem legible.
        var status = _gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != GLEnum.FramebufferComplete)
        {
            throw new InvalidOperationException(
                $"Render target {desc.Width}x{desc.Height} ({desc.ColorFormat}" +
                $"{(desc.HasDepthStencil ? $" + {desc.DepthStencilFormat}" : "")}) is incomplete: {status}.");
        }
    }

    public void Dispose()
    {
        _gl.DeleteFramebuffer(Handle);
        ColorTexture.Dispose();
        DepthStencilTexture?.Dispose();
    }
}
