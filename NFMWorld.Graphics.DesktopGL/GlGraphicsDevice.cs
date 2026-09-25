// LLM maintained.
//
// The IGraphicsDevice implementation: a core-profile desktop GL 3.3 context plus resource creation.
//
// A standalone copy of the ANGLE backend rather than a shared base with it, per the choice made when
// this backend was added. The two differ in the things that actually decide behaviour - the binding
// (Silk.NET.OpenGL vs Silk.NET.OpenGLES), the shader form the bundles are compiled from (Glsl330 vs
// GlslEs), whether a base-vertex draw needs an extension (it does not here), and who owns the
// context (the host, always) - and those differences run through every file. Sharing a base would
// mean parameterising each of them, which for a measurement harness is worse than the duplication:
// the point of this backend is that it is a plain desktop GL implementation with nothing ANGLE-shaped
// left in it.
//
// The device owns very little. GL has no device object, no allocator and no deferred context - every
// resource is an integer name in the current context - so unlike the sokol backend there is no
// handle table or descriptor validation layer here. What the device actually provides is the context
// lifetime, the reflection-to-program bridge (GlShaderProgram), and a single command buffer, because
// the abstraction's one-live-buffer rule exists precisely so an immediate-mode backend can implement
// it directly.
using NFMWorld.Shaders;
using Silk.NET.OpenGL;

namespace NFMWorld.Graphics.DesktopGL;

/// <summary>
/// A desktop OpenGL 3.3 core rendering device.
///
/// One way in: <see cref="Create"/> attaches to a context the host has already made current - the
/// real integration path, where the host (SDL3, in this app) owns the window and context and hands
/// over its <c>SDL_GL_GetProcAddress</c>. There is no headless path and no EGL, unlike the ANGLE
/// backend: desktop GL has no ownerless surface this needs, and the smoke test goes through the same
/// host-owned-context path with an off-screen framebuffer.
///
/// Nothing here chooses a GL implementation, which is the substantive difference from the ANGLE
/// backend: that one loads a specific ANGLE build so the implementation under test is the same on
/// every machine, whereas this one deliberately takes whatever the driver provides - that is the
/// whole question it exists to answer.
/// </summary>
public sealed class GlGraphicsDevice : IGraphicsDevice, IDisposable
{
    private readonly GL _gl;

    /// <summary>
    /// Where resources hand their GL names when they are disposed or finalized, and why.
    ///
    /// A disposer cannot delete its object itself: the finalizer thread can resolve no GL entry point
    /// past 1.1, so <c>glDeleteBuffers</c> there throws - on the finalizer thread, which no catch can
    /// intercept. See <see cref="GlDeletionQueue"/> for the measurement behind that.
    ///
    /// This backend can queue the deletions at all only because of a property of the game rather than
    /// of the abstraction: every GL resource is constructed by this device and GL is only ever touched
    /// from the thread that owns the context (there is no second render thread and no shared-context
    /// worker), so <see cref="Submit"/> on the main thread is guaranteed to drain anything queued.
    /// That is also why the queue is drained rather than made thread-safe against a background
    /// deleter - there is no background deleter to be safe against.
    /// </summary>
    private readonly GlDeletionQueue _deletions = new();

    private GlCommandBuffer? _activeCommandBuffer;
    private bool _disposed;

    public ISwapchain Swapchain { get; }

    /// <inheritdoc />
    public bool HasBottomLeftFramebufferOrigin => true;

    private GlGraphicsDevice(GL gl, int width, int height, Action? present = null, int multiSampleCount = 0)
    {
        _gl = gl;
        Swapchain = new GlSwapchain(width, height, present, multiSampleCount);
    }

    /// <summary>
    /// Whether this context can honour a non-zero base vertex in an indexed draw.
    ///
    /// Always true here, and kept as a property so call sites written against the ANGLE backend -
    /// where the answer genuinely depends on the extensions the driver exposes - compile unchanged.
    /// The base-vertex draw is core desktop GL from 3.2, so a context that satisfied this backend's
    /// 3.3 floor has it by construction.
    /// </summary>
    internal bool SupportsBaseVertex => true;

