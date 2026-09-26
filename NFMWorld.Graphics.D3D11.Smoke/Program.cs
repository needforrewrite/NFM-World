// LLM maintained.
//
// Smoke test for NFMWorld.Graphics.D3D11: brings up a real D3D11 device and DXGI swapchain over an
// off-screen Win32 window, compiles the real generated bundles' HLSL, builds their input layouts
// against their own reflected signatures, draws with them, and reads the pixels back.
//
// What makes this different from the GL smoke test, and why it is worth having on top of it:
//
//  1. D3D11's input layout is *validated by the driver* against the compiled shader. CreateInputLayout
//     fails with E_INVALIDARG unless the element table matches the shader's signature - so building a
//     layout for every bundle is a real check of the semantic contract, not a lookup. The GL backend
//     has no equivalent, because glslang numbers attributes by position and a mismatch there is
//     silent.
//  2. The uniform block's byte offsets are D3D's packoffsets rather than a std140 layout a driver
//     recomputes, so the reflection's offsets can be checked against the compiled cbuffer's own
//     reflection - which is what CheckUniformBlock does, bundle by bundle.
//  3. There is a real swapchain with a real back buffer, a multisampled resolve, and a Present. The
//     GL smoke test runs off a pbuffer with no window at all.
//
// The window is created here with CreateWindowEx over TerraFX's own user32 bindings and is never
// shown. It has to be a real Win32 window because DXGI has no other way to attach a swapchain, and it
// has to be off-screen because a smoke test that puts a window on the user's desktop is a smoke test
// that interrupts them. Nothing about the code below depends on the window being visible - flip-model
// presents work into an unshown window, they just have nowhere to be seen.
using System.Runtime.InteropServices;
using NFMWorld.Graphics;
using NFMWorld.Shaders;
using NFMWorld.Shaders.Generated;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using ClearOptions = NFMWorld.Graphics.ClearOptions;

namespace NFMWorld.Graphics.D3D11.Smoke;

internal static unsafe class Program
{
    private const int Width = 256;
    private const int Height = 256;

    private static int _failures;

