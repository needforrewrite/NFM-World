// LLM maintained.
//
// The IGraphicsDevice implementation: an ANGLE ES 3.0 context plus resource creation.
//
// The device owns very little. GL has no device object, no allocator and no deferred context - every
// resource is an integer name in the current context - so unlike the sokol backend there is no
// handle table or descriptor validation layer here. What the device actually provides is the context
// lifetime, the reflection-to-program bridge (GlShaderProgram), and a single command buffer, because
// the abstraction's one-live-buffer rule exists precisely so an immediate-mode backend can implement
// it directly.
using NFMWorld.Shaders;
using Silk.NET.OpenGLES;

namespace NFMWorld.Graphics.OpenGL;

/// <summary>
/// A GLES 3.0 rendering device backed by ANGLE.
///
/// Two ways in, and the difference is only who owns the GL context:
///
/// - <see cref="CreateHeadless"/> brings up its own EGL display, context and pbuffer. This is what
///   the smoke test uses, and it is the only path that can resize its drawable.
/// - <see cref="Create"/> attaches to a context the host has already made current - the real
///   integration path, where the host (SDL3, in this app) owns the window and hands over its
///   <c>SDL_GL_GetProcAddress</c>. The swapchain then only reports the drawable's size.
///
/// The ANGLE being loaded matters more than it looks. Silk.NET.OpenGLES.ANGLE.Native - Silk.NET's
/// own native package - ships 32-bit binaries in its win-x64 folder and cannot be used at all. This
/// one takes them from Maxine.Silk.OpenGLES.ANGLE.Native, which builds ANGLE per platform, so the
/// ANGLE under test is the same one on every machine rather than whatever the host happens to have
/// installed. See Egl.cs for the import and the two things it replaces.
/// </summary>
public sealed class GlGraphicsDevice : IGraphicsDevice, IDisposable
{
    private readonly GL _gl;

    /// <summary>EGL state when this device owns it, or null when the host's context was attached.</summary>
    private readonly Egl.Context? _context;

    private GlCommandBuffer? _activeCommandBuffer;
    private bool _disposed;

    public ISwapchain Swapchain { get; }

    private GlGraphicsDevice(GL gl, Egl.Context? context, int width, int height)
    {
        _gl = gl;
        _context = context;
        Swapchain = new GlSwapchain(context, width, height);
    }

