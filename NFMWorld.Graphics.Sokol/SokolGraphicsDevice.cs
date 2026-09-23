using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NFMWorld.Shaders;
using SharpSokol.Native;

namespace NFMWorld.Graphics.Sokol;

/// <summary>
/// sokol_gfx-backed <see cref="IGraphicsDevice"/>.
///
/// Unlike the FNA3D backend - which creates its own device against a window handle and owns that
/// window's swapchain - sokol_gfx does not own a window at all. Its D3D11 backend asserts on an
/// <c>ID3D11Device</c> injected through <c>sg_desc.environment</c>, and that device is produced by
/// sokol_app. So the lifetime here is inverted: <see cref="Run"/> drives sokol_app's main loop and
/// creates the device inside its init callback, handing this instance to the caller's frame
/// callback. That mirrors the intended C usage exactly (see sokol_glue.h's <c>sglue_environment</c>),
/// and it is the only arrangement the D3D11 backend accepts.
///
/// This is why the class is not constructed by the caller the way <c>FNA3DGraphicsDevice.Create</c>
/// is: there is no valid device object until sokol_app has initialized the 3D API.
/// </summary>
public sealed unsafe class SokolGraphicsDevice : IGraphicsDevice
{
    private SokolSwapchain? _swapchain;
    private SokolCommandBuffer? _activeCommandBuffer;
    private bool _shutdown;

    /// <summary>
    /// The live device, set once <c>sg_setup</c> has succeeded. sokol_gfx is a global singleton
    /// (every <c>sg_*</c> call is a free function), so at most one device can exist per process -
    /// this is the handle the static entry points below check against.
    /// </summary>
    private static SokolGraphicsDevice? _current;

    public ISwapchain Swapchain =>
        _swapchain ?? throw new InvalidOperationException("The swapchain is only available once the sokol_app frame loop has started.");

    private SokolGraphicsDevice() { }

    /// <summary>
    /// Initializes sokol_gfx and runs <paramref name="frame"/> once per frame until the window
    /// closes. <paramref name="init"/> runs after the 3D API exists, so it is the place to create
    /// resources; <paramref name="shutdown"/> runs as the loop ends.
    /// </summary>
    /// <param name="configure">
    /// Optional last chance to adjust <c>sg_desc</c> before <c>sg_setup</c> - used for the one
    /// thing a caller genuinely needs to control from outside: installing an
    /// <c>sg_logger</c>. sokol reports validation failures by <em>logging</em> rather than by
    /// failing a call (<c>_SG_VALIDATE</c> only sets an error and logs; see sokol_gfx.h:7779),
    /// and this backend logs nothing by default, so without a logger a validation failure is
    /// entirely silent. <see cref="SokolGraphicsDevice.Run"/> passes the environment first, so
    /// an implementation must leave it alone.
    /// </param>
    /// <summary>
    /// Adjusts an <c>sg_desc</c> in place - a <c>ref</c>-taking delegate rather than
    /// <c>Action&lt;sg_desc&gt;</c> because the struct is passed by value, so a mutation through a
    /// plain action would be discarded.
    /// </summary>
    public delegate void ConfigureDesc(ref sg_desc desc);

    public static void Run(
        string title,
        int width,
        int height,
        Action<SokolGraphicsDevice> init,
        Action<SokolGraphicsDevice> frame,
        Action<SokolGraphicsDevice>? shutdown = null,
        int multiSampleCount = 0,
        ConfigureDesc? configure = null)
    {
        var state = new RunState(init, frame, shutdown);

        var desc = new sapp_desc
        {
            width = width,
            height = height,
            sample_count = multiSampleCount,
            window_title = Utf8Z(title),
            // sokol_app defaults depth_format to DEPTH_STENCIL, which is what the shadow-cascade
            // and NanoVG pass paths need, so it is left alone here.
            init_userdata_cb = &RunState.InitThunk,
            frame_userdata_cb = &RunState.FrameThunk,
            cleanup_userdata_cb = &RunState.CleanupThunk,
            user_data = (void*)state.Pinned(),
        };

        state.Configure = configure;
        App.run(&desc);
    }

    /// <summary>
    /// Copies <paramref name="value"/> into unmanaged memory as a NUL-terminated UTF-8 string.
    /// Used for the window title, which has to outlive the <c>sapp_desc</c> it is passed in: the
    /// allocation is intentionally never freed, because sokol_app reads it for the window's whole
    /// lifetime and there is exactly one per process.
    /// </summary>
    private static sbyte* Utf8Z(string value)
    {
        var bytes = SokolNative.Utf8Z(value);
        var pointer = (sbyte*)NativeMemory.Alloc((nuint)bytes.Length);
        for (var i = 0; i < bytes.Length; i++) pointer[i] = (sbyte)bytes[i];
        return pointer;
    }