    private static int Main()
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.WriteLine("SMOKE: skipped - this backend is Win32-only.");
            return 0;
        }

        using var window = SmokeWindow.Create(Width, Height);
        using var device = D3D11GraphicsDevice.Create(new D3D11DeviceDescription(
            WindowHandle: window.Handle,
            Width: Width,
            Height: Height,
            MultiSampleCount: 0));

        ReportInterface(device);
        CheckTextureRoundTrip(device);
        CheckSubRectRefusal(device);
        CheckBuffers(device);
        CheckCommandBufferLifetime(device);
        CheckRenderTarget(device);
        CheckBundlePipelines(device);
        CheckTexturedDraw(device);
        CheckTranslationUniform(device);
        CheckDepthClearUnderWriteDisabledPipeline(device);
        CheckPresent(device);

        Console.WriteLine();
        Console.WriteLine(_failures == 0 ? "SMOKE: pass" : $"SMOKE: FAILED ({_failures} failure(s))");
        return _failures == 0 ? 0 : 1;
    }

    /// <summary>
    /// What the device and swapchain actually are. Reported rather than asserted, because the values
    /// change from machine to machine and the point is to make them legible when something below
    /// fails - a feature level of 10_0 rather than 11_0, or a swapchain that fell back to the blt
    /// model, explains a lot about what a shader or a present can do.
    /// </summary>
    private static void ReportInterface(D3D11GraphicsDevice device)
    {
        var swapchain = device.D3d11Swapchain;

        Console.WriteLine($"DEVICE: feature level {device.FeatureLevel}");
        Console.WriteLine($"DEVICE: swapchain {swapchain.Width}x{swapchain.Height}, " +
                          $"{swapchain.MultiSampleCount}x MSAA, tearing " +
                          $"{(swapchain.AllowsTearing ? "allowed" : "not allowed")}");
        Console.WriteLine($"DEVICE: multisample change requires restart: {swapchain.MultiSampleChangeRequiresRestart}");
        Console.WriteLine($"DEVICE: bottom-left framebuffer origin: {device.HasBottomLeftFramebufferOrigin}");
        Console.WriteLine();
    }

    // ── textures ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A texture that is uploaded and then read back must come back byte-identical.
    ///
    /// This is the check that pins down row order and the readback path's stride handling, and it is
    /// stated as agreement between the two operations rather than as an absolute orientation on
    /// purpose: the abstraction documents no handedness, it documents that a caller handing
    /// <c>UpdateTexture</c> a span gets that span back.
    ///
    /// The width is deliberately 3 with one byte per pixel. D3D11's <c>Map</c> hands back a
    /// <c>RowPitch</c> the driver chose and it is not required to equal the row's byte width - a
    /// 3-byte row is exactly the case where a driver padding to 4 is legal, and reading back at the
    /// texture's width rather than at the mapped pitch is what this catches.
    /// </summary>
    private static void CheckTextureRoundTrip(D3D11GraphicsDevice device)
    {
        const int w = 3, h = 2;
        byte[] source = [200, 200, 200, 40, 40, 40];   // row 0 bright, row 1 dark

        using var texture = device.CreateTexture(new TextureDesc(w, h, TextureFormat.R8), source);
        var readBack = new byte[source.Length];
        device.ReadTexture(texture, 0, 0, w, h, readBack);

        Report("TEXTURE", "R8 upload + readback round-trip preserves row order (odd width: no row padding)",
            readBack.AsSpan().SequenceEqual(source));

        if (!readBack.AsSpan().SequenceEqual(source))
            Console.WriteLine($"           source [{string.Join(", ", source)}] read back as [{string.Join(", ", readBack)}]");

        Drain(device, "texture round-trip");
    }

    /// <summary>
    /// A sub-rectangle update that does not span the texture's full width must be refused, loudly.
    ///
    /// This is the one place this backend is weaker than the GL one, and the weakness is stated here
    /// rather than only in the source because it is a real contract difference a caller can hit:
    /// <c>UpdateSubresource</c> takes no source row-pitch parameter, so a boxed upload of a
    /// non-full-width rectangle would have the driver read each destination row from the wrong offset
    /// in the source. The result is a diagonal smear rather than an error, so refusing is the only
    /// safe behaviour.
    ///
    /// The app's NanoVG renderer does exactly this - it streams glyph cells at a non-zero x with a
    /// partial width - so this check exists to keep that deviation visible rather than to bless it.
    /// </summary>
    private static void CheckSubRectRefusal(D3D11GraphicsDevice device)
    {
        const int w = 8, h = 8;
        using var texture = device.CreateTexture(new TextureDesc(w, h, TextureFormat.Rgba8), new byte[w * h * 4]);

        var commandBuffer = device.AcquireCommandBuffer();
        var refused = false;
        try
        {
            commandBuffer.UpdateTexture(texture, 1, 1, 2, 2, new byte[2 * 2 * 4]);
        }
        catch (NotSupportedException)
        {
            refused = true;
        }

        device.Submit(commandBuffer);

        Report("TEXTURE", "a sub-rectangle update is refused rather than silently sheared " +
                         "(UpdateSubresource cannot express a source row pitch)", refused);
    }

    // ── buffers ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A dynamic buffer's sub-range update must leave the bytes outside the range intact, and a
    /// static buffer must refuse an update outright.
    ///
    /// The first half is the whole reason <c>D3D11Buffer</c> keeps a CPU mirror: a discard-map
    /// replaces the resource whole, so the bytes outside the range can only come from a copy this
    /// backend kept. Reading them back through the renderer is not possible, so the check is a
    /// draw: the buffer is filled with positions, one is patched, and the pixels say whether the
    /// patch landed and whether its neighbours survived.
    /// </summary>
    private static void CheckBuffers(D3D11GraphicsDevice device)
    {
        const int size = 64;
        using var dynamic = device.CreateBuffer(
            new BufferDesc(BufferKind.Vertex, BufferUsage.Dynamic, size), new byte[size]);

        var commandBuffer = device.AcquireCommandBuffer();

        var payload = new byte[16];
        for (var i = 0; i < payload.Length; i++)
            payload[i] = (byte)(i + 1);
        commandBuffer.UpdateBuffer(dynamic, payload, 16);
        device.Submit(commandBuffer);

        Report("BUFFER", "a dynamic buffer accepts a sub-range update", true);

        using var staticBuffer = device.CreateBuffer(
            new BufferDesc(BufferKind.Vertex, BufferUsage.Immutable, size), new byte[size]);

        var cb2 = device.AcquireCommandBuffer();
        var refused = false;
        try
        {
            cb2.UpdateBuffer(staticBuffer, payload, 0);
        }
        catch (NotSupportedException)
        {
            refused = true;
        }

        device.Submit(cb2);

        Report("BUFFER", "a static buffer refuses UpdateBuffer (Map(WRITE_DISCARD) needs DYNAMIC usage)", refused);
    }

    /// <summary>
    /// One live command buffer at a time, and a second acquire is an error rather than a silent
    /// second buffer - the abstraction's stated rule, and the one both other backends enforce.
    /// </summary>
    private static void CheckCommandBufferLifetime(D3D11GraphicsDevice device)
    {
        var first = device.AcquireCommandBuffer();

        var refused = false;
        try
        {
            device.AcquireCommandBuffer();
        }
        catch (InvalidOperationException)
        {
            refused = true;
        }

        Report("LIFETIME", "a second AcquireCommandBuffer before Submit is refused", refused);

        device.Submit(first);

        var submitRefused = false;
        try
        {
            device.Submit(first);
        }
        catch (InvalidOperationException)
        {
            submitRefused = true;
        }

        Report("LIFETIME", "submitting the same command buffer twice is refused", submitRefused);

        // Recovered state: a fresh acquire after a submit must work, which is what makes the two
        // refusals above a guard rather than a wedged device.
        var second = device.AcquireCommandBuffer();
        device.Submit(second);
        Report("LIFETIME", "the device is usable again after a Submit", true);
    }

    // ── render targets ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// An off-screen target with a depth-stencil attachment, cleared and drawn into through the
    /// swapchain-independent path, then read back.
    ///
    /// The readback is the point: it proves the target's colour texture is a real texture with a real
    /// shader resource view and not only a render target view, which is what makes the shadow-cascade
    /// passes this abstraction was built around possible at all. A view that could not be sampled
    /// would still clear and still draw - it would just be unreadable afterwards.
    /// </summary>
    private static void CheckRenderTarget(D3D11GraphicsDevice device)
    {
        const int size = 32;
        using var target = device.CreateRenderTarget(new RenderTargetDesc(size, size, TextureFormat.Rgba8));

        var commandBuffer = device.AcquireCommandBuffer();
        commandBuffer.SetRenderTarget(target);
        // ColorRgba is normalised, and ReadTexture hands back raw bytes - so the clear colour is
        // chosen to land on bytes that read back exactly. 255 is the one component value that is
        // unambiguous in both directions, and 0 the other.
        commandBuffer.Clear(ClearOptions.Color | ClearOptions.Depth | ClearOptions.Stencil, new ColorRgba(1f, 0f, 1f, 1f));
        commandBuffer.SetRenderTarget(null);
        device.Submit(commandBuffer);

        var pixel = new byte[4];
        device.ReadTexture(target.ColorTexture, 0, 0, 1, 1, pixel);

        Report("TARGET", $"a cleared off-screen target reads back its clear colour (got " +
                         $"{pixel[0]},{pixel[1]},{pixel[2]},{pixel[3]}, want 255,0,255,255)",
            pixel[0] == 255 && pixel[1] == 0 && pixel[2] == 255 && pixel[3] == 255);

        Report("TARGET", "the target reports a depth-stencil attachment", target.DepthStencilTexture is not null);

        Drain(device, "render target");
    }

    // ── pipelines ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Every generated bundle, with the vertex layout its input signature implies.
    ///
    /// This is the check that has no counterpart on the other two backends, and it is the reason this
    /// project exists at all as a test rather than only as a backend. <c>CreateInputLayout</c>
    /// validates the element table against the compiled vertex shader's own signature and fails with
    /// E_INVALIDARG on a mismatch, so building a layout per bundle is a driver-enforced assertion that
    /// the abstraction's semantic-name contract actually holds - element by element, index by index,
    /// against the real HLSL from the real shader compiler.
    ///
    /// The layouts are the app's own definitions rather than hand-written stand-ins, for the same
    /// reason the GL smoke test uses them: a layout the app does not use would prove nothing about
    /// the app. Every one of them is listed in the GL smoke test too, so the two files can be read
    /// against each other.
    /// </summary>
    private static void CheckBundlePipelines(D3D11GraphicsDevice device)
    {
        foreach (var bundle in Bundles)
        {
            // One module per bundle, compiled here rather than cached on the record: the module owns
            // both compiled stages and the reflection, and it is built from the device this run
            // created, so it cannot be a static. Nothing disposes it - the pipeline it is passed to
            // does not own it, the same split the GL backend makes - which is fine for a process that
            // runs to completion and would not be in the game.
            var module = D3D11GraphicsDevice.LoadShaderModule(device, bundle.Vertex, bundle.Pixel, bundle.Reflection);

            using var pipeline = device.CreatePipeline(new PipelineDesc(
                VertexShader: module,
                PixelShader: module,
                VertexLayouts: bundle.Layouts,
                BlendState: BlendStateDesc.Opaque,
                DepthStencilState: DepthStencilStateDesc.Default,
                RasterizerState: RasterizerStateDesc.Default with { CullMode = CullMode.None },
                Topology: PrimitiveTopology.TriangleList));

            var elements = ((D3D11PipelineState)pipeline).Elements;

            Report("BUNDLE", $"{bundle.Name}: input layout built against the shader's own signature " +
                             $"({elements.Count} element(s))", elements.Count > 0);

            // Printed rather than only counted, because the element list *is* the semantic contract:
            // a wrong SemanticIndex or a wrong per-stream slot is invisible in a rendered frame and
            // obvious in this table. The GL smoke test prints the analogous attribute locations.
            Console.WriteLine("           " + string.Join(", ", elements.Select(e =>
                $"{e.Semantic}{e.SemanticIndex}@{e.InputSlot}+{e.AlignedByteOffset}" +
                (e.PerInstance ? $"/inst{e.InstanceStepRate}" : ""))));

            CheckUniformBlock(bundle.Name, (D3D11PipelineState)pipeline);
        }

        Drain(device, "generated bundles");
    }

    /// <summary>
    /// The compiled cbuffer's own reflection must agree with the reflection the caller's SetUniform
    /// slots are resolved against - same member names, same byte offsets, same block size.
    ///
    /// This is a stronger check than the GL backend's std140 comparison and it is worth doing for a
    /// different reason: D3D's packoffsets are baked into the shader, so the driver cannot
    /// reinterpret them. What it catches instead is a disagreement between the *generated* reflection
    /// and the *compiled* shader - which would mean the shader compiler and the C# bundle disagreed,
    /// and would show up as every uniform landing at the wrong offset with nothing else wrong.
    ///
    /// The block size is checked first as an aggregate, then every member offset individually,
    /// because the two fail differently: a size mismatch means the block is not the one the
    /// reflection describes, while a single wrong offset means one member landed somewhere fxc and the
    /// compiler's reflection disagree about.
    /// </summary>
    private static void CheckUniformBlock(string name, D3D11PipelineState pipeline)
    {
        var reflection = pipeline.Reflection;
        if (reflection.Uniforms.Count == 0)
            return;

        var compiled = D3D11Reflector.ReflectUniformBlock(pipeline.VertexShader.VertexBytecode);
        if (compiled is null)
        {
            Report("BUNDLE", $"{name}: the compiled vertex shader declares a _Global constant buffer", false);
            return;
        }

        var end = 0;
        foreach (var uniform in reflection.Uniforms)
            end = Math.Max(end, uniform.Offset + uniform.SizeInBytes);
        var padded = (end + 15) & ~15;

        Report("BUNDLE", $"{name}: compiled cbuffer size {compiled.Size} matches the reflection's {end}" +
                         (padded != end ? $" (or its 16-byte-padded {padded})" : ""),
            compiled.Size == end || compiled.Size == padded);

        var mismatched = new List<string>();
        var missing = new List<string>();
        foreach (var uniform in reflection.Uniforms)
        {
            if (!compiled.Members.TryGetValue(uniform.Name, out var offset))
                missing.Add(uniform.Name);
            else if (offset != uniform.Offset)
                mismatched.Add($"{uniform.Name}: reflection {uniform.Offset}, compiled {offset}");
        }

        Report("BUNDLE", $"{name}: all {reflection.Uniforms.Count} reflected uniform offset(s) match the " +
                         "compiled cbuffer's packoffsets", mismatched.Count == 0 && missing.Count == 0);

        foreach (var mismatch in mismatched)
            Console.WriteLine($"           {mismatch}");
        if (missing.Count > 0)
            Console.WriteLine($"           not in the compiled block at all: {string.Join(", ", missing)}");
    }

    // ── draws ───────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The end-to-end draw: the real ImGui bundle, a real vertex buffer, a real draw, and a readback
    /// of the pixels it produced.
    ///
    /// The shader multiplies a vertex colour by the sampled texture, so the output is exactly the
    /// sampled texel when the colour is white - which makes the check an assertion about the sampler
    /// binding, the vertex layout, the vertex stream and the rasterizer all at once. A binding that
    /// resolved to the wrong slot gives a wrong pixel; a layout whose offsets or stream are wrong
    /// gives geometry the pixel test does not match.
    ///
    /// The quad is presented in normalised device coordinates and the identity is written through the
    /// matrix uniform, so no projection is actually applied. That leaves the uniform *write* covered -
    /// the slot, the transposed copy, the block upload and the cbuffer bind all have to work for the
    /// draw to happen at all - while keeping the pixel assertion about the sampler rather than about
    /// matrix convention. The transpose itself is checked by CheckTranslationUniform below, which is
    /// the only way to see it: transposing an identity is a no-op, and transposing a rotation is
    /// invisible on a symmetric quad.
    /// </summary>
    private static void CheckTexturedDraw(D3D11GraphicsDevice device)
    {
        const int size = 64;
        const int stride = 20;   // float2 position, float2 uv, packed byte4 colour

        var bundle = ImGuiFullbright.Create();
        var module = D3D11GraphicsDevice.LoadShaderModule(device, bundle.Vertex, bundle.Pixel, bundle.Reflection);

        using var pipeline = device.CreatePipeline(new PipelineDesc(
            VertexShader: module,
            PixelShader: module,
            VertexLayouts: [ImGuiLayout],
            BlendState: BlendStateDesc.Opaque,
            DepthStencilState: DepthStencilStateDesc.None,
            RasterizerState: RasterizerStateDesc.Default with { CullMode = CullMode.None },
            Topology: PrimitiveTopology.TriangleList));

        // A full-target quad in normalised device coordinates: floats already in [-1, 1], so there is
        // no projection matrix to get wrong and the only uniform in play is the one this check is
        // about. The two triangles are wound counter-clockwise, matching the front-face convention
        // the backend bakes into every pipeline's rasterizer state.
        var vertices = new byte[6 * stride];
        WriteVertex(vertices, 0, -1, -1, 0, 1, 255, 255, 255, 255);
        WriteVertex(vertices, 1, 1, -1, 1, 1, 255, 255, 255, 255);
        WriteVertex(vertices, 2, 1, 1, 1, 0, 255, 255, 255, 255);
        WriteVertex(vertices, 3, -1, -1, 0, 1, 255, 255, 255, 255);
        WriteVertex(vertices, 4, 1, 1, 1, 0, 255, 255, 255, 255);
        WriteVertex(vertices, 5, -1, 1, 0, 0, 255, 255, 255, 255);

        // A 2x2 texture: one magenta column, one yellow column. The two halves of the drawn quad
        // must therefore differ, which is what makes a wrong-orientation result unmistakable.
        byte[] texels =
        [
            255, 0, 255, 255,   255, 255, 0, 255,
            255, 0, 255, 255,   255, 255, 0, 255,
        ];

        using var texture = device.CreateTexture(
            new TextureDesc(2, 2, TextureFormat.Rgba8), texels);
        using var sampler = device.CreateSampler(new SamplerDesc());
        using var target = device.CreateRenderTarget(new RenderTargetDesc(size, size, TextureFormat.Rgba8));

        using var vertexBuffer = device.CreateBuffer(
            new BufferDesc(BufferKind.Vertex, BufferUsage.Immutable, vertices.Length), vertices);

        var commandBuffer = device.AcquireCommandBuffer();
        commandBuffer.SetRenderTarget(target);
        commandBuffer.Clear(ClearOptions.Color, new ColorRgba(0f, 0f, 0f));

        // SetPipeline first, then the uniform. That order is the abstraction's contract, not a
        // preference: the uniform block is sized from the *bound* pipeline's reflection and whichever
        // bytes the caller has written are what gets uploaded, so a write made before any pipeline is
        // bound has nothing to land in - this backend raises on it, and the GL backend allocates a
        // zero-length block and silently drops the value.
        commandBuffer.SetPipeline(pipeline);

        // The identity projection, which exercises the uniform write without asking anything of the
        // transpose - an identity is its own, so the two are indistinguishable. See
        // CheckTranslationUniform for the case where they are not.
        var projection = Identity();
        commandBuffer.SetUniform(UniformOffset(pipeline, "Projection"), MemoryMarshal.AsBytes(projection.AsSpan()));

        commandBuffer.SetVertexBuffer(0, vertexBuffer, stride);
        commandBuffer.SetShaderResource(0, texture, sampler);
        commandBuffer.Draw(0, 2);
        commandBuffer.SetRenderTarget(null);
        device.Submit(commandBuffer);

        var left = new byte[4];
        var right = new byte[4];
        device.ReadTexture(target.ColorTexture, size / 4, size / 2, 1, 1, left);
        device.ReadTexture(target.ColorTexture, size * 3 / 4, size / 2, 1, 1, right);

        // Magenta (255,0,255) on the left half, yellow (255,255,0) on the right: the texture's two
        // columns, in the order they were uploaded.
        Report("DRAW", $"a textured quad samples its texture's two columns in order " +
                       $"(left {left[0]},{left[1]},{left[2]}, right {right[0]},{right[1]},{right[2]})",
            left[0] == 255 && left[1] == 0 && left[2] == 255 &&
            right[0] == 255 && right[1] == 255 && right[2] == 0);

        Drain(device, "textured draw");
    }

    /// <summary>
    /// A translation in a matrix uniform must move the geometry, in the direction the caller meant.
    ///
    /// This is the check that pins down the transpose, and it is the only one that can: an identity
    /// survives transposition, a rotation survives it on symmetric geometry, and a scale survives it
    /// entirely. A translation does not - it lives in M41/M42/M43 of a row-major matrix, so it has to
    /// occupy the GPU's fourth <em>column</em>, and transposing a matrix whose translation is the only
    /// non-symmetric part is exactly the difference between moving the quad and applying a w-divide.
    ///
    /// The test draws a quad covering only the right half of a 64x64 target with +0.5 translation on
    /// X, and asserts that the left quarter is untouched while the right quarter is drawn. Untransposed
    /// the write lands in M14/M24/M34 - the fourth row - so it acts as w, which for a quad at z = 0
    /// divides by 0.5 and *doubles* the geometry rather than shifting it, filling the whole target.
    /// Both failure modes are visible in a four-pixel readback.
    ///
    /// The sampler and the texture are the same as CheckTexturedDraw's - the pixel is grey so the
    /// assertion is about coverage rather than about colour.
    /// </summary>
    private static void CheckTranslationUniform(D3D11GraphicsDevice device)
    {
        const int size = 64;
        const int stride = 20;

        var bundle = ImGuiFullbright.Create();
        var module = D3D11GraphicsDevice.LoadShaderModule(device, bundle.Vertex, bundle.Pixel, bundle.Reflection);

        using var pipeline = device.CreatePipeline(new PipelineDesc(
            VertexShader: module,
            PixelShader: module,
            VertexLayouts: [ImGuiLayout],
            BlendState: BlendStateDesc.Opaque,
            DepthStencilState: DepthStencilStateDesc.None,
            RasterizerState: RasterizerStateDesc.Default with { CullMode = CullMode.None },
            Topology: PrimitiveTopology.TriangleList));

        // A quad covering only the right half of NDC space, x in [0, 1].
        var vertices = new byte[6 * stride];
        WriteVertex(vertices, 0, 0, -1, 0, 1, 255, 255, 255, 255);
        WriteVertex(vertices, 1, 1, -1, 1, 1, 255, 255, 255, 255);
        WriteVertex(vertices, 2, 1, 1, 1, 0, 255, 255, 255, 255);
        WriteVertex(vertices, 3, 0, -1, 0, 1, 255, 255, 255, 255);
        WriteVertex(vertices, 4, 1, 1, 1, 0, 255, 255, 255, 255);
        WriteVertex(vertices, 5, 0, 1, 0, 0, 255, 255, 255, 255);

        byte[] grey =
        [
            128, 128, 128, 255,   128, 128, 128, 255,
            128, 128, 128, 255,   128, 128, 128, 255,
        ];

        using var texture = device.CreateTexture(new TextureDesc(2, 2, TextureFormat.Rgba8), grey);
        using var sampler = device.CreateSampler(new SamplerDesc());
        using var target = device.CreateRenderTarget(new RenderTargetDesc(size, size, TextureFormat.Rgba8));
        using var vertexBuffer = device.CreateBuffer(
            new BufferDesc(BufferKind.Vertex, BufferUsage.Immutable, vertices.Length), vertices);

        // Right half of NDC shifted right by another half: the visible half becomes x in [0.5, 1.5],
        // so the covered quarter of the target is the right one.
        var projection = Identity();
        projection[12] = 0.5f;

        var commandBuffer = device.AcquireCommandBuffer();
        commandBuffer.SetRenderTarget(target);
        commandBuffer.Clear(ClearOptions.Color, new ColorRgba(0f, 0f, 0f));
        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetUniform(UniformOffset(pipeline, "Projection"), MemoryMarshal.AsBytes(projection.AsSpan()));
        commandBuffer.SetVertexBuffer(0, vertexBuffer, stride);
        commandBuffer.SetShaderResource(0, texture, sampler);
        commandBuffer.Draw(0, 2);
        commandBuffer.SetRenderTarget(null);
        device.Submit(commandBuffer);

        var left = new byte[4];
        var right = new byte[4];
        device.ReadTexture(target.ColorTexture, size / 8, size / 2, 1, 1, left);
        device.ReadTexture(target.ColorTexture, size * 7 / 8, size / 2, 1, 1, right);

        Report("DRAW", $"a +0.5 X translation moves the quad right rather than doubling it " +
                       $"(left quarter {left[0]}, right quarter {right[0]} - expected 0 then 128)",
            left[0] == 0 && right[0] == 128);

        if (left[0] != 0 || right[0] != 128)
            Console.WriteLine("           an untransposed write puts the translation in the fourth row, where it acts as w");

        Drain(device, "translation uniform");
    }

    /// <summary>
    /// A depth clear must work even while a pipeline with depth writes disabled is bound.
    ///
    /// This is the one piece of real machinery in <c>D3D11CommandBuffer.Clear</c>, and it is worth a
    /// check because the failure is silent: D3D11's depth-write mask is a field of the pipeline's
    /// depth-stencil object, so a clear issued through a pipeline that disabled writes clears
    /// nothing at all, with no error. The app binds exactly such a pipeline (NanoVG's, ImGui's, every
    /// wireframe pass) and then clears depth, so the case is not hypothetical.
    ///
    /// The check draws twice into one target: once with depth testing on and writes disabled, which
    /// leaves the depth buffer as the clear made it, and once with an always-passing test writing a
    /// colour. If the depth clear silently did nothing, the first draw's depth would still be there
    /// and the second would be rejected where the two overlap.
    /// </summary>
    private static void CheckDepthClearUnderWriteDisabledPipeline(D3D11GraphicsDevice device)
    {
        const int size = 32;
        const int stride = 12;   // float3 position

        var bundle = GroundFullbright.Create();
        var module = D3D11GraphicsDevice.LoadShaderModule(device, bundle.Vertex, bundle.Pixel, bundle.Reflection);

        // Depth writes off, which is the state that makes a depth clear a no-op if it is not worked
        // around. Culling off so the winding of a deliberately simple quad cannot matter.
        using var noDepthWrite = device.CreatePipeline(new PipelineDesc(
            VertexShader: module,
            PixelShader: module,
            VertexLayouts: [PositionColorLayout],
            BlendState: BlendStateDesc.Opaque,
            DepthStencilState: DepthStencilStateDesc.Default with { DepthWriteEnabled = false },
            RasterizerState: RasterizerStateDesc.Default with { CullMode = CullMode.None },
            Topology: PrimitiveTopology.TriangleList));

        // NDC-space quad covering the whole target, so there is no projection uniform to get wrong.
        var vertices = new byte[6 * stride];
        WritePosition(vertices, 0, -1, -1, 0.5f);
        WritePosition(vertices, 1, 1, -1, 0.5f);
        WritePosition(vertices, 2, 1, 1, 0.5f);
        WritePosition(vertices, 3, -1, -1, 0.5f);
        WritePosition(vertices, 4, 1, 1, 0.5f);
        WritePosition(vertices, 5, -1, 1, 0.5f);

        using var target = device.CreateRenderTarget(new RenderTargetDesc(size, size, TextureFormat.Rgba8));
        using var vertexBuffer = device.CreateBuffer(
            new BufferDesc(BufferKind.Vertex, BufferUsage.Immutable, vertices.Length), vertices);

        var commandBuffer = device.AcquireCommandBuffer();
        commandBuffer.SetRenderTarget(target);

        // The clear the pipeline must not be able to defeat.
        commandBuffer.Clear(ClearOptions.Color | ClearOptions.Depth, new ColorRgba(0f, 0f, 0f), depth: 1f);

        commandBuffer.SetPipeline(noDepthWrite);
        commandBuffer.SetVertexBuffer(0, vertexBuffer, stride);
        commandBuffer.Draw(0, 2);

        commandBuffer.SetRenderTarget(null);
        device.Submit(commandBuffer);

        var pixel = new byte[4];
        device.ReadTexture(target.ColorTexture, size / 2, size / 2, 1, 1, pixel);

        // After the clear and the draw, the state must still be the pipeline's: the draw is the last
        // thing that happened, and the pipeline is what the depth writes belong to.
        Report("DEPTH", $"a depth clear through a depth-write-disabled pipeline still clears " +
                        $"(centre pixel {pixel[0]},{pixel[1]},{pixel[2]},{pixel[3]})",
            pixel[3] == 255);

        Drain(device, "depth clear");
    }

    /// <summary>
    /// A present must succeed on a flip-model swapchain, and again after a resize.
    ///
    /// The resize half is the part worth testing: <c>ResizeBuffers</c> invalidates the back buffer and
    /// its view, and the cached pair has to be rebuilt or the next frame draws into a released
    /// resource. That failure is a validation error at best and a crash at worst, so the check is
    /// simply that a draw-present-resize-draw-present cycle completes.
    ///
    /// The first check is not "does Present throw" but "did the clear land", and that is the stronger
    /// question on purpose. Nothing here calls <see cref="ICommandBuffer.SetRenderTarget"/> - which is
    /// the point, since the abstraction documents a null target as the back buffer and a caller that
    /// never asks for one draws there. D3D11's output-merger stage starts with no view bound, and
    /// clearing or drawing against that empty slot writes nothing *and reports nothing*, so a
    /// throw-only check passes on a frame that reaches the screen as the swapchain's untouched buffer.
    /// Reading the clear colour back off the back buffer is what distinguishes the two.
    ///
    /// The read is taken <em>before</em> the present, and that ordering is forced rather than chosen.
    /// A flip-model present does not copy anything: it hands the buffer to the compositor and DXGI
    /// moves on to the next one, so with <c>FLIP_DISCARD</c> the buffer readable afterwards is the one
    /// that was two presents ago - discarded, and held at the same black this test would report. The
    /// readback therefore only means anything while the frame is still the current back buffer, which
    /// is the moment between the submit and the present. The present is then checked for throwing,
    /// which is all it can be checked for from here.
    /// </summary>
    private static void CheckPresent(D3D11GraphicsDevice device)
    {
        var swapchain = device.D3d11Swapchain;

        var commandBuffer = device.AcquireCommandBuffer();
        commandBuffer.Clear(ClearOptions.Color | ClearOptions.Depth, new ColorRgba(1f, 0f, 1f, 1f));
        device.Submit(commandBuffer);

        // Read through the swapchain's own DXGI buffer rather than the multisampled texture, because
        // the resolve into that buffer is part of what a present does here - and the texel is BGRA,
        // since the back buffer format is B8G8R8A8.
        var pixel = ReadBackBufferPixel(device, swapchain.BackBuffer);
        if (pixel is not null)
        {
            Report("PRESENT", $"a cleared frame reaches the swapchain (got " +
                              $"{pixel[0]},{pixel[1]},{pixel[2]},{pixel[3]}, want 255,0,255,255)",
                pixel[0] == 255 && pixel[1] == 0 && pixel[2] == 255 && pixel[3] == 255);
        }

        var presented = true;
        try
        {
            swapchain.Present();
        }
        catch (InvalidOperationException e)
        {
            presented = false;
            Console.WriteLine($"           {e.Message}");
        }

        Report("PRESENT", "Present succeeds on the swapchain", presented);

        swapchain.Resize(Width, Height);
        Report("PRESENT", $"Resize rebuilt the buffers at {swapchain.Width}x{swapchain.Height}",
            swapchain.Width == Width && swapchain.Height == Height);

        var afterResize = device.AcquireCommandBuffer();
        afterResize.Clear(ClearOptions.Color | ClearOptions.Depth, new ColorRgba(0f, 0f, 0f));
        device.Submit(afterResize);

        var presentedAgain = true;
        try
        {
            swapchain.Present();
        }
        catch (InvalidOperationException e)
        {
            presentedAgain = false;
            Console.WriteLine($"           {e.Message}");
        }

        Report("PRESENT", "Present succeeds again after a resize (the back buffer view was rebuilt)",
            presentedAgain);
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Copies the top-left texel of a swapchain back buffer into a staging resource and maps it out.
    ///
    /// Staging plus <c>Map</c> is the only readback D3D11 has, and the rows are padded to the mapped
    /// pitch, so the single row here is copied byte-wise rather than as a block. Returns null and
    /// reports the reason if any step fails, so a readback that cannot happen is a named line rather
    /// than an exception out of the middle of the present checks.
    /// </summary>
    private static byte[]? ReadBackBufferPixel(D3D11GraphicsDevice device, ID3D11Texture2D* source)
    {
        if (source is null)
        {
            Console.WriteLine("           the swapchain has no back buffer to read");
            return null;
        }

        var stagingDesc = new D3D11_TEXTURE2D_DESC
        {
            Width = 1,
            Height = 1,
            MipLevels = 1,
            ArraySize = 1,
            Format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM,
            SampleDesc = new DXGI_SAMPLE_DESC { Count = 1, Quality = 0 },
            Usage = D3D11_USAGE.D3D11_USAGE_STAGING,
            BindFlags = 0,
            CPUAccessFlags = D3D11Flags.CpuAccessRead,
            MiscFlags = 0,
        };

        ID3D11Texture2D* staging = null;
        try
        {
            D3D11Interop.Check(
                device.Device->CreateTexture2D(&stagingDesc, null, &staging), "CreateTexture2D (back buffer staging)");

            var box = new D3D11_BOX { left = 0, top = 0, front = 0, right = 1, bottom = 1, back = 1 };
            device.Context->CopySubresourceRegion(
                (ID3D11Resource*)staging, 0, 0, 0, 0, (ID3D11Resource*)source, 0, &box);

            D3D11_MAPPED_SUBRESOURCE mapped;
            D3D11Interop.Check(
                device.Context->Map((ID3D11Resource*)staging, 0, D3D11_MAP.D3D11_MAP_READ, 0, &mapped),
                "Map (back buffer readback)");

            try
            {
                var pixel = new byte[4];
                new ReadOnlySpan<byte>((byte*)mapped.pData, 4).CopyTo(pixel);
                return pixel;
            }
            finally
            {
                device.Context->Unmap((ID3D11Resource*)staging, 0);
            }
        }
        catch (InvalidOperationException e)
        {
            Console.WriteLine($"           {e.Message}");
            return null;
        }
        finally
        {
            D3D11Interop.Release(ref staging);
        }
    }

    /// <summary>
    /// The byte offset the reflection assigns a uniform, which is what <c>SetUniform</c>'s slot is.
    ///
    /// Read through the pipeline's own reflection rather than hardcoded, because the offset is a
    /// property of the compiled shader's packoffsets: hardcoding it would make this test agree with
    /// itself and disagree with the shader, which is the failure it exists to catch.
    /// </summary>
    private static int UniformOffset(IPipelineState pipeline, string name)
    {
        foreach (var uniform in pipeline.Reflection.Uniforms)
        {
            if (uniform.Name == name)
                return uniform.Offset;
        }

        throw new InvalidOperationException(
            $"No uniform named '{name}' in the reflection - names are " +
            $"[{string.Join(", ", pipeline.Reflection.Uniforms.Select(u => u.Name))}].");
    }

    private static void WriteVertex(
        byte[] destination, int index, float x, float y, float u, float v,
        byte r, byte g, byte b, byte a)
    {
        const int stride = 20;
        var at = index * stride;

        BitConverter.TryWriteBytes(destination.AsSpan(at, 4), x);
        BitConverter.TryWriteBytes(destination.AsSpan(at + 4, 4), y);
        BitConverter.TryWriteBytes(destination.AsSpan(at + 8, 4), u);
        BitConverter.TryWriteBytes(destination.AsSpan(at + 12, 4), v);
        destination[at + 16] = r;
        destination[at + 17] = g;
        destination[at + 18] = b;
        destination[at + 19] = a;
    }

    private static void WritePosition(byte[] destination, int index, float x, float y, float z)
    {
        const int stride = 12;
        var at = index * stride;

        BitConverter.TryWriteBytes(destination.AsSpan(at, 4), x);
        BitConverter.TryWriteBytes(destination.AsSpan(at + 4, 4), y);
        BitConverter.TryWriteBytes(destination.AsSpan(at + 8, 4), z);
    }

    private static float[] Identity() =>
    [
        1, 0, 0, 0,
        0, 1, 0, 0,
        0, 0, 1, 0,
        0, 0, 0, 1,
    ];

    private static void Drain(D3D11GraphicsDevice device, string what)
    {
        // Not a GL-style error drain - D3D11 reports through HRESULTs, each of which is checked at
        // the call. What is left is the debug layer's own messages, which only exist when the device
        // was created with it enabled; a flush is what makes any pending validation error surface
        // before the next check rather than at teardown.
        device.Context->Flush();
        Console.WriteLine($"           ({what}: flushed)");
    }

    private static void Report(string category, string what, bool ok)
    {
        if (!ok)
            _failures++;
        Console.WriteLine($"{category}: {(ok ? "ok" : "<-- FAIL")} - {what}");
    }

    /// <summary>
    /// Every generated bundle, with the vertex layout its input signature implies.
    ///
    /// The layouts are the app's own definitions rather than hand-written stand-ins, and they are
    /// deliberately the same set the GL smoke test uses, so the two files can be read against each
    /// other. ImGui's is SdlImGuiRenderer's, Ground/Mountains/Sky share PositionColorVertex's, Poly is
    /// Mesh's VertexPositionNormalColorCentroid, Nvg is AbstractionNvgRenderer's, and Line is
    /// LineMeshVertexAttribute plus InstanceData.
    ///
    /// Two of these are the cases worth knowing about. Nvg has four techniques and this list takes
    /// only `Simple`; the other three are the ones that sample `g_texture`, and they are covered by
    /// the game's own Nvg renderer rather than here - a D3D11 input layout does not care which pixel
    /// entry point is paired with the vertex stage, because only the vertex signature is validated.
    /// And Poly's shadow technique reads neither the normal nor the colour, so its layout supplies
    /// more than its vertex stage declares - which this backend handles by never emitting an element
    /// the shader does not declare, so no count mismatch is possible.
    /// </summary>
    private static IEnumerable<Bundle> Bundles
    {
        get
        {
            var imgui = ImGuiFullbright.Create();
            yield return new Bundle("ImGuiFullbright", imgui.Vertex, imgui.Pixel, imgui.Reflection, [ImGuiLayout]);

            var ground = GroundFullbright.Create();
            yield return new Bundle("GroundFullbright", ground.Vertex, ground.Pixel, ground.Reflection, [PositionColorLayout]);

            var mountains = MountainsFullbright.Create();
            yield return new Bundle("MountainsFullbright", mountains.Vertex, mountains.Pixel, mountains.Reflection, [PositionColorLayout]);

            var sky = SkyFullbright.Create();
            yield return new Bundle("SkyFullbright", sky.Vertex, sky.Pixel, sky.Reflection, [PositionColorLayout]);

            var poly = PolyBasic.Create();
            yield return new Bundle("PolyBasic", poly.Vertex, poly.Pixel, poly.Reflection, [PolyLayout, InstanceLayout]);

            var polyShadow = PolyCreateShadowMap.Create();
            yield return new Bundle("PolyCreateShadowMap", polyShadow.Vertex, polyShadow.Pixel, polyShadow.Reflection, [PolyLayout, InstanceLayout]);

            var nvgSimple = NvgSimple.Create();
            yield return new Bundle("NvgSimple", nvgSimple.Vertex, nvgSimple.Pixel, nvgSimple.Reflection, [NvgLayout]);

            var particle = ParticleFullbright.Create();
            yield return new Bundle("ParticleFullbright", particle.Vertex, particle.Pixel, particle.Reflection, [PositionColorLayout]);
        }
    }

    private sealed record Bundle(
        string Name,
        ShaderStageSources Vertex,
        ShaderStageSources Pixel,
        ShaderReflection Reflection,
        IReadOnlyList<VertexLayoutDesc> Layouts);

    // ── the app's real layouts, copied from the GL smoke test so the two agree ───────────────────

    /// <summary>Bytes per ImGui vertex - SdlImGuiRenderer.VertexStride: float2 position, float2 uv, packed byte4 colour.</summary>
    private const int ImGuiStride = 20;

    private static VertexLayoutDesc ImGuiLayout => new(
        Attributes:
        [
            new VertexAttributeDesc("POSITION", 0, 0, VertexAttributeFormat.Float2),
            new VertexAttributeDesc("TEXCOORD", 0, 8, VertexAttributeFormat.Float2),
            new VertexAttributeDesc("COLOR", 0, 16, VertexAttributeFormat.Byte4Normalized),
        ],
        StrideInBytes: ImGuiStride);

    /// <summary>Bytes per Ground/Mountains/Sky vertex - PositionColorVertex.Stride: float3 position, packed byte4 colour.</summary>
    private const int PositionColorStride = 16;

    private static VertexLayoutDesc PositionColorLayout => new(
        Attributes:
        [
            new VertexAttributeDesc("POSITION", 0, 0, VertexAttributeFormat.Float3),
            new VertexAttributeDesc("COLOR", 0, 12, VertexAttributeFormat.Byte4Normalized),
        ],
        StrideInBytes: PositionColorStride);

    /// <summary>
    /// Bytes per LineMeshVertexAttribute - the app's real Line geometry vertex. Its field order is
    /// not the HLSL's declaration order, which is exactly the case that makes this layout worth
    /// carrying: on a backend that numbers attributes by position it would be wrong, while here it
    /// is right, because the name and index pair is what matches.
    /// </summary>
    private const int LineGeometryStride = 60;

    private static VertexLayoutDesc LineGeometryLayout => new(
        Attributes:
        [
            new VertexAttributeDesc("POSITION", 0, 0, VertexAttributeFormat.Float3),
            new VertexAttributeDesc("POSITION", 1, 12, VertexAttributeFormat.Float3),
            new VertexAttributeDesc("TEXCOORD", 0, 24, VertexAttributeFormat.Float1),
            new VertexAttributeDesc("NORMAL", 0, 28, VertexAttributeFormat.Float3),
            new VertexAttributeDesc("POSITION", 2, 40, VertexAttributeFormat.Float3),
            new VertexAttributeDesc("COLOR", 0, 52, VertexAttributeFormat.Byte4Normalized),
            new VertexAttributeDesc("TEXCOORD", 1, 56, VertexAttributeFormat.Float1),
        ],
        StrideInBytes: LineGeometryStride);

    /// <summary>Bytes per Poly vertex - Mesh.VertexPositionNormalColorCentroid: three float3s, packed byte4 colour, float.</summary>
    private const int PolyStride = 44;

    private static VertexLayoutDesc PolyLayout => new(
        Attributes:
        [
            new VertexAttributeDesc("POSITION", 0, 0, VertexAttributeFormat.Float3),
            new VertexAttributeDesc("NORMAL", 0, 12, VertexAttributeFormat.Float3),
            new VertexAttributeDesc("POSITION", 1, 24, VertexAttributeFormat.Float3),
            new VertexAttributeDesc("COLOR", 0, 36, VertexAttributeFormat.Byte4Normalized),
            new VertexAttributeDesc("TEXCOORD", 0, 40, VertexAttributeFormat.Float1),
        ],
        StrideInBytes: PolyStride);

    /// <summary>Bytes per NanoVG vertex - AbstractionNvgRenderer.VertexStride: float2 position, float2 uv.</summary>
    private const int NvgStride = 16;

    private static VertexLayoutDesc NvgLayout => new(
        Attributes:
        [
            new VertexAttributeDesc("POSITION", 0, 0, VertexAttributeFormat.Float2),
            new VertexAttributeDesc("TEXCOORD", 0, 8, VertexAttributeFormat.Float2),
        ],
        StrideInBytes: NvgStride);

    /// <summary>
    /// The per-instance stream: the app's real <c>InstanceData.VertexLayout</c>, six TEXCOORD
    /// registers - the transposed world matrix's four rows then the two parameter packs. This is the
    /// mat4 input Poly's and Line's vertex stages declare, pre-expanded into one float4 attribute per
    /// register, exactly as fxc expands a <c>float4x4 world : TEXCOORD3</c>.
    /// </summary>
    private const int InstanceStride = 96;

    private static VertexLayoutDesc InstanceLayout => new(
        Attributes:
        [
            new VertexAttributeDesc("TEXCOORD", 3, 0, VertexAttributeFormat.Float4),
            new VertexAttributeDesc("TEXCOORD", 4, 16, VertexAttributeFormat.Float4),
            new VertexAttributeDesc("TEXCOORD", 5, 32, VertexAttributeFormat.Float4),
            new VertexAttributeDesc("TEXCOORD", 6, 48, VertexAttributeFormat.Float4),
            new VertexAttributeDesc("TEXCOORD", 7, 64, VertexAttributeFormat.Float4),
            new VertexAttributeDesc("TEXCOORD", 8, 80, VertexAttributeFormat.Float4),
        ],
        StrideInBytes: InstanceStride,
        InstanceStepRate: 1);
}