    /// <summary>
    /// Brings up a headless ES 3.0 context on ANGLE and renders into an off-screen pbuffer.
    ///
    /// There is no ANGLE-installation parameter any more: the bindings' native package places the
    /// right ANGLE next to the executable for the current RID, so the loader finds it without being
    /// told where to look.
    /// </summary>
    public static GlGraphicsDevice CreateHeadless(int width, int height)
    {
        var context = Egl.Context.CreateHeadless(width, height);
        try
        {
            return new GlGraphicsDevice(GL.GetApi(context.GlContext), context, width, height);
        }
        catch
        {
            context.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Attaches to a GL context the host has already created and made current.
    ///
    /// <paramref name="getProcAddress"/> is the host's loader - <c>SDL_GL_GetProcAddress</c> for an
    /// SDL3 window. The POC does not call it: building a window and an EGL surface is the host's
    /// job, and doing it here would duplicate a lifetime the host already manages.
    ///
    /// Note this path does not and cannot reallocate the drawable on resize - the EGL surface
    /// belongs to the host - so <see cref="ISwapchain.Resize"/> only tracks the new size.
    /// </summary>
    public static GlGraphicsDevice Create(Func<string, nint> getProcAddress, int backBufferWidth, int backBufferHeight)
    {
        ArgumentNullException.ThrowIfNull(getProcAddress);
        return new GlGraphicsDevice(GL.GetApi(getProcAddress), null, backBufferWidth, backBufferHeight);
    }

    public ICommandBuffer AcquireCommandBuffer()
    {
        if (_activeCommandBuffer is not null)
        {
            throw new InvalidOperationException(
                $"{nameof(AcquireCommandBuffer)} was called again before the previous command buffer " +
                "was submitted - only one command buffer may be live at a time.");
        }

        var commandBuffer = new GlCommandBuffer(_gl, this);
        _activeCommandBuffer = commandBuffer;
        return commandBuffer;
    }

    /// <summary>
    /// Ends the frame.
    ///
    /// There is nothing to replay: every command buffer method issued its GL call as it was called,
    /// which is what the one-live-buffer rule makes safe. What is left is to invalidate the command
    /// buffer's per-frame state (the bound pipeline and vertex streams), which it would otherwise
    /// carry into the next frame, and to present.
    /// </summary>
    public void Submit(ICommandBuffer commandBuffer)
    {
        if (commandBuffer is not GlCommandBuffer glCommandBuffer)
        {
            throw new ArgumentException(
                $"{nameof(Submit)} needs a command buffer this backend created.", nameof(commandBuffer));
        }

        if (!ReferenceEquals(glCommandBuffer, _activeCommandBuffer))
        {
            throw new InvalidOperationException(
                "This command buffer is not the live one - it was either already submitted or never acquired.");
        }

        glCommandBuffer.Reset();
        _activeCommandBuffer = null;
    }

    public IBuffer CreateBuffer(BufferDesc desc, ReadOnlySpan<byte> initialData = default)
    {
        if (desc.SizeInBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(desc), $"A buffer of {desc.SizeInBytes} bytes cannot be allocated.");

        if (initialData.Length > desc.SizeInBytes)
        {
            throw new ArgumentException(
                $"Initial data holds {initialData.Length} bytes but the buffer is {desc.SizeInBytes} bytes.",
                nameof(initialData));
        }

        return new GlBuffer(_gl, desc, initialData);
    }

    public ITexture CreateTexture(TextureDesc desc, ReadOnlySpan<byte> initialData = default)
    {
        if (desc.Width <= 0 || desc.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(desc), $"A {desc.Width}x{desc.Height} texture cannot be allocated.");

        // ToInternalFormat throws for the compressed formats ES 3.0 core lacks, before any GL call.
        return new GlTexture(_gl, desc, initialData);
    }

    public IRenderTarget CreateRenderTarget(RenderTargetDesc desc)
    {
        if (desc.Width <= 0 || desc.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(desc), $"A {desc.Width}x{desc.Height} render target cannot be allocated.");

        return new GlRenderTarget(_gl, desc);
    }

    public ISampler CreateSampler(SamplerDesc desc)
    {
        // A sampler's min filter has to know whether the texture it will read has mipmaps, and the
        // abstraction's SamplerDesc does not carry that - the two are paired at bind time, not at
        // creation. GLES resolves it at draw time from the bound texture's completeness, so the
        // mipmapped variants are safe to request unconditionally: a texture with only level 0
        // makes a mipmapping min filter read as incomplete, which is why GlTexture allocates its
        // whole chain when MipMapped is set.
        return new GlSampler(_gl, desc, mipMapped: true);
    }

    public IPipelineState CreatePipeline(PipelineDesc desc)
    {
        ArgumentNullException.ThrowIfNull(desc.VertexShader);
        ArgumentNullException.ThrowIfNull(desc.PixelShader);

        var vertex = RequireModule(desc.VertexShader, "VertexShader");
        var pixel = RequireModule(desc.PixelShader, "PixelShader");

        // Unlike sokol, whose <c>sg_shader</c> holds both stages, GL links two separately compiled
        // shader objects - so both halves have to describe the same program. In practice callers
        // pass the same module for both (see LoadProgram), because the reflection describes the
        // linked program rather than a single stage; two different modules would mean the pipeline
        // was built against one program's bindings while the command buffer read the other's.
        if (!ReferenceEquals(vertex, pixel))
        {
            throw new ArgumentException(
                "Both shader stages must come from the same LoadProgram module. The reflection describes " +
                "the linked program rather than a single stage, so a vertex module and a pixel module " +
                "built separately cannot be paired here.", nameof(desc));
        }

        var program = new GlShaderProgram(_gl, vertex.Vertex, vertex.Pixel, vertex.Reflection);

        try
        {
            return new GlPipelineState(_gl, program, desc);
        }
        catch
        {
            // The pipeline is the program's owner (see GlPipelineState.Dispose), so a failure before
            // it exists has to clean the program up here or the linked program leaks.
            program.Dispose();
            throw;
        }
    }

    private static GlShaderModule RequireModule(IShaderModule module, string name)
    {
        if (module is not GlShaderModule glModule)
        {
            throw new ArgumentException(
                $"{name} must be a shader module this backend created - see {nameof(LoadProgram)}.", nameof(module));
        }

        return glModule;
    }

    /// <summary>
    /// Wraps a generated bundle's per-stage ES 3.0 GLSL for use as a pipeline shader.
    ///
    /// The ES source is the <em>only</em> form this backend compiles - no SPIR-V, no HLSL - because
    /// ANGLE translates the GLSL it is given to HLSL for its D3D11 backend itself. What the bundle
    /// carries alongside (HLSL, MSL, desktop GLSL) is what the other backends consume.
    ///
    /// The reflection travels with the module rather than being looked up later: glGetUniformLocation
    /// is the only way to find a sampler, and the reflected name is what it has to be asked for. That
    /// only works because the shader compiler names the ES combined sampler after the texture - see
    /// SpirvCrossReflector.NameCombinedSamplers and GlShaderProgram.TextureLocations.
    /// </summary>
    public static IShaderModule LoadProgram(ShaderStageSources vertex, ShaderStageSources pixel, ShaderReflection reflection)
    {
        ArgumentNullException.ThrowIfNull(vertex);
        ArgumentNullException.ThrowIfNull(pixel);
        ArgumentNullException.ThrowIfNull(reflection);

        if (string.IsNullOrWhiteSpace(vertex.GlslEs) || string.IsNullOrWhiteSpace(pixel.GlslEs))
        {
            throw new ArgumentException(
                "The bundle carries no ES 3.0 GLSL for one or both stages. This backend compiles " +
                "ShaderStageSources.GlslEs; a bundle generated before that field existed cannot be used.",
                nameof(vertex));
        }

        return new GlShaderModule(vertex, pixel, reflection);
    }

    /// <summary>
    /// Reads pixels back off the GPU.
    ///
    /// GL has a real readback path, so this does not keep a CPU mirror of anything the way the sokol
    /// backend has to - <c>glReadPixels</c> returns whatever is actually in the texture, whether the
    /// CPU uploaded it or a draw produced it. What it reads is a <em>framebuffer</em> rather than a
    /// texture, so the texture is attached to a scratch FBO for the duration of the call; that is
    /// also why the abstraction requires the caller to have bound something else as a render target,
    /// since a texture attached to the bound target would be both source and destination.
    ///
    /// Only one conversion is undone here: <c>glReadPixels</c> pads each row to
    /// <c>GL_PACK_ALIGNMENT</c>, while the contract is tightly packed rows.
    ///
    /// Rows are <em>not</em> flipped. GL's texture origin is the lower left and
    /// <c>glTexImage2D</c>/<c>glTexSubImage2D</c> take rows in that same bottom-up order, so a
    /// texture uploaded through this backend and then read back through it round-trips exactly as
    /// the caller wrote it - which is what the abstraction's callers depend on, since they hand
    /// <c>UpdateTexture</c> a top-down span and expect <c>ReadTexture</c> to return the same bytes.
    /// Flipping one side and not the other is what turns an orientation convention into a sheared
    /// readback.
    /// </summary>
    public void ReadTexture(ITexture texture, int x, int y, int width, int height, Span<byte> destination, int level = 0)
    {
        ArgumentNullException.ThrowIfNull(texture);

        if (texture is not GlTexture glTexture)
            throw new ArgumentException($"{nameof(ReadTexture)} needs a texture this backend created.", nameof(texture));

        if (glTexture.Format.IsDepthFormat())
        {
            throw new NotSupportedException(
                $"{glTexture.Format} has no colour storage to read back. Read the render target's " +
                $"{nameof(IRenderTarget.ColorTexture)} instead - that is what the abstraction documents.");
        }

        if (level < 0 || level >= (glTexture.MipMapped ? MipLevels(glTexture.Width, glTexture.Height) : 1))
            throw new ArgumentOutOfRangeException(nameof(level), level, "Level is outside the texture's mip chain.");

        if (x < 0 || y < 0 || width <= 0 || height <= 0 || x + width > glTexture.Width || y + height > glTexture.Height)
        {
            throw new ArgumentOutOfRangeException(nameof(x),
                $"The {width}x{height} region at ({x}, {y}) does not fit the {glTexture.Width}x{glTexture.Height} texture.");
        }

        var bytesPerPixel = glTexture.Format.BytesPerPixel();
        var required = width * height * bytesPerPixel;
        if (destination.Length < required)
        {
            throw new ArgumentException(
                $"Destination holds {destination.Length} bytes; {required} are required for {width}x{height} " +
                $"of {glTexture.Format}.", nameof(destination));
        }

        ReadFromFramebuffer(glTexture, x, y, width, height, bytesPerPixel, level, destination[..required]);
    }

    /// <summary>
    /// Reads through a scratch framebuffer.
    ///
    /// The rows are copied straight through, without the flip the two coordinate systems might
    /// suggest. <c>glReadPixels</c> fills its buffer from the bottom row of the rectangle up, and
    /// texture storage is indexed that same way, so the bytes come out in the order the texture
    /// holds them - which is the order a caller's <c>UpdateTexture</c> span was written in. See
    /// <see cref="ReadTexture"/> for why that round trip is the property worth preserving. The
    /// scratch buffer exists only because the pack alignment has to be forced to 1, and that is
    /// global state.
    /// </summary>
    private void ReadFromFramebuffer(GlTexture texture, int x, int y, int width, int height,
        int bytesPerPixel, int level, Span<byte> destination)
    {
        var rowBytes = width * bytesPerPixel;

        // A throwaway FBO, so the caller's binding survives. GL's framebuffer binding is global
        // state, and this is documented as callable from editor/export paths that are mid-frame.
        // GL_READ_FRAMEBUFFER_BINDING, not GL_FRAMEBUFFER_BINDING: the read binding is what
        // glReadPixels consults.
        var previousFramebuffer = (uint)_gl.GetInteger(GetPName.ReadFramebufferBinding);
        var previousReadBuffer = (uint)_gl.GetInteger(GetPName.ReadBuffer);
        var framebuffer = _gl.GenFramebuffer();

        try
        {
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer);
            _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                TextureTarget.Texture2D, texture.Handle, level);

            // Reading attachment 0 requires it to be the read buffer; an FBO's default read buffer
            // is COLOR_ATTACHMENT0, but it is set explicitly so a texture attached at a different
            // point could be handled by changing this one line.
            _gl.ReadBuffer(ReadBufferMode.ColorAttachment0);

            var status = _gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != GLEnum.FramebufferComplete)
            {
                throw new InvalidOperationException(
                    $"Cannot read {texture.Format} back: the scratch framebuffer is {status}. A " +
                    "texture whose level 0 is read this way must be colour-renderable.");
            }

            // The rows are packed to their exact width. GL's default pack alignment is 4, which
            // inserts padding after a row whose byte length is not a multiple of four - and this
            // backend's contract is tightly packed rows, so an R8 texture of odd width would
            // otherwise come back sheared.
            _gl.PixelStore(PixelStoreParameter.PackAlignment, 1);

            _gl.ReadPixels(x, y, (uint)width, (uint)height, texture.Format.ToUploadFormat(),
                texture.Format.ToUploadType(), destination);

            _gl.PixelStore(PixelStoreParameter.PackAlignment, 4);
        }
        finally
        {
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, previousFramebuffer);
            if (previousReadBuffer != 0)
                _gl.ReadBuffer((ReadBufferMode)previousReadBuffer);
            _gl.DeleteFramebuffer(framebuffer);
        }

        // Reading only packs the commands; without a flush the bytes could be stale, and the
        // abstraction documents this as synchronous by design.
        _gl.Finish();
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

    /// <summary>The GL context, for the smoke test to query strings the abstraction does not expose.</summary>
    internal GL Gl => _gl;

    /// <summary>Where ANGLE was loaded from, or null when the host supplied the context.</summary>
    internal string? AngleDirectory => _context?.LoadedFrom;

    /// <summary>
    /// The EGL implementation's vendor string, or null when the host supplied the context.
    ///
    /// Reported by the smoke test alongside the GL strings: "Google Inc." for ANGLE means the
    /// explicit D3D11 platform request worked, while anything else means an EGL implementation this
    /// backend did not intend to be running on.
    /// </summary>
    internal string? EglVendor => _context?.Vendor;

    /// <summary>The EGL version string parsed at initialization, or null when the host supplied the context.</summary>
    internal string? EglVersion => _context?.Version;

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _activeCommandBuffer = null;

        // The context is destroyed last: every GL object created through this device belongs to it,
        // and a Delete* call after eglTerminate has no context to act on.
        _context?.Dispose();
    }
}