    /// <summary>
    /// Everything the native callbacks need, held as unmanaged state because sokol_app stores only
    /// a <c>void*</c>. The managed delegates are kept here so they are not collected while the
    /// native loop can still call them.
    /// </summary>
    private sealed class RunState(
        Action<SokolGraphicsDevice> init,
        Action<SokolGraphicsDevice> frame,
        Action<SokolGraphicsDevice>? shutdown)
    {
        private GCHandle _self;

        /// <summary>Set by <see cref="Run"/> after construction; see that method's parameter of the same name.</summary>
        public ConfigureDesc? Configure { get; set; }

        public IntPtr Pinned()
        {
            _self = GCHandle.Alloc(this);
            return GCHandle.ToIntPtr(_self);
        }

        private static RunState From(void* userData) =>
            (RunState)GCHandle.FromIntPtr((IntPtr)userData).Target!;

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static void InitThunk(void* userData)
        {
            var state = From(userData);

            // The generated D3D11 environment comes from sokol_app: sg_setup's D3D11 backend
            // requires an injected ID3D11Device and will assert without one, so this must be
            // called from inside the app's init callback rather than before the loop.
            var desc = new sg_desc { environment = Glue.environment() };
            state.Configure?.Invoke(ref desc);
            Gfx.setup(&desc);

            var device = new SokolGraphicsDevice { _swapchain = new SokolSwapchain() };
            _current = device;
            state.Device = device;

            state.Init(device);
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static void FrameThunk(void* userData)
        {
            var state = From(userData);

            // sokol_app recreates the swapchain when the window resizes, so the cached size is
            // refreshed each frame rather than only on resize events - the cheap query avoids a
            // missed resize when the OS coalesces events.
            state.Device!.SwapchainState.Refresh();

            state.Frame(state.Device);
            state.Device.FrameIndex++;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        public static void CleanupThunk(void* userData)
        {
            var state = From(userData);
            if (state.Device is { } device)
            {
                state.Shutdown?.Invoke(device);
                device.Dispose();
            }
            Gfx.shutdown();
            _current = null;
            if (state._self.IsAllocated) state._self.Free();
        }

        public SokolGraphicsDevice? Device { get; private set; }
        public Action<SokolGraphicsDevice> Init => init;
        public Action<SokolGraphicsDevice> Frame => frame;
        public Action<SokolGraphicsDevice>? Shutdown => shutdown;
    }

    /// <summary>
    /// The live device, or null when no frame loop is running. Exposed for the smoke test and for
    /// backends that need to reach the device from outside a callback.
    /// </summary>
    internal static SokolGraphicsDevice? Current => _current;

    /// <summary>The swapchain wrapper, reached through <see cref="Swapchain"/> but stored concretely here so the frame loop can refresh it.</summary>
    internal SokolSwapchain SwapchainState => _swapchain!;

    /// <summary>
    /// Counts frames since the device was created, incremented at the end of each frame callback.
    ///
    /// This is a resource-upload budget rather than a time source: sokol allows at most one
    /// <c>sg_update_buffer</c>/<c>sg_update_image</c> per resource per frame (<c>VALIDATE_UPDATEBUF_
    /// ONCE</c>), so the command buffer needs to know which frame it is in to collapse several
    /// updates the abstraction permits into the one sokol will accept. It is deliberately
    /// independent of sokol_app's own frame counter, which this backend has no access to.
    /// </summary>
    internal long FrameIndex { get; private set; }

    /// <summary>
    /// Wraps one program's compiled stages for use as pipeline shaders.
    ///
    /// This is the sokol counterpart of <c>FNA3DGraphicsDevice.LoadEffectModule</c>, and the
    /// asymmetry is deliberate: FNA3D wants a whole compiled Effect blob and nothing else, while
    /// sokol wants HLSL (or DXBC) <em>per stage</em> plus an explicit description of every binding
    /// the shader declares. The binding description is derived from
    /// <paramref name="reflection"/> at pipeline-creation time (see <see cref="ShaderBindings"/>),
    /// so a caller only has to supply the code and the reflection.
    ///
    /// <paramref name="vertexSource"/>/<paramref name="pixelSource"/> are what actually get
    /// compiled on this backend, because the native library here is a D3D11-only build that cannot
    /// consume SPIR-V - see <see cref="SokolShaderProgram.VertexSource"/>.
    /// </summary>
    public static IShaderModule LoadProgram(
        ReadOnlyMemory<byte> vertexBytecode,
        ReadOnlyMemory<byte> pixelBytecode,
        ShaderReflection reflection,
        string? vertexSource = null,
        string? pixelSource = null) =>
        new SokolShaderProgram(vertexBytecode, pixelBytecode, reflection, vertexSource, pixelSource);

    /// <summary>
    /// Wraps a generated bundle's per-stage sources for use as pipeline shaders, selecting the form
    /// the running backend actually compiles.
    ///
    /// The selection has to happen here rather than in the bundle: an <c>sg_shader_function</c>
    /// holds a single <c>source</c> field, not one per backend, so a bundle that shipped every
    /// backend's output still has to be told which one to use at shader-creation time. The choice
    /// is read from <c>sg_query_backend()</c>, which is upstream sokol-shdc's own convention - its
    /// generated code emits <c>{prog}_shader_desc(sg_backend backend)</c> and callers pass
    /// <c>sg_query_backend()</c>.
    ///
    /// D3D11 (this repository's vendored build, and by far the common case on Windows) compiles
    /// the HLSL with <c>d3d11_target</c>; Metal compiles the MSL; GLCORE compiles the desktop
    /// GLSL; GLES3 compiles the ES 3.0 GLSL; Vulkan takes the SPIR-V. The two GL branches are
    /// separate cases rather than one because the sources are different dialects, not two spellings
    /// of the same thing - see <see cref="ShaderStageSources.GlslEs"/>.
    ///
    /// Only the D3D11 path is exercised end to end so far, for reasons that are per-backend rather
    /// than about the sources:
    ///
    /// Vulkan additionally needs <c>spirv_set0_binding_n</c>/<c>spirv_set1_binding_n</c>, and GL
    /// needs <c>glsl_name</c> on every uniform block, texture view and texture-sampler pair, since
    /// sokol resolves those by <c>glGetUniformLocation</c> rather than by binding
    /// (<c>sokol_gfx.h:11697</c>, <c>:11736</c>). spirv-cross names a combined GL sampler after the
    /// SPIR-V id, so the GLSL declares <c>_857</c> where the reflection says <c>ShadowMap0</c>;
    /// filling those fields in needs the name mapping the cross-compiler does not currently
    /// surface.
    ///
    /// The same <c>glsl_name</c> gap applies to the ES source, so the GLES3 branch is wired to the
    /// right source but is not yet known to bind its uniforms. What *is* verified for ES is
    /// everything up to that point: all eight generated programs compile and link under a real
    /// ANGLE ES 3.0 context, their std140 block sizes match the emitted reflection exactly, and
    /// <c>layout(row_major)</c> is honoured so matrices are read the same way D3D reads them.
    /// Metal is only untried, not known-broken.
    /// </summary>
    public static IShaderModule LoadProgram(
        ShaderStageSources vertex,
        ShaderStageSources pixel,
        ShaderReflection reflection)
    {
        var backend = Gfx.query_backend();
        return backend switch
        {
            sg_backend.SG_BACKEND_METAL_IOS or sg_backend.SG_BACKEND_METAL_MACOS
                or sg_backend.SG_BACKEND_METAL_SIMULATOR =>
                new SokolShaderProgram(ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, reflection, vertex.Msl, pixel.Msl),

            // A GL backend compiles GLSL and has no use for the SPIR-V, so the bytecode is
            // dropped rather than passed alongside a source - SetShaderFunction prefers the
            // source and would ignore it anyway.
            //
            // GLCORE and GLES3 take different GLSL, and sokol is explicit that the GLES3 backend
            // wants `#version 300 es` (sokol_gfx.h:927-931). They cannot share a source field
            // because the dialects differ in ways a driver rejects rather than tolerates: an ES
            // shader needs explicit precision qualifiers, and it links varyings by name where
            // desktop GLSL links them by layout(location).
            sg_backend.SG_BACKEND_GLCORE =>
                new SokolShaderProgram(ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, reflection, vertex.Glsl, pixel.Glsl),

            sg_backend.SG_BACKEND_GLES3 =>
                new SokolShaderProgram(ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, reflection, vertex.GlslEs, pixel.GlslEs),

            sg_backend.SG_BACKEND_VULKAN =>
                new SokolShaderProgram(vertex.Spirv, pixel.Spirv, reflection, null, null),

            // D3D11 is the default branch rather than an explicit case so an unrecognized backend
            // fails at sg_make_shader with a clear sokol validation error rather than silently
            // binding a shader in a language that backend cannot compile. D3D11 is the only
            // backend the vendored native library is built with in any case.
            _ => new SokolShaderProgram(ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, reflection, vertex.Hlsl, pixel.Hlsl),
        };
    }

    public ICommandBuffer AcquireCommandBuffer()
    {
        if (_activeCommandBuffer is not null)
            throw new InvalidOperationException(
                $"{nameof(AcquireCommandBuffer)} was called again before the previous command buffer was submitted - only one command buffer may be live at a time.");

        var commandBuffer = new SokolCommandBuffer(this);
        _activeCommandBuffer = commandBuffer;
        return commandBuffer;
    }

    public void Submit(ICommandBuffer commandBuffer)
    {
        if (commandBuffer is not SokolCommandBuffer sokol)
            throw new ArgumentException($"{nameof(Submit)} needs a command buffer this backend created.", nameof(commandBuffer));

        // A sokol frame is exactly one commit; the abstraction's Submit is the frame boundary, so
        // it closes any open pass and commits here (the command buffer itself never presents).
        sokol.Commit();
        _activeCommandBuffer = null;
    }

    public IBuffer CreateBuffer(BufferDesc desc, ReadOnlySpan<byte> initialData = default)
    {
        var buffer = new sg_buffer_desc
        {
            // size is the allocation; data (set below for immutable buffers) is the contents.
            size = (nuint)desc.SizeInBytes,
            usage = new sg_buffer_usage
            {
                vertex_buffer = SokolNative.Bool(desc.Kind == BufferKind.Vertex),
                index_buffer = SokolNative.Bool(desc.Kind == BufferKind.Index),
                // sokol's "immutable" is closer to the abstraction's Immutable than to Dynamic:
                // it lets the backend place the buffer in device-local memory with no CPU access.
                immutable = SokolNative.Bool(desc.Usage == BufferUsage.Immutable),
                // Dynamic buffers are updated through sg_update_buffer, which requires this flag.
                dynamic_update = SokolNative.Bool(desc.Usage == BufferUsage.Dynamic),
            },
        };

        // Initial data has to travel two different ways. An immutable buffer cannot be updated
        // after creation at all (sg_update_buffer rejects it), so its contents must go into the
        // descriptor; a dynamic one is the opposite - sokol creates a descriptor-supplied buffer
        // as immutable, so it must be filled by a post-creation update instead.
        fixed (byte* pointer = initialData)
        {
            if (!initialData.IsEmpty)
            {
                if (desc.Usage == BufferUsage.Immutable)
                    buffer.data = SokolNative.Range(pointer, initialData.Length);

                var handle = Gfx.make_buffer(&buffer);
                var result = new SokolBuffer(handle, desc);
                if (desc.Usage != BufferUsage.Immutable) UploadBuffer(handle, initialData);
                return result;
            }

            return new SokolBuffer(Gfx.make_buffer(&buffer), desc);
        }
    }

    private static void UploadBuffer(sg_buffer buffer, ReadOnlySpan<byte> data)
    {
        fixed (byte* pointer = data)
        {
            var range = SokolNative.Range(pointer, data.Length);
            Gfx.update_buffer(buffer, &range);
        }
    }

    public unsafe ITexture CreateTexture(TextureDesc desc, ReadOnlySpan<byte> initialData = default)
    {
        var depth = IsDepthFormat(desc.Format);
        var mipCount = desc.MipMapped && !desc.RenderTargetable ? MipCount(desc.Width, desc.Height) : 1;

        // "Updatable" and "mipmapped" are mutually exclusive here, and the reason is D3D11's rather
        // than sokol's: sg_update_image requires usage.dynamic_update, which _sg_d3d11_image_usage
        // turns into D3D11_USAGE_DYNAMIC - and CreateTexture2D rejects USAGE_DYNAMIC whenever
        // MipLevels > 1 (checked against the driver: E_INVALIDARG). sokol also refuses descriptor
        // data on a dynamic_update image, so the two cases need genuinely different images:
        //
        //   - a CPU-writable single-level texture: dynamic_update, no descriptor data, fed entirely
        //     through sg_update_image, so UpdateTexture can run as often as the caller likes;
        //   - everything else: one immutable image carrying its whole chain in the descriptor.
        //
        // A render target's contents belong to the GPU, and a compressed format has no CPU mirror
        // to stream from, so both take the immutable path whatever their level count.
        var updatable = !desc.RenderTargetable && mipCount == 1
            && desc.Format is TextureFormat.Rgba8 or TextureFormat.Bgra8 or TextureFormat.R8 or TextureFormat.Single;

        // An immutable image must carry its data in the descriptor, and sokol wants a whole chain -
        // every level of a multi-level image, not just level 0. Only a format the CPU can average
        // can be given one, so a multi-level Single texture drops back to a single level here: a
        // chain sokol would reject is worse than the one level the abstraction actually asked for.
        if (mipCount > 1 && !SokolTexture.CanGenerateMips(desc.Format)) mipCount = 1;

        var chain = Array.Empty<byte[]>();
        if (!updatable && !initialData.IsEmpty)
            chain = SokolTexture.BuildMipChain(desc.Width, desc.Height, mipCount, desc.Format, initialData);

        var imageDesc = new sg_image_desc
        {
            type = sg_image_type.SG_IMAGETYPE_2D,
            width = desc.Width,
            height = desc.Height,
            num_slices = 1,
            // _sg_image_desc_defaults turns 0 into 1, so "let sokol pick the chain" is not a
            // thing that exists here - the chain length has to be given explicitly.
            num_mipmaps = mipCount,
            pixel_format = desc.Format.ToNative(desc.RenderTargetable),
            usage = new sg_image_usage
            {
                // Attachment capability is a usage flag in sokol, not a property of the format.
                color_attachment = SokolNative.Bool(desc.RenderTargetable && !depth),
                depth_stencil_attachment = SokolNative.Bool(desc.RenderTargetable && depth),
                // Left unset otherwise: _sg_image_usage_defaults turns "none of the three set"
                // into immutable = true, which is the one usage that accepts descriptor data.
                dynamic_update = SokolNative.Bool(updatable),
            },
        };

        // The descriptor holds each level by pointer and nothing pins the array itself, so the
        // levels are kept alive across the call one at a time.
        var handles = new GCHandle[chain.Length];
        try
        {
            for (var level = 0; level < chain.Length; level++)
            {
                handles[level] = GCHandle.Alloc(chain[level], GCHandleType.Pinned);
                var pointer = (byte*)handles[level].AddrOfPinnedObject();
                imageDesc.data.mip_levels[level] = SokolNative.Range(pointer, chain[level].Length);
            }

            var texture = new SokolTexture(Gfx.make_image(&imageDesc), desc, mipCount, updatable);
            if (texture.Handle.id == 0)
                throw new InvalidOperationException("sg_make_image failed - see the sokol log for the validation error.");

            // sokol binds views, never bare images, so a sampler-visible texture needs one from
            // the moment it exists - SetShaderResource would otherwise bind a null view. A depth
            // image is the exception: it is only ever attached, and D3D11 needs a typeless/
            // comparison SRV format to sample one, which this minimal backend does not set up.
            if (!depth) texture.View = MakeTextureView(texture.Handle, mipCount);

            if (updatable)
            {
                // Nothing may go into the descriptor, so the contents travel through the mirror and
                // a deferred upload instead. A texture created with no initial data still gets a
                // mirror, because a later sub-rectangle update needs one to preserve what it does
                // not touch.
                texture.CpuShadow = initialData.IsEmpty
                    ? new byte[desc.Width * desc.Height * texture.BytesPerPixel]
                    : initialData.ToArray();
                if (!initialData.IsEmpty) texture.MarkDirty();
                return texture;
            }

            // The immutable path: the descriptor already carried the data (or the chain just built
            // from it), and a mirror is kept only so ReadTexture has something to serve.
            if (texture.CanMirror && !initialData.IsEmpty) texture.CpuShadow = initialData.ToArray();
            return texture;
        }
        finally
        {
            foreach (var handle in handles)
                if (handle.IsAllocated) handle.Free();
        }
    }

    /// <summary>The full mip chain length for a given size, i.e. 1 + floor(log2(max(w, h))).</summary>
    private static int MipCount(int width, int height)
    {
        var size = Math.Max(width, height);
        var count = 1;
        while (size > 1) { size >>= 1; count++; }
        return count;
    }

    /// <summary>
    /// Whether a format is attached as a depth(-stencil) target rather than a colour one. Only
    /// <see cref="TextureFormat.Depth24Stencil8"/> is: <see cref="TextureFormat.Single"/> is
    /// R32F, a plain float colour format, even though sokol also has a float <em>depth</em> format.
    /// The two are not interchangeable, because the FX shaders sample their shadow maps with
    /// ordinary <c>tex2D</c> rather than a comparison sampler - which sokol only allows on a
    /// filterable colour format.
    /// </summary>
    private static bool IsDepthFormat(TextureFormat format) => format == TextureFormat.Depth24Stencil8;

    public IRenderTarget CreateRenderTarget(RenderTargetDesc desc)
    {
        var colorTexture = (SokolTexture)CreateTexture(new TextureDesc(desc.Width, desc.Height, desc.ColorFormat, RenderTargetable: true));

        SokolTexture? depthTexture = null;
        var depthView = default(sg_view);
        if (desc.HasDepthStencil)
        {
            depthTexture = (SokolTexture)CreateTexture(new TextureDesc(desc.Width, desc.Height, desc.DepthStencilFormat, RenderTargetable: true));
            var depthViewDesc = new sg_view_desc
            {
                depth_stencil_attachment = new sg_image_view_desc { image = depthTexture.Handle, mip_level = 0, slice = 0 },
            };
            depthView = Gfx.make_view(&depthViewDesc);
        }

        // The colour image needs two views: the attachment view a pass writes through, and the
        // texture view that later passes sample it through - the latter created by CreateTexture
        // above, which is what lets a rendered-to image be read as a texture.
        return new SokolRenderTarget(colorTexture, depthTexture, MakeColorAttachmentView(colorTexture.Handle), depthView);
    }

    /// <summary>
    /// The sampling view for an image. <c>count = 0</c> means "to the end of the chain" in
    /// sokol's range type, so a mipmapped texture exposes every level through one view - but the
    /// view is only valid against a pipeline that will actually sample those levels.
    /// </summary>
    private static sg_view MakeTextureView(sg_image image, int mipCount)
    {
        var desc = new sg_view_desc
        {
            texture = new sg_texture_view_desc
            {
                image = image,
                mip_levels = new sg_texture_view_range { @base = 0, count = mipCount },
                slices = new sg_texture_view_range { @base = 0, count = 1 },
            },
        };
        return Gfx.make_view(&desc);
    }

    private static sg_view MakeColorAttachmentView(sg_image image)
    {
        var desc = new sg_view_desc
        {
            color_attachment = new sg_image_view_desc { image = image, mip_level = 0, slice = 0 },
        };
        return Gfx.make_view(&desc);
    }

    public ISampler CreateSampler(SamplerDesc desc)
    {
        var samplerDesc = new sg_sampler_desc
        {
            min_filter = desc.Filter.ToNative(),
            mag_filter = desc.Filter.ToNative(),
            mipmap_filter = desc.Filter.ToNative(),
            wrap_u = desc.AddressU.ToNative(),
            wrap_v = desc.AddressV.ToNative(),
            wrap_w = sg_wrap.SG_WRAP_REPEAT,
            // max_lod's default is FLT_MAX via _sg_def_flt, so an explicit 0 would be replaced by
            // "unlimited" anyway - stating it is the honest way to get the whole chain.
            min_lod = 0f,
            max_lod = float.MaxValue,
            border_color = sg_border_color.SG_BORDERCOLOR_TRANSPARENT_BLACK,
            // sokol treats a non-NEVER compare as "this is a comparison sampler", and the shader
            // desc here declares every sampler as SG_SAMPLERTYPE_FILTERING - so ALWAYS (the nearest
            // thing sokol has to XNA's "no comparison") would fail VALIDATE_ABND_EXPECTED_SAMPLER_
            // COMPARE_NEVER at every draw that binds it. NEVER is what a filtering sampler must
            // carry; the shadow map's comparison state would need a separate sampler type.
            compare = sg_compare_func.SG_COMPAREFUNC_NEVER,
            max_anisotropy = 1,
        };
        return new SokolSampler(Gfx.make_sampler(&samplerDesc), desc);
    }

    public IPipelineState CreatePipeline(PipelineDesc desc)
    {
        var vertex = RequireProgram(desc.VertexShader, "VertexShader");
        var pixel = RequireProgram(desc.PixelShader, "PixelShader");

        if (!ReferenceEquals(vertex.Reflection, pixel.Reflection) && vertex.Reflection != pixel.Reflection)
            throw new ArgumentException(
                "Both shader stages must come from the same LoadProgram call: sokol takes one shader object holding " +
                "both stages, and the reflection is a property of the program rather than of a stage.");

        var reflection = vertex.Reflection;

        // The binding tables are needed twice: once in the shader desc (what the shader declares)
        // and once here (how the abstraction's flat slots map onto it).
        var layout = new ShaderBindings(reflection);
        var shader = CreateShader(vertex, pixel, layout, desc);

        var pipelineDesc = BuildPipelineDesc(desc, shader, layout);
        var handle = Gfx.make_pipeline(&pipelineDesc);
        if (handle.id == 0)
            throw new InvalidOperationException("sg_make_pipeline failed - see the sokol log for the validation error.");

        return new SokolPipelineState(
            handle, shader, desc, reflection,
            layout.UniformBlockSize, layout.UniformBlockSlots, layout.ViewSlotMap, layout.SamplerSlotMap);
    }

    private static SokolShaderProgram RequireProgram(IShaderModule module, string name) =>
        module as SokolShaderProgram
        ?? throw new ArgumentException($"{name} must be a shader module this backend created.", nameof(module));

    private static unsafe sg_shader CreateShader(SokolShaderProgram vertex, SokolShaderProgram pixel, ShaderBindings layout, PipelineDesc desc)
    {
        var strings = new Utf8Strings();
        try
        {
            var shaderDesc = new sg_shader_desc();

            SetShaderFunction(&shaderDesc.vertex_func, vertex.VertexSource, vertex.VertexBytecode, vertex.EntryPoint, strings, "vs_5_0", "VertexShader");
            SetShaderFunction(&shaderDesc.fragment_func, pixel.PixelSource, pixel.PixelBytecode, pixel.EntryPoint, strings, "ps_5_0", "PixelShader");

            // Attributes are matched to shader inputs *by index* (sokol_gfx.h:14444 builds the
            // D3D11 input layout from layout.attrs[i]'s format/slot/offset, but takes the semantic
            // from shd->d3d11.attrs[i]), so this table and BuildPipelineDesc's must be built in the
            // same order - which is why both iterate desc.VertexLayouts the same way.
            var attributeIndex = 0;
            for (var slot = 0; slot < desc.VertexLayouts.Count && slot < 8; slot++)
            {
                foreach (var attribute in desc.VertexLayouts[slot].Attributes)
                {
                    if (attributeIndex >= 16) break;
                    // The semantic index is the attribute's Slot (FNA3D's UsageIndex), not a digit
                    // suffix on the name - see SokolMapping.SemanticName.
                    shaderDesc.attrs[attributeIndex].base_type = sg_shader_attr_base_type.SG_SHADERATTRBASETYPE_FLOAT;
                    shaderDesc.attrs[attributeIndex].hlsl_sem_name = strings.Pin(SokolMapping.SemanticName(attribute.Semantic));
                    shaderDesc.attrs[attributeIndex].hlsl_sem_index = (byte)attribute.Slot;
                    attributeIndex++;
                }
            }

            for (var i = 0; i < layout.UniformBlockSlots.Count; i++)
            {
                shaderDesc.uniform_blocks[i].stage = layout.UniformBlockStages[i];
                shaderDesc.uniform_blocks[i].size = (uint)layout.UniformBlockSize;
                // The merged _Global cbuffer is register(b0) in both stages. The register is a
                // per-stage namespace, so VS and FS each declaring b0 is not a collision.
                shaderDesc.uniform_blocks[i].hlsl_register_b_n = 0;
            }

            for (var i = 0; i < layout.ViewCount; i++)
            {
                shaderDesc.views[i].texture.stage = layout.ViewStages[i];
                shaderDesc.views[i].texture.image_type = sg_image_type.SG_IMAGETYPE_2D;
                // Every texture these shaders sample is an ordinary filterable float/UNORM one -
                // the shadow maps included, which is why this backend keeps Single as R32F rather
                // than as a depth format (see IsDepthFormat).
                shaderDesc.views[i].texture.sample_type = sg_image_sample_type.SG_IMAGESAMPLETYPE_FLOAT;
                shaderDesc.views[i].texture.hlsl_register_t_n = layout.ViewRegisters[i];
            }

            for (var i = 0; i < layout.SamplerCount; i++)
            {
                shaderDesc.samplers[i].stage = layout.SamplerStages[i];
                shaderDesc.samplers[i].sampler_type = sg_sampler_type.SG_SAMPLERTYPE_FILTERING;
                shaderDesc.samplers[i].hlsl_register_s_n = layout.SamplerRegisters[i];
            }

            for (var i = 0; i < layout.PairCount; i++)
            {
                shaderDesc.texture_sampler_pairs[i].stage = layout.PairStages[i];
                shaderDesc.texture_sampler_pairs[i].view_slot = (byte)layout.PairViewSlots[i];
                shaderDesc.texture_sampler_pairs[i].sampler_slot = (byte)layout.PairSamplerSlots[i];
            }

            // sg_shader_desc holds the bytecode by pointer, so a program built from bytecode
            // rather than source has to keep both spans pinned across the call. Source-built
            // programs (the path this backend normally takes - see SetShaderFunction) pin nothing.
            var vertexBytecode = vertex.VertexBytecode.Span;
            var pixelBytecode = pixel.PixelBytecode.Span;
            fixed (byte* vertexPointer = vertexBytecode)
            fixed (byte* pixelPointer = pixelBytecode)
            {
                if (!vertexBytecode.IsEmpty)
                    shaderDesc.vertex_func.bytecode = SokolNative.Range(vertexPointer, vertexBytecode.Length);
                if (!pixelBytecode.IsEmpty)
                    shaderDesc.fragment_func.bytecode = SokolNative.Range(pixelPointer, pixelBytecode.Length);

                var handle = Gfx.make_shader(&shaderDesc);
                if (handle.id == 0)
                    throw new InvalidOperationException("sg_make_shader failed - see the sokol log for the validation error.");
                return handle;
            }
        }
        finally
        {
            strings.Free();
        }
    }

    /// <summary>
    /// Fills one stage's <c>sg_shader_function</c>, preferring HLSL source over bytecode.
    ///
    /// Source is preferred because the native library in this repository is a D3D11-only build:
    /// it has no GL entry points, and its D3D11 shader path does not translate bytecode - it
    /// hands <c>bytecode.ptr</c> straight to <c>CreateVertexShader</c> as a DXBC blob
    /// (sokol_gfx.h:14318), or calls D3DCompile on <c>source</c> if that is set instead. The
    /// shader compiler's bundles carry SPIR-V, which is neither, so a bundle must supply the HLSL
    /// that spirv-cross emits alongside it for this backend to have anything to compile.
    /// </summary>
    private static void SetShaderFunction(
        sg_shader_function* function,
        string? source,
        ReadOnlyMemory<byte> bytecode,
        string entryPoint,
        Utf8Strings strings,
        string target,
        string which)
    {
        if (source is { Length: > 0 })
        {
            function->source = strings.Pin(source);
            function->entry = strings.Pin(entryPoint);
            function->d3d11_target = strings.Pin(target);
            return;
        }

        if (bytecode.IsEmpty)
            throw new InvalidOperationException(
                $"{which} has neither HLSL source nor bytecode. sokol's D3D11 backend accepts HLSL source " +
                "(compiled at make-shader time) or a DXBC blob - SPIR-V, which the shader compiler's bundles " +
                "currently carry, is neither. Pass the HLSL spirv-cross emits as the program's source.");

        function->entry = strings.Pin(entryPoint);
        function->d3d11_target = strings.Pin(target);
        // A fixed pointer is only valid until the enclosing statement, so the bytecode case is
        // handled by the caller's own pinning - see CreateShader.
    }

    private static sg_pipeline_desc BuildPipelineDesc(PipelineDesc desc, sg_shader shader, ShaderBindings layout)
    {
        var pipelineDesc = new sg_pipeline_desc
        {
            shader = shader,
            primitive_type = desc.Topology.ToNative(),
            // sokol bakes the index format into the pipeline, while the abstraction supplies it
            // per buffer at draw time. The app's index buffers are UInt16 on the paths this
            // backend is exercised on, so UInt16 is the assumption - a UInt32 index buffer drawn
            // through a UInt16 pipeline would misread it.
            index_type = sg_index_type.SG_INDEXTYPE_UINT16,
            cull_mode = desc.RasterizerState.CullMode.ToNative(),
            // XNA/FNA's winding convention is clockwise, which matches sokol's default - stated
            // rather than assumed so a future default change cannot silently flip every triangle.
            face_winding = sg_face_winding.SG_FACEWINDING_CW,
            // sokol requires the pipeline's sample count to match the pass it draws into. Most
            // pipelines here draw straight to the swapchain, so they take its count; a pipeline
            // aimed at a single-sampled off-screen target needs its own pipeline.
            sample_count = App.isvalid() != 0 ? Math.Max(App.sample_count(), 1) : 1,
        };

        // Vertex layout: one buffer per abstraction slot, with its attributes. sokol indexes
        // attributes globally across all buffers via attr.buffer_index, and pairs them with the
        // shader's attribute table by index - see CreateShader.
        var attributeIndex = 0;
        for (var slot = 0; slot < desc.VertexLayouts.Count && slot < 8; slot++)
        {
            var layoutDesc = desc.VertexLayouts[slot];
            pipelineDesc.layout.buffers[slot].stride = layoutDesc.StrideInBytes;
            pipelineDesc.layout.buffers[slot].step_rate = layoutDesc.InstanceStepRate;
            pipelineDesc.layout.buffers[slot].step_func = layoutDesc.InstanceStepRate > 0
                ? sg_vertex_step.SG_VERTEXSTEP_PER_INSTANCE
                : sg_vertex_step.SG_VERTEXSTEP_PER_VERTEX;

            foreach (var attribute in layoutDesc.Attributes)
            {
                if (attributeIndex >= 16) break;
                pipelineDesc.layout.attrs[attributeIndex].buffer_index = slot;
                pipelineDesc.layout.attrs[attributeIndex].offset = attribute.OffsetInBytes;
                pipelineDesc.layout.attrs[attributeIndex].format = attribute.Format.ToNative();
                attributeIndex++;
            }
        }

        ApplyBlendState(&pipelineDesc, desc);
        ApplyDepthStencilState(&pipelineDesc, desc);
        _ = layout;
        return pipelineDesc;
    }

    private static void ApplyBlendState(sg_pipeline_desc* pipelineDesc, PipelineDesc desc)
    {
        pipelineDesc->color_count = 1;
        pipelineDesc->colors[0].write_mask = desc.BlendState.ColorWriteMask.ToNative();
        pipelineDesc->colors[0].blend.enabled = SokolNative.Bool(desc.BlendState.Enabled);
        pipelineDesc->colors[0].blend.src_factor_rgb = desc.BlendState.SourceColor.ToNative();
        pipelineDesc->colors[0].blend.dst_factor_rgb = desc.BlendState.DestinationColor.ToNative();
        pipelineDesc->colors[0].blend.op_rgb = desc.BlendState.ColorOperation.ToNative();
        pipelineDesc->colors[0].blend.src_factor_alpha = desc.BlendState.SourceAlpha.ToNative();
        pipelineDesc->colors[0].blend.dst_factor_alpha = desc.BlendState.DestinationAlpha.ToNative();
        pipelineDesc->colors[0].blend.op_alpha = desc.BlendState.AlphaOperation.ToNative();
    }

    private static void ApplyDepthStencilState(sg_pipeline_desc* pipelineDesc, PipelineDesc desc)
    {
        var depth = desc.DepthStencilState;
        pipelineDesc->depth.compare = depth.DepthCompare.ToNative();
        pipelineDesc->depth.write_enabled = SokolNative.Bool(depth.DepthWriteEnabled);
        // sokol has no single "depth test on/off" flag - it is expressed through the compare
        // function, with ALWAYS being the pass-everything case that XNA's DepthTestEnabled=false
        // means (the read/write flags then decide whether depth is still recorded).
        if (!depth.DepthTestEnabled) pipelineDesc->depth.compare = sg_compare_func.SG_COMPAREFUNC_ALWAYS;

        pipelineDesc->stencil.enabled = SokolNative.Bool(depth.StencilTestEnabled);
        if (!depth.StencilTestEnabled) return;

        pipelineDesc->stencil.read_mask = (byte)depth.StencilReadMask;
        pipelineDesc->stencil.write_mask = (byte)depth.StencilWriteMask;
        pipelineDesc->stencil.@ref = (byte)depth.ReferenceStencil;
        pipelineDesc->stencil.front.compare = depth.StencilFunction.ToNative();
        pipelineDesc->stencil.front.fail_op = depth.StencilFail.ToNative();
        pipelineDesc->stencil.front.depth_fail_op = depth.StencilDepthFail.ToNative();
        pipelineDesc->stencil.front.pass_op = depth.StencilPass.ToNative();
        pipelineDesc->stencil.back.compare = depth.CcwStencilFunction.ToNative();
        pipelineDesc->stencil.back.fail_op = depth.CcwStencilFail.ToNative();
        pipelineDesc->stencil.back.depth_fail_op = depth.CcwStencilDepthFail.ToNative();
        pipelineDesc->stencil.back.pass_op = depth.CcwStencilPass.ToNative();
    }

    /// <summary>
    /// Reads a texture's pixels back into <paramref name="destination"/>.
    ///
    /// sokol_gfx provides no readback of any kind - there is no <c>sg_read_texture</c>, and the
    /// D3D11 backend never issues a staging-buffer copy - so this can only be served from the
    /// CPU-side mirror the backend keeps for otherwise-unreadable textures. That makes it exact
    /// for textures this backend uploaded and updated itself, and impossible for one the GPU
    /// rendered into.
    /// </summary>
    public void ReadTexture(ITexture texture, int x, int y, int width, int height, Span<byte> destination, int level = 0)
    {
        if (texture is not SokolTexture sokol)
            throw new ArgumentException($"{nameof(ReadTexture)} needs a texture this backend created.", nameof(texture));

        if (level != 0)
            throw new NotSupportedException("sokol_gfx exposes no readback and this backend only mirrors mip 0.");

        if (TryReadTexture(sokol, x, y, width, height, destination, out var error)) return;
        throw new NotSupportedException(error);
    }

    /// <summary>
    /// Mirror-backed readback. Returns false (with a reason) rather than throwing, so callers can
    /// tell "this texture has no mirror" apart from a genuine failure.
    /// </summary>
    internal static bool TryReadTexture(SokolTexture texture, int x, int y, int width, int height, Span<byte> destination, out string error)
    {
        if (texture.CpuShadow is not { } shadow)
        {
            error =
                "sokol_gfx has no texture readback, so this backend can only serve a texture it " +
                "uploaded or updated itself. Render-target and depth textures have no CPU mirror.";
            return false;
        }

        var bytesPerPixel = texture.BytesPerPixel;
        if (bytesPerPixel == 0)
        {
            error = $"{texture.Format} textures have no readable CPU mirror.";
            return false;
        }

        var rowBytes = width * bytesPerPixel;
        if (destination.Length < rowBytes * height)
        {
            error = $"Destination holds {destination.Length} bytes; {rowBytes * height} are required for {width}x{height}.";
            return false;
        }

        for (var row = 0; row < height; row++)
        {
            var source = ((y + row) * texture.Width + x) * bytesPerPixel;
            shadow.AsSpan(source, rowBytes).CopyTo(destination[(row * rowBytes)..]);
        }

        error = string.Empty;
        return true;
    }

    public void Dispose()
    {
        if (_shutdown) return;
        _shutdown = true;
        _swapchain = null;
    }
}

/// <summary>
/// UTF-8 strings living in unmanaged memory for the duration of one <c>sg_make_*</c> call.
///
/// sokol copies the strings it is given during the call, so they only need to be stable until it
/// returns - but they must be genuinely pinned. A <c>fixed</c> block cannot be used for the fields
/// of a descriptor (the addresses are written into the struct and read later, inside the native
/// call), so the memory is allocated outside the GC heap instead.
/// </summary>
internal sealed unsafe class Utf8Strings
{
    private readonly List<nint> _allocations = [];

    public sbyte* Pin(string value)
    {
        var bytes = SokolNative.Utf8Z(value);
        var pointer = (sbyte*)NativeMemory.Alloc((nuint)bytes.Length);
        for (var i = 0; i < bytes.Length; i++) pointer[i] = (sbyte)bytes[i];
        _allocations.Add((nint)pointer);
        return pointer;
    }

    public void Free()
    {
        foreach (var allocation in _allocations) NativeMemory.Free((void*)(nint)allocation);
        _allocations.Clear();
    }
}