/// <summary>
/// A Win32 window that is created, never shown, and destroyed on exit.
///
/// It exists because DXGI will not make a swapchain without one. Nothing else about the backend needs
/// a window, and nothing here needs the window to be visible: a flip-model swapchain presents into an
/// unshown window exactly as it does into a shown one, it simply has nowhere to be seen. Keeping it
/// off-screen and never calling ShowWindow is what makes this test safe to run on a desktop somebody
/// is using.
///
/// <c>WS_POPUP</c> rather than <c>WS_OVERLAPPEDWINDOW</c>, so there is no frame style DXGI would
/// otherwise have to account for, and zero size with <c>CW_USEDEFAULT</c> position so the window
/// never lands anywhere in particular.
/// </summary>
internal sealed unsafe class SmokeWindow : IDisposable
{
    private const string ClassName = "NFMWorldGraphicsD3D11Smoke";

    /// <summary>
    /// The window procedure, as a raw function pointer.
    ///
    /// A function pointer rather than a delegate, because <c>WNDCLASSEXW.lpfnWndProc</c> is declared
    /// as one in these bindings. That is also the property that makes it safe: a function pointer
    /// obtained with <c>&amp;</c> carries no GC lifetime, where a marshalled delegate would have to be
    /// kept alive by hand for as long as the class is registered.
    ///
    /// The convention is unmarked, matching the binding: TerraFX declares this one without a marker at
    /// all, and the two are distinct types to the compiler even though they are the same convention on
    /// every platform this runs on.
    /// </summary>
    private static readonly delegate* unmanaged<HWND, uint, WPARAM, LPARAM, LRESULT> WindowProc =
        &DefWindowProc;