/// <summary>
/// A shader module holding a program's two ES 3.0 stages plus its reflection.
///
/// One module covers both stages, rather than one per stage, because GL links two separately compiled
/// shader objects into a single program and the reflection describes the linked result. That makes
/// the module the natural unit, and it is why <see cref="IShaderModule.Stage"/> reports
/// <see cref="ShaderStage.Vertex"/> regardless - the abstraction has no "whole program" stage, and
/// sokol's backend makes the same choice for the same reason.
/// </summary>
internal sealed class GlShaderModule : IShaderModule
{
    /// <summary>
    /// The two stages. <see cref="ShaderStageSources"/> describes one stage, so a program needs both
    /// of them - the vertex source compiles into the vertex shader object and the pixel source into
    /// the fragment one, and GL links the pair.
    /// </summary>
    internal ShaderStageSources Vertex { get; }
    internal ShaderStageSources Pixel { get; }

    /// <summary>The linked program's reflection - see <see cref="GlShaderProgram"/>.</summary>
    internal ShaderReflection Reflection { get; }

    public ShaderStage Stage => ShaderStage.Vertex;

    /// <summary>
    /// Empty. The abstraction's <see cref="IShaderModule.Bytecode"/> is for backends that consume
    /// compiled bytecode; this one compiles GLSL source, and inventing a byte blob to put here would
    /// only invite a caller to depend on it.
    /// </summary>
    public ReadOnlyMemory<byte> Bytecode => ReadOnlyMemory<byte>.Empty;

    internal GlShaderModule(ShaderStageSources vertex, ShaderStageSources pixel, ShaderReflection reflection)
    {
        Vertex = vertex;
        Pixel = pixel;
        Reflection = reflection;
    }
}