    /// <summary>
    /// Attaches to a GL context the host has already created and made current.
    ///
    /// <paramref name="getProcAddress"/> is the host's loader - <c>SDL_GL_GetProcAddress</c> for an
    /// SDL3 window. Building a window and a context is the host's job, and doing it here would
    /// duplicate a lifetime the host already manages.
    ///
    /// There is no headless overload and no EGL here, unlike the ANGLE backend: desktop GL has no
    /// pbuffer path this backend needs, and a core 3.3 context is what the host's SDL window
    /// provides. Removing it removes the whole Egl.cs dependency, which is the point of the copy -
    /// nothing in this backend chooses a GL implementation.
    ///
    /// Note this path does not and cannot reallocate the drawable on resize - the window and its
    /// context belong to the host - so <see cref="ISwapchain.Resize"/> only tracks the new size.
    /// </summary>
    /// <param name="present">
    /// How the host shows a finished frame. On the SDL path the drawable and the window both belong
    /// to the host, so presentation is the host's too (<c>SDL_GL_SwapWindow</c>) and there is no
    /// handle here to do it with. Null means nothing presents, which is correct only for a host that
    /// reads its own framebuffer.
    /// </param>
    /// <param name="multiSampleCount">
    /// The sample count the host's window was actually created with, read back from SDL by the
    /// caller. Defaulted to 0 so a host that does not request multisampling - and every smoke test -
    /// reads unchanged. This backend cannot query it itself: the count belongs to the host's pixel
    /// format, and there is no GL call that reports the default framebuffer's samples that would not
    /// also have to run on a context this backend does not own.
    /// </param>
    public static GlGraphicsDevice Create(
        Func<string, nint> getProcAddress, int backBufferWidth, int backBufferHeight, Action? present = null,
        int multiSampleCount = 0)
    {
        ArgumentNullException.ThrowIfNull(getProcAddress);
        return new GlGraphicsDevice(
            GL.GetApi(getProcAddress), backBufferWidth, backBufferHeight, present, multiSampleCount);
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
    /// carry into the next frame, to present, and to run <see cref="_deletions"/>.
    ///
    /// That last part is the reason this method cannot be a pure no-op. It is the only point at which
    /// the backend is certain to be on the context's thread with the frame's rendering behind it, so
    /// it is where GL names queued by disposers and finalizers are finally deleted.
    ///
    /// A resource disposed <em>after</em> this frame's drain is deleted on the next frame's, which is
    /// correct but not prompt - and deliberately not worked around here. Retrying on a timer was
    /// tried and is worse than it sounds: it does not fix anything that draining at frame end does
    /// not, it only shortens the window, and it would make the window something to reason about.
    /// Meanwhile the one unbounded case has a different answer entirely - a program or pipeline
    /// allocated per frame should be disposed per frame by its owner, not left to the finalizer.
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

        // The frame's last GL work, so anything queued while this frame was being built goes now
        // rather than at the next frame. Nothing below this point in the frame loop touches the
        // context, which is what makes "after the reset" the right place for it.
        _deletions.Drain(_gl);
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

        return new GlBuffer(_gl, _deletions, desc, initialData);
    }

    public ITexture CreateTexture(TextureDesc desc, ReadOnlySpan<byte> initialData = default)
    {
        if (desc.Width <= 0 || desc.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(desc), $"A {desc.Width}x{desc.Height} texture cannot be allocated.");

        // ToInternalFormat throws for the compressed formats the core profile lacks, before any GL call.
        return new GlTexture(_gl, _deletions, desc, initialData);
    }

    public IRenderTarget CreateRenderTarget(RenderTargetDesc desc)
    {
        if (desc.Width <= 0 || desc.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(desc), $"A {desc.Width}x{desc.Height} render target cannot be allocated.");

        return new GlRenderTarget(_gl, _deletions, desc);
    }

    public ISampler CreateSampler(SamplerDesc desc)
    {
        // A sampler's min filter has to know whether the texture it will read has mipmaps, and the
        // abstraction's SamplerDesc does not carry that - the two are paired at bind time, not at
        // creation. GLES resolves it at draw time from the bound texture's completeness, so the
        // mipmapped variants are safe to request unconditionally: a texture with only level 0
        // makes a mipmapping min filter read as incomplete, which is why GlTexture allocates its
        // whole chain when MipMapped is set.
        return new GlSampler(_gl, _deletions, desc, mipMapped: true);
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

        var program = new GlShaderProgram(_gl, _deletions, vertex.Vertex, vertex.Pixel, vertex.Reflection);

        try
        {
            return new GlPipelineState(_gl, _deletions, program, desc);
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
    /// Wraps a generated bundle's per-stage desktop GLSL for use as a pipeline shader.
    ///
    /// The ES source is the <em>only</em> form this backend compiles - no SPIR-V, no HLSL - because
    /// The driver compiles the GLSL it is given for whatever hardware it targets. What the bundle
    /// carries alongside (HLSL, MSL, desktop GLSL) is what the other backends consume.
    ///
    /// The reflection travels with the module rather than being looked up later: glGetUniformLocation
    /// is the only way to find a sampler, and the reflected name is what it has to be asked for. That
    /// only works because the shader compiler names the ES combined sampler after the texture - see
    /// SpirvCrossReflector.NameCombinedSamplers and GlShaderProgram.TextureLocations.
    /// </summary>
    /// <inheritdoc cref="IGraphicsDevice.CreateShaderModule"/>
    IShaderModule IGraphicsDevice.CreateShaderModule(ShaderStageSources vertex, ShaderStageSources pixel, ShaderReflection reflection) =>
        LoadProgram(vertex, pixel, reflection);

    /// <inheritdoc cref="LoadProgram(ShaderStageSources,ShaderStageSources,ShaderReflection)"/>
    public static IShaderModule LoadProgram(ShaderStageSources vertex, ShaderStageSources pixel, ShaderReflection reflection)
    {
        ArgumentNullException.ThrowIfNull(vertex);
        ArgumentNullException.ThrowIfNull(pixel);
        ArgumentNullException.ThrowIfNull(reflection);

        if (string.IsNullOrWhiteSpace(vertex.Glsl330) || string.IsNullOrWhiteSpace(pixel.Glsl330))
        {
            throw new ArgumentException(
                "The bundle carries no desktop GLSL for one or both stages. This backend compiles " +
                "ShaderStageSources.Glsl330; a bundle generated before that field existed cannot be used.",
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

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _activeCommandBuffer = null;

        // Nothing to tear down beyond this device's own state: the context, the window and the
        // drawable all belong to the host, which is why there is no eglTerminate equivalent here.
        // Every GL object this device created is released by its own Dispose, not by the context
        // going away.
    }
}

/// <summary>
/// A shader module holding a program's two desktop-GL stages plus its reflection.
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