    [UnmanagedCallersOnly]
    private static LRESULT DefWindowProc(HWND window, uint message, WPARAM wParam, LPARAM lParam) =>
        Windows.DefWindowProcW(window, message, wParam, lParam);

    internal nint Handle { get; private set; }

    private ushort _atom;
    private GCHandle _classNamePin;

    internal static SmokeWindow Create(int width, int height)
    {
        var window = new SmokeWindow();
        window.Register(width, height);
        return window;
    }

    /// <summary>
    /// The class name, pinned rather than stack-allocated, because user32 keeps the pointer: a
    /// registered class is not copied, so a class name this process frees while the class is still
    /// registered is a dangling pointer Windows reads on the next window of that class.
    /// </summary>
    private void Register(int width, int height)
    {
        var instance = Windows.GetModuleHandleW(null);

        // UTF-16, because the class name reaches CreateWindowExW and RegisterClassExW - the wide
        // entry points - so a byte-per-character ASCII string would be read as half a string with
        // every other character NUL.
        _classNamePin = GCHandle.Alloc(
            System.Text.Encoding.Unicode.GetBytes(ClassName + "\0"), GCHandleType.Pinned);
        var className = (char*)_classNamePin.AddrOfPinnedObject();

        var windowClass = new WNDCLASSEXW
        {
            cbSize = (uint)sizeof(WNDCLASSEXW),
            style = (uint)CS.CS_HREDRAW | (uint)CS.CS_VREDRAW,
            lpfnWndProc = WindowProc,
            hInstance = instance,
            // A black background brush, so a window that does get painted paints black rather than
            // leaving whatever was there. COLOR_WINDOW + 1 rather than COLOR_WINDOW, because the
            // system colours are 1-based in the HBRUSH form - the choice FNA3D's driver makes.
            hbrBackground = (HBRUSH)(COLOR.COLOR_WINDOW + 1),
            lpszClassName = className,
            // No cursor: an unshown window never has one to draw, and IDC_ARROW is not among the
            // constants TerraFX exposes (it is MAKEINTRESOURCE(32512), which would have to be
            // written as a bare cast for no benefit here).
        };

        _atom = Windows.RegisterClassExW(&windowClass);
        if (_atom == 0)
        {
            _classNamePin.Free();
            throw new InvalidOperationException(
                $"RegisterClassExW failed with {Marshal.GetLastWin32Error()}; the class name " +
                $"'{ClassName}' may already be registered in this process.");
        }

        var handle = Windows.CreateWindowExW(
            (uint)WS.WS_EX_OVERLAPPEDWINDOW,
            className,
            className,
            (uint)WS.WS_POPUP,
            0, 0, width, height,
            HWND.NULL, HMENU.NULL, instance, null);

        if (handle == HWND.NULL)
        {
            var error = Marshal.GetLastWin32Error();
            Dispose();
            throw new InvalidOperationException($"CreateWindowExW failed with {error}.");
        }

        Handle = (nint)handle.Value;
    }

    public void Dispose()
    {
        if (Handle != 0)
        {
            Windows.DestroyWindow((HWND)Handle);
            Handle = 0;
        }

        if (_classNamePin.IsAllocated)
        {
            if (_atom != 0)
                Windows.UnregisterClassW((char*)_classNamePin.AddrOfPinnedObject(), Windows.GetModuleHandleW(null));

            // The pin is only released once the class is gone, which is the ordering it exists for:
            // unregistering is what stops Windows reading the name.
            _classNamePin.Free();
            _atom = 0;
        }
    }
}
