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

using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using NFMWorld.Shaders;
using NFMWorld.Shaders.Generated;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using AposColor = Maxine.Extensions.Mathematics.Color;

namespace NFMWorld.Graphics.D3D11.Smoke;

internal static unsafe class Program
{
    private const int Width = 256;
    private const int Height = 256;

    private static int _failures;

    private static int Main(string[] args)
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.WriteLine("SMOKE: skipped - this backend is Win32-only.");
            return 0;
        }


        using var window = SmokeWindow.Create(Width, Height);
        using var device = D3D11GraphicsDevice.Create(
            new D3D11DeviceDescription(
                WindowHandle: window.Handle,
                Width: Width,
                Height: Height,
                MultiSampleCount: 0,
                // Deliberately off. The cache exists to skip D3DCompile, and this test's job is to run it:
                // a hit would make the bundle checks below read a previous run's blob and report on the
                // compiler's output from some other day or source revision.
                EnableShaderCache: false
            )
        );

        if (args.Contains("--benchmark"))
        {
            BenchmarkUniformUploads(device);
            return _failures == 0 ? 0 : 1;
        }

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
        CheckMsdfText(device);
        CheckSubmissionCaching(device);

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
        Console.WriteLine(
            $"DEVICE: swapchain {swapchain.Width}x{swapchain.Height}, "
            + $"{swapchain.MultiSampleCount}x MSAA, tearing "
            + $"{(swapchain.AllowsTearing ? "allowed" : "not allowed")}"
        );
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
        byte[] source = [200, 200, 200, 40, 40, 40]; // row 0 bright, row 1 dark

        using var texture = device.CreateTexture(new TextureDesc(w, h, TextureFormat.R8), source);
        var readBack = new byte[source.Length];
        device.ReadTexture(texture, 0, 0, w, h, readBack);

        Report(
            "TEXTURE",
            "R8 upload + readback round-trip preserves row order (odd width: no row padding)",
            readBack.AsSpan().SequenceEqual(source)
        );

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

        Report(
            "TEXTURE",
            "a sub-rectangle update is refused rather than silently sheared "
            + "(UpdateSubresource cannot express a source row pitch)",
            refused
        );
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
            new BufferDesc(BufferKind.Vertex, BufferUsage.Dynamic, size),
            new byte[size]
        );

        var commandBuffer = device.AcquireCommandBuffer();

        var payload = new byte[16];
        for (var i = 0; i < payload.Length; i++)
            payload[i] = (byte)(i + 1);
        commandBuffer.UpdateBuffer(dynamic, payload, 16);
        device.Submit(commandBuffer);

        Report("BUFFER", "a dynamic buffer accepts a sub-range update", true);

        using var staticBuffer = device.CreateBuffer(
            new BufferDesc(BufferKind.Vertex, BufferUsage.Immutable, size),
            new byte[size]
        );

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

        // A frame that threw between acquire and submit abandons its buffer instead. Without that
        // one exception turns into a failed acquire every frame, and the loop reports the wrong fault.
        var abandonedRecovered = true;
        string? abandonedError = null;
        try
        {
            NFMWorld.Graphics.IGraphicsDevice abstraction = device;
            var abandoned = abstraction.AcquireCommandBuffer();
            abstraction.AbandonCommandBuffer(abandoned);
            abstraction.Submit(abstraction.AcquireCommandBuffer());
        }
        catch (InvalidOperationException ex)
        {
            abandonedRecovered = false;
            abandonedError = ex.Message;
        }

        Report(
            "LIFETIME",
            "the device is usable again after an abandoned command buffer"
            + (abandonedError is null ? "" : $": {abandonedError}"),
            abandonedRecovered
        );
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

        Report(
            "TARGET",
            $"a cleared off-screen target reads back its clear colour (got "
            + $"{pixel[0]},{pixel[1]},{pixel[2]},{pixel[3]}, want 255,0,255,255)",
            pixel[0] == 255 && pixel[1] == 0 && pixel[2] == 255 && pixel[3] == 255
        );

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

            using var pipeline = device.CreatePipeline(
                new PipelineDesc(
                    VertexShader: module,
                    PixelShader: module,
                    VertexLayouts: bundle.Layouts,
                    BlendState: BlendStateDesc.Opaque,
                    DepthStencilState: DepthStencilStateDesc.Default,
                    RasterizerState: RasterizerStateDesc.Default with { CullMode = CullMode.None },
                    Topology: PrimitiveTopology.TriangleList
                )
            );

            var elements = ((D3D11PipelineState)pipeline).Elements;

            Report(
                "BUNDLE",
                $"{bundle.Name}: input layout built against the shader's own signature " + $"({elements.Count} element(s))",
                elements.Count > 0
            );

            // Printed rather than only counted, because the element list *is* the semantic contract:
            // a wrong SemanticIndex or a wrong per-stream slot is invisible in a rendered frame and
            // obvious in this table. The GL smoke test prints the analogous attribute locations.
            Console.WriteLine(
                "           "
                + string.Join(
                    ", ",
                    elements.Select(e =>
                        $"{e.Semantic}{e.SemanticIndex}@{e.InputSlot}+{e.AlignedByteOffset}"
                        + (e.PerInstance ? $"/inst{e.InstanceStepRate}" : "")
                    )
                )
            );

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

        Report(
            "BUNDLE",
            $"{name}: compiled cbuffer size {compiled.Size} matches the reflection's {end}"
            + (padded != end ? $" (or its 16-byte-padded {padded})" : ""),
            compiled.Size == end || compiled.Size == padded
        );

        var mismatched = new List<string>();
        var missing = new List<string>();
        foreach (var uniform in reflection.Uniforms)
        {
            if (!compiled.Members.TryGetValue(uniform.Name, out var offset))
                missing.Add(uniform.Name);
            else if (offset != uniform.Offset)
                mismatched.Add($"{uniform.Name}: reflection {uniform.Offset}, compiled {offset}");
        }

        Report(
            "BUNDLE",
            $"{name}: all {reflection.Uniforms.Count} reflected uniform offset(s) match the " + "compiled cbuffer's packoffsets",
            mismatched.Count == 0 && missing.Count == 0
        );

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
        const int stride = 20; // float2 position, float2 uv, packed byte4 colour

        var bundle = ImGuiFullbright.Create();
        var module = D3D11GraphicsDevice.LoadShaderModule(device, bundle.Vertex, bundle.Pixel, bundle.Reflection);

        using var pipeline = device.CreatePipeline(
            new PipelineDesc(
                VertexShader: module,
                PixelShader: module,
                VertexLayouts: [ImGuiLayout],
                BlendState: BlendStateDesc.Opaque,
                DepthStencilState: DepthStencilStateDesc.None,
                RasterizerState: RasterizerStateDesc.Default with { CullMode = CullMode.None },
                Topology: PrimitiveTopology.TriangleList
            )
        );

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
            255, 0, 255, 255, 255, 255, 0, 255,
            255, 0, 255, 255, 255, 255, 0, 255,
        ];

        using var texture = device.CreateTexture(
            new TextureDesc(2, 2, TextureFormat.Rgba8),
            texels
        );
        using var sampler = device.CreateSampler(new SamplerDesc());
        using var target = device.CreateRenderTarget(new RenderTargetDesc(size, size, TextureFormat.Rgba8));

        using var vertexBuffer = device.CreateBuffer(
            new BufferDesc(BufferKind.Vertex, BufferUsage.Immutable, vertices.Length),
            vertices
        );

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
        Report(
            "DRAW",
            $"a textured quad samples its texture's two columns in order "
            + $"(left {left[0]},{left[1]},{left[2]}, right {right[0]},{right[1]},{right[2]})",
            left[0] == 255 && left[1] == 0 && left[2] == 255 && right[0] == 255 && right[1] == 255 && right[2] == 0
        );

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

        using var pipeline = device.CreatePipeline(
            new PipelineDesc(
                VertexShader: module,
                PixelShader: module,
                VertexLayouts: [ImGuiLayout],
                BlendState: BlendStateDesc.Opaque,
                DepthStencilState: DepthStencilStateDesc.None,
                RasterizerState: RasterizerStateDesc.Default with { CullMode = CullMode.None },
                Topology: PrimitiveTopology.TriangleList
            )
        );

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
            128, 128, 128, 255, 128, 128, 128, 255,
            128, 128, 128, 255, 128, 128, 128, 255,
        ];

        using var texture = device.CreateTexture(new TextureDesc(2, 2, TextureFormat.Rgba8), grey);
        using var sampler = device.CreateSampler(new SamplerDesc());
        using var target = device.CreateRenderTarget(new RenderTargetDesc(size, size, TextureFormat.Rgba8));
        using var vertexBuffer = device.CreateBuffer(
            new BufferDesc(BufferKind.Vertex, BufferUsage.Immutable, vertices.Length),
            vertices
        );

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

        Report(
            "DRAW",
            $"a +0.5 X translation moves the quad right rather than doubling it "
            + $"(left quarter {left[0]}, right quarter {right[0]} - expected 0 then 128)",
            left[0] == 0 && right[0] == 128
        );

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
        const int stride = 12; // float3 position

        var bundle = GroundFullbright.Create();
        var module = D3D11GraphicsDevice.LoadShaderModule(device, bundle.Vertex, bundle.Pixel, bundle.Reflection);

        // Depth writes off, which is the state that makes a depth clear a no-op if it is not worked
        // around. Culling off so the winding of a deliberately simple quad cannot matter.
        using var noDepthWrite = device.CreatePipeline(
            new PipelineDesc(
                VertexShader: module,
                PixelShader: module,
                VertexLayouts: [PositionColorLayout],
                BlendState: BlendStateDesc.Opaque,
                DepthStencilState: DepthStencilStateDesc.Default with { DepthWriteEnabled = false },
                RasterizerState: RasterizerStateDesc.Default with { CullMode = CullMode.None },
                Topology: PrimitiveTopology.TriangleList
            )
        );

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
            new BufferDesc(BufferKind.Vertex, BufferUsage.Immutable, vertices.Length),
            vertices
        );

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
        Report(
            "DEPTH",
            $"a depth clear through a depth-write-disabled pipeline still clears "
            + $"(centre pixel {pixel[0]},{pixel[1]},{pixel[2]},{pixel[3]})",
            pixel[3] == 255
        );

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
            Report(
                "PRESENT",
                $"a cleared frame reaches the swapchain (got "
                + $"{pixel[0]},{pixel[1]},{pixel[2]},{pixel[3]}, want 255,0,255,255)",
                pixel[0] == 255 && pixel[1] == 0 && pixel[2] == 255 && pixel[3] == 255
            );
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

        var nextFrame = device.AcquireCommandBuffer();
        ID3D11RenderTargetView* boundView = null;
        device.Context->OMGetRenderTargets(1, &boundView, null);
        var backBufferBound = boundView is not null && boundView == swapchain.ColorView;
        D3D11Interop.Release(ref boundView);
        device.Submit(nextFrame);
        Report("PRESENT", "The next frame starts with the swapchain's color view bound", backBufferBound);

        swapchain.Resize(Width, Height);
        Report(
            "PRESENT",
            $"Resize rebuilt the buffers at {swapchain.Width}x{swapchain.Height}",
            swapchain.Width == Width && swapchain.Height == Height
        );

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

        Report(
            "PRESENT",
            "Present succeeds again after a resize (the back buffer view was rebuilt)",
            presentedAgain
        );
    }

    private sealed class SubmissionFixture : IDisposable
    {
        private readonly D3D11ShaderModule[] _modules;
        internal readonly D3D11PipelineState[] Pipelines;
        internal readonly IBuffer Vertices;
        internal readonly ITexture TextureA;
        internal readonly ITexture TextureB;
        internal readonly ISampler Wrap;
        internal readonly ISampler Clamp;

        internal SubmissionFixture(D3D11GraphicsDevice device)
        {
            _modules = new D3D11ShaderModule[2];
            Pipelines = new D3D11PipelineState[2];
            for (var index = 0; index < 2; index++)
            {
                var block = "cbuffer Globals : register(b0) { float4 Tint : packoffset(c0); float4 Geometry : packoffset(c1);"
                            + (index == 0 ? " };" : " float4 Tail : packoffset(c4); };");
                var vertex = block + """
                                     struct Input { float2 Position : POSITION0; float2 UV : TEXCOORD0; };
                                     struct Output { float4 Position : SV_Position; float2 UV : TEXCOORD0; };
                                     Output main(Input input) {
                                         Output output;
                                         output.Position = float4(input.Position * Geometry.zw + Geometry.xy, 0, 1);
                                         output.UV = input.UV;
                                         return output;
                                     }
                                     """;
                var pixel = block + """
                                    Texture2D Image : register(t0);
                                    SamplerState ImageSampler : register(s0);
                                    float4 main(float4 position : SV_Position, float2 uv : TEXCOORD0) : SV_Target {
                                        return Image.Sample(ImageSampler, uv) * Tint
                                    """ + (index == 0 ? "; }" : " + Tail; }");
                var uniforms = new List<UniformParam>
                {
                    new("Tint", 0, 16, UniformType.Vector4),
                    new("Geometry", 16, 16, UniformType.Vector4),
                };
                if (index != 0)
                    uniforms.Add(new UniformParam("Tail", 64, 16, UniformType.Vector4));
                var reflection = new ShaderReflection
                {
                    Uniforms = uniforms,
                    Textures = [new ResourceBinding("Image", 0)],
                    Samplers = [new ResourceBinding("ImageSampler", 0)],
                };
                _modules[index] = (D3D11ShaderModule)D3D11GraphicsDevice.LoadShaderModule(
                    device,
                    new ShaderStageSources(default, vertex, "", "", "", ""),
                    new ShaderStageSources(default, pixel, "", "", "", ""),
                    reflection
                );
                Pipelines[index] = (D3D11PipelineState)device.CreatePipeline(new PipelineDesc(
                    _modules[index],
                    _modules[index],
                    [new VertexLayoutDesc([
                        new VertexAttributeDesc("POSITION", 0, 0, VertexAttributeFormat.Float2),
                        new VertexAttributeDesc("TEXCOORD", 0, 8, VertexAttributeFormat.Float2),
                    ], 16)],
                    BlendStateDesc.Opaque,
                    DepthStencilStateDesc.None,
                    new RasterizerStateDesc(CullMode.None),
                    PrimitiveTopology.TriangleList
                )
                );
            }

            Vector4[] vertices =
            [
                new(-1, -1, 1.25f, 0.5f), new(1, -1, 1.25f, 0.5f), new(1, 1, 1.25f, 0.5f),
                new(-1, -1, 1.25f, 0.5f), new(1, 1, 1.25f, 0.5f), new(-1, 1, 1.25f, 0.5f),
            ];
            Vertices = device.CreateBuffer(
                new BufferDesc(BufferKind.Vertex, BufferUsage.Immutable, 6 * 16),
                MemoryMarshal.AsBytes(vertices.AsSpan())
            );
            TextureA = device.CreateTexture(
                new TextureDesc(2, 1, TextureFormat.Rgba8),
                [255, 0, 0, 255, 0, 255, 0, 255]
            );
            TextureB = device.CreateTexture(
                new TextureDesc(2, 1, TextureFormat.Rgba8),
                [0, 0, 255, 255, 255, 255, 0, 255]
            );
            Wrap = device.CreateSampler(new SamplerDesc(TextureFilter.Point, TextureAddressMode.Wrap, TextureAddressMode.Wrap));
            Clamp = device.CreateSampler(new SamplerDesc(TextureFilter.Point, TextureAddressMode.Clamp, TextureAddressMode
                .Clamp
            )
            );
        }

        internal static void SetVector(ICommandBuffer cb, int offset, Vector4 value) =>
            cb.SetUniform(offset, MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(in value, 1)));

        internal void Bind(ICommandBuffer cb, int pipeline, ITexture? texture = null, ISampler? sampler = null)
        {
            cb.SetPipeline(Pipelines[pipeline]);
            cb.SetVertexBuffer(0, Vertices, 16);
            cb.SetShaderResource(0, texture ?? TextureA, sampler ?? Wrap);
            SetVector(cb, 0, Vector4.One);
            SetVector(cb, 16, new Vector4(0, 0, 1, 1));
        }

        public void Dispose()
        {
            foreach (var pipeline in Pipelines) pipeline.Dispose();
            foreach (var module in _modules) module.Dispose();
            Vertices.Dispose();
            TextureA.Dispose();
            TextureB.Dispose();
            Wrap.Dispose();
            Clamp.Dispose();
        }
    }

    private static void CheckSubmissionCaching(D3D11GraphicsDevice device)
    {
        using var fixture = new SubmissionFixture(device);
        using var target = device.CreateRenderTarget(new RenderTargetDesc(128, 16, TextureFormat.Rgba8));
        var cb = (D3D11CommandBuffer)device.AcquireCommandBuffer();
        var before = cb.UniformBufferCreationCount;
        for (var frame = 0; frame < 2; frame++)
        {
            cb.SetRenderTarget(target);
            cb.SetViewport(new Viewport(0, 0, 128, 16));
            cb.Clear(ClearOptions.Color, new ColorRgba(0, 0, 0, 0));
            for (var draw = 0; draw < 8; draw++)
            {
                cb.SetViewport(new Viewport(draw * 16, 0, 16, 16));
                var pipeline = draw is 1 or 3 or 4 or 6 ? 1 : 0;
                fixture.Bind(
                    cb,
                    pipeline,
                    draw is 3 or 4 or 5 ? fixture.TextureB : fixture.TextureA,
                    draw is 2 or 3 or 4 ? fixture.Clamp : fixture.Wrap
                );
                if (draw is 1 or 4)
                    SubmissionFixture.SetVector(cb, 0, new Vector4(0.5f));
                if (draw == 3)
                {
                    SubmissionFixture.SetVector(cb, 0, new Vector4(0.25f));
                    SubmissionFixture.SetVector(cb, 64, new Vector4(0, 0, 0.125f, 0));
                }

                if (draw == 5)
                    SubmissionFixture.SetVector(cb, 16, new Vector4(-0.5f, 0, 0.5f, 1));
                // Repeat native pointers, including after pipeline and block-size switches.
                cb.SetShaderResource(
                    0,
                    draw is 3 or 4 or 5 ? fixture.TextureB : fixture.TextureA,
                    draw is 2 or 3 or 4 ? fixture.Clamp : fixture.Wrap
                );
                cb.Draw(0, 2);
            }

            cb.SetRenderTarget(null);
            device.Submit(cb);

            byte[] pixels = new byte[128 * 16 * 4];
            device.ReadTexture(target.ColorTexture, 0, 0, 128, 16, pixels);
            (int R, int G, int B)[] expected =
                [(255, 0, 0), (128, 0, 0), (0, 255, 0), (64, 64, 32),
                 (128, 128, 32), (0, 0, 255), (255, 0, 0), (255, 0, 0)];
            for (var draw = 0; draw < expected.Length; draw++)
            {
                var at = (8 * 128 + draw * 16 + 4) * 4;
                var color = expected[draw];
                Report(
                    "REBIND",
                    $"frame {frame}, draw {draw}: pipeline/block/texture/sampler/uniform changes preserve pixels",
                    Math.Abs(pixels[at] - color.R) <= 1 && Math.Abs(pixels[at + 1] - color.G) <= 1
                    && Math.Abs(pixels[at + 2] - color.B) <= 1
                );
            }

            Report(
                "REBIND",
                "vertex-stage geometry uniform changes coverage independently of pixel-stage tint",
                pixels[(8 * 128 + 5 * 16 + 12) * 4 + 2] == 0
            );
            if (frame == 0)
            {
                Report(
                    "REBIND",
                    "warm-up creates at most one constant buffer per block size",
                    cb.UniformBufferCreationCount - before <= 2
                );
                before = cb.UniformBufferCreationCount;
                cb = (D3D11CommandBuffer)device.AcquireCommandBuffer();
            }
        }

        Report(
            "REBIND",
            "alternating warmed block sizes create no further constant buffers",
            cb.UniformBufferCreationCount == before
        );

        using var source = device.CreateRenderTarget(new RenderTargetDesc(16, 16, TextureFormat.Rgba8));
        cb = (D3D11CommandBuffer)device.AcquireCommandBuffer();
        cb.SetViewport(new Viewport(0, 0, 16, 16));
        cb.SetRenderTarget(source);
        cb.Clear(ClearOptions.Color, new ColorRgba(1, 0, 0, 1));
        cb.SetRenderTarget(target);
        fixture.Bind(cb, 0, source.ColorTexture);
        cb.Draw(0, 2);
        cb.SetRenderTarget(source);
        cb.Clear(ClearOptions.Color, new ColorRgba(0, 1, 0, 1));
        cb.SetRenderTarget(target);
        cb.SetShaderResource(0, source.ColorTexture, fixture.Wrap);
        cb.Draw(0, 2);
        cb.SetRenderTarget(null);
        device.Submit(cb);
        byte[] pixel = new byte[4];
        device.ReadTexture(target.ColorTexture, 8, 8, 1, 1, pixel);
        Report(
            "REBIND",
            "same-pointer binding restores SRVs invalidated by a render-target hazard",
            pixel[0] == 0 && pixel[1] == 255 && pixel[2] == 0
        );

        cb = (D3D11CommandBuffer)device.AcquireCommandBuffer();
        cb.SetRenderTarget(target);
        var temporaryTexture = device.CreateTexture(new TextureDesc(1, 1, TextureFormat.Rgba8), [0, 0, 255, 255]);
        var temporarySampler = device.CreateSampler(new SamplerDesc(TextureFilter.Point));
        fixture.Bind(cb, 0, temporaryTexture, temporarySampler);
        cb.SetShaderResource(0, temporaryTexture, temporarySampler);
        temporaryTexture.Dispose();
        temporarySampler.Dispose();
        cb.Draw(0, 2);
        cb.SetViewport(new Viewport(16, 0, 16, 16));
        cb.SetShaderResource(0, temporaryTexture, temporarySampler);
        cb.Draw(0, 2);
        cb.SetRenderTarget(null);
        device.Submit(cb);
        device.ReadTexture(target.ColorTexture, 8, 8, 1, 1, pixel);
        Report(
            "REBIND",
            "slot references keep disposed caller textures and samplers alive through the draw",
            pixel[0] == 0 && pixel[1] == 0 && pixel[2] == 255
        );
        device.ReadTexture(target.ColorTexture, 24, 8, 1, 1, pixel);
        Report(
            "REBIND",
            "rebinding disposed wrappers clears old native slot pointers rather than sampling stale resources",
            pixel[0] == 0 && pixel[1] == 0 && pixel[2] == 0
        );
    }

    private static void BenchmarkUniformUploads(D3D11GraphicsDevice device)
    {
        const int draws = 20_000;
        using var fixture = new SubmissionFixture(device);
        using var target = device.CreateRenderTarget(new RenderTargetDesc(1, 1, TextureFormat.Rgba8));
        var cb = (D3D11CommandBuffer)device.AcquireCommandBuffer();
        cb.SetRenderTarget(target);
        cb.SetViewport(new Viewport(0, 0, 1, 1));
        for (var pipeline = 0; pipeline < 2; pipeline++)
        {
            fixture.Bind(cb, pipeline);
            cb.Draw(0, 2);
        }

        // Both reference implementations live only here; native buffers are created before timing.
        var buffers = new ID3D11Buffer*[4];
        byte[][] payloads = [new byte[32], new byte[80]];
        for (var i = 0; i < 2; i++)
        {
            var geometry = new Vector4(0, 0, 1, 1);
            MemoryMarshal.Write(payloads[i].AsSpan(16), in geometry);
        }

        byte[] pixel = new byte[4];
        try
        {
            for (var i = 0; i < buffers.Length; i++)
            {
                var mapped = i < 2;
                var desc = new D3D11_BUFFER_DESC
                {
                    ByteWidth = (uint)payloads[i & 1].Length,
                    Usage = mapped ? D3D11_USAGE.D3D11_USAGE_DYNAMIC : D3D11_USAGE.D3D11_USAGE_DEFAULT,
                    BindFlags = D3D11Flags.BindConstantBuffer,
                    CPUAccessFlags = mapped ? D3D11Flags.CpuAccessWrite : 0,
                };
                ID3D11Buffer* buffer = null;
                D3D11Interop.Check(device.Device->CreateBuffer(&desc, null, &buffer), "CreateBuffer (benchmark)");
                buffers[i] = buffer;
            }

            Console.WriteLine($"BENCH: {draws} alternating 32/80-byte uploads + both-stage binds + 1px draws per sample");
            Console.WriteLine("BENCH: round 0 warms each path; rounds 1..4 reverse order to reduce ordering bias.");
            var binding = stackalloc ID3D11Buffer*[1];
            for (var round = 0; round < 5; round++)
            for (var order = 0; order < 3; order++)
            {
                var mode = (round & 1) == 0 ? order : 2 - order;
                var createdBefore = cb.UniformBufferCreationCount;
                cb.SetRenderTarget(target);
                var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                var start = Stopwatch.GetTimestamp();
                for (var draw = 0; draw < draws; draw++)
                {
                    var index = draw & 1;
                    var tint = new Vector4((draw & 2) == 0 ? 1 : 0.5f, 1, 1, 1);
                    if (mode == 0)
                    {
                        cb.SetPipeline(fixture.Pipelines[index]);
                        SubmissionFixture.SetVector(cb, 0, tint);
                        SubmissionFixture.SetVector(cb, 16, new Vector4(0, 0, 1, 1));
                        cb.Draw(0, 2);
                    }
                    else
                    {
                        fixture.Pipelines[index].Apply(device);
                        var payload = payloads[index];
                        MemoryMarshal.Write(payload.AsSpan(), in tint);
                        var buffer = buffers[index + (mode == 2 ? 2 : 0)];
                        if (mode == 1)
                        {
                            D3D11_MAPPED_SUBRESOURCE mapped;
                            D3D11Interop.Check(device.Context->Map(
                                (ID3D11Resource*)buffer,
                                0,
                                D3D11_MAP.D3D11_MAP_WRITE_DISCARD,
                                0,
                                &mapped
                            ), "Map (benchmark)"
                            );
                            payload.AsSpan().CopyTo(new Span<byte>(mapped.pData, payload.Length));
                            device.Context->Unmap((ID3D11Resource*)buffer, 0);
                        }
                        else
                        {
                            fixed (byte* data = payload)
                                device.Context->UpdateSubresource((ID3D11Resource*)buffer, 0, null, data, 0, 0);
                        }

                        binding[0] = buffer;
                        device.Context->VSSetConstantBuffers(0, 1, binding);
                        device.Context->PSSetConstantBuffers(0, 1, binding);
                        device.Context->Draw(6, 0);
                    }
                }

                var cpuEnd = Stopwatch.GetTimestamp();
                var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
                cb.SetRenderTarget(null);
                device.ReadTexture(target.ColorTexture, 0, 0, 1, 1, pixel);
                var completed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                var cpu = Stopwatch.GetElapsedTime(start, cpuEnd).TotalMilliseconds;
                var modeName = mode switch { 0 => "production-update", 1 => "native-map", _ => "native-update" };
                Console.WriteLine($"BENCH: round={round} mode={modeName} cpu_ms={cpu:F3} ns/draw={cpu * 1_000_000 / draws:F1}"
                                  + $" completed_ms={completed:F3} managed_bytes={allocated}"
                                  + $" production_buffer_creations={cb.UniformBufferCreationCount - createdBefore}"
                );
                Report(
                    "BENCH",
                    $"{modeName} final per-draw tint reaches the target",
                    Math.Abs(pixel[0] - 128) <= 1 && pixel[1] == 0 && pixel[2] == 0
                );
                if (mode == 0)
                    Report("BENCH", "warmed production sizes create no buffers", cb.UniformBufferCreationCount == createdBefore);
            }
        }
        finally
        {
            foreach (var buffer in buffers)
            {
                var owned = buffer;
                D3D11Interop.Release(ref owned);
            }

            cb.SetRenderTarget(null);
            device.Submit(cb);
        }
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
                device.Device->CreateTexture2D(&stagingDesc, null, &staging),
                "CreateTexture2D (back buffer staging)"
            );

            var box = new D3D11_BOX { left = 0, top = 0, front = 0, right = 1, bottom = 1, back = 1 };
            device.Context->CopySubresourceRegion(
                (ID3D11Resource*)staging,
                0,
                0,
                0,
                0,
                (ID3D11Resource*)source,
                0,
                &box
            );

            D3D11_MAPPED_SUBRESOURCE mapped;
            D3D11Interop.Check(
                device.Context->Map((ID3D11Resource*)staging, 0, D3D11_MAP.D3D11_MAP_READ, 0, &mapped),
                "Map (back buffer readback)"
            );

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
            $"No uniform named '{name}' in the reflection - names are "
            + $"[{string.Join(", ", pipeline.Reflection.Uniforms.Select(u => u.Name))}]."
        );
    }

    private static void WriteVertex(
        byte[] destination,
        int index,
        float x,
        float y,
        float u,
        float v,
        byte r,
        byte g,
        byte b,
        byte a
    )
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
            yield return new Bundle(
                "MountainsFullbright",
                mountains.Vertex,
                mountains.Pixel,
                mountains.Reflection,
                [PositionColorLayout]
            );

            var sky = SkyFullbright.Create();
            yield return new Bundle("SkyFullbright", sky.Vertex, sky.Pixel, sky.Reflection, [PositionColorLayout]);

            var poly = PolyBasic.Create();
            yield return new Bundle("PolyBasic", poly.Vertex, poly.Pixel, poly.Reflection, [PolyLayout, InstanceLayout]);

            var polyShadow = PolyCreateShadowMap.Create();
            yield return new Bundle(
                "PolyCreateShadowMap",
                polyShadow.Vertex,
                polyShadow.Pixel,
                polyShadow.Reflection,
                [PolyLayout, InstanceLayout]
            );

            var nvgSimple = NvgSimple.Create();
            yield return new Bundle("NvgSimple", nvgSimple.Vertex, nvgSimple.Pixel, nvgSimple.Reflection, [NvgLayout]);

            var particle = ParticleFullbright.Create();
            yield return new Bundle(
                "ParticleFullbright",
                particle.Vertex,
                particle.Pixel,
                particle.Reflection,
                [PositionColorLayout]
            );
        }
    }

    private sealed record Bundle(
        string Name,
        ShaderStageSources Vertex,
        ShaderStageSources Pixel,
        ShaderReflection Reflection,
        IReadOnlyList<VertexLayoutDesc> Layouts
    );

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
        StrideInBytes: ImGuiStride
    );

    /// <summary>Bytes per Ground/Mountains/Sky vertex - PositionColorVertex.Stride: float3 position, packed byte4 colour.</summary>
    private const int PositionColorStride = 16;

    private static VertexLayoutDesc PositionColorLayout => new(
        Attributes:
        [
            new VertexAttributeDesc("POSITION", 0, 0, VertexAttributeFormat.Float3),
            new VertexAttributeDesc("COLOR", 0, 12, VertexAttributeFormat.Byte4Normalized),
        ],
        StrideInBytes: PositionColorStride
    );

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
        StrideInBytes: LineGeometryStride
    );

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
        StrideInBytes: PolyStride
    );

    /// <summary>Bytes per NanoVG vertex - AbstractionNvgRenderer.VertexStride: float2 position, float2 uv.</summary>
    private const int NvgStride = 16;

    private static VertexLayoutDesc NvgLayout => new(
        Attributes:
        [
            new VertexAttributeDesc("POSITION", 0, 0, VertexAttributeFormat.Float2),
            new VertexAttributeDesc("TEXCOORD", 0, 8, VertexAttributeFormat.Float2),
        ],
        StrideInBytes: NvgStride
    );

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
        InstanceStepRate: 1
    );

    /// <summary>
    /// The game's debug counter, drawn exactly as <c>FPSCounter.Render</c> draws it: a black outlined
    /// pass first, then the same text in white over the top. Rendered with the real text renderer and
    /// the real shader, then read back.
    ///
    /// This is the only place the glyph path can be rendered at all. The GL smoke drives the same
    /// batch but runs headless ANGLE, which hands <c>ddx</c>/<c>ddy</c> back as zero; that collapses
    /// the AA band to a hard step, and a glyph's band is the whole subject here.
    /// </summary>
    private static void CheckMsdfText(D3D11GraphicsDevice device)
    {
        const int size = 320;
        string fontPath = Path.Combine(AppContext.BaseDirectory, "NotoSans-Regular.ttf");
        if (!File.Exists(fontPath))
        {
            Report("MSDF", $"font present at {fontPath}", false);
            return;
        }

        var font = new Apos.Shapes.ShapeFont(File.ReadAllBytes(fontPath));
        var batch = new Apos.Shapes.ShapeBatch(device, warmup: false);
        using var target = device.CreateRenderTarget(new RenderTargetDesc(size, size, TextureFormat.Rgba8));
        // The game draws with 8x MSAA by default, and a multisampled target shades a pixel whose
        // centre is outside a quad but some of whose samples are inside it - at the centre, with
        // the quad's varyings extrapolated past its edge. That is resolved into the plain target to
        // be read back.
        using var multisampled = new D3D11RenderTarget(device, new RenderTargetDesc(size, size, TextureFormat.Rgba8), 8);

        // The counter's own font and size, and the same lines it draws.
        const float fontSize = 16f;
        string text = "Fps: 240.11\nElapsed time: 0.05\nTick: 1200us\nGTC: 0.30us\nFrame (CPU): 4ms\n"
                      + "Updates: 13\nFrames: 13\nLua calls: 0 (0.00ms)\nTables: 0, str-ins: 0 (0.00ms), resize: 0";

        string traced = text;

        // The batch's default projection is built from the swapchain, not this off-screen target, so
        // the target's own size is passed: world units to pixels at one to one, origin top left.
        var projection = Matrix4x4.CreateOrthographicOffCenter(0, size, size, 0, 0, 1);

        // Mid grey so the background is a value of its own, and the outline a third one.
        var background = new ColorRgba(0.5f, 0.5f, 0.5f, 1f);

        byte[] Render(
            bool outlined,
            float emSize,
            bool fill = true,
            float penX = 10,
            float aaSize = 1.5f,
            float outlineWidth = 1f,
            bool msaa = false,
            ColorRgba? back = null
        )
        {
            var pen = new Vector2(penX, 3.208f);
            var cb = device.AcquireCommandBuffer();
            cb.SetRenderTarget(msaa ? multisampled : target);
            cb.SetViewport(new Viewport(0, 0, size, size));
            cb.Clear(ClearOptions.Color, back ?? background);
            batch.Begin(cb, null, projection);
            var white = new Apos.Shapes.Gradient(
                Vector2.Zero,
                new AposColor(255, 255, 255),
                Vector2.One,
                new AposColor(255, 255, 255)
            );
            var black = new Apos.Shapes.Gradient(Vector2.Zero, new AposColor(0, 0, 0), Vector2.One, new AposColor(0, 0, 0));
            // One draw carrying the fill and the band together, which is what DrawStringOutlined
            // does. The fill-only arm is the plain DrawString the ink bound is checked against.
            if (outlined)
            {
                batch.DrawString(font, traced, pen, emSize, white, black, outlineWidth, aaSize: aaSize);
            }
            else if (fill)
            {
                batch.DrawString(font, traced, pen, emSize, white, aaSize: aaSize);
            }

            batch.End();
            cb.SetRenderTarget(null);
            device.Submit(cb);

            if (msaa)
            {
                unsafe
                {
                    device.Context->ResolveSubresource(
                        (ID3D11Resource*)((D3D11Texture)target.ColorTexture!).Texture,
                        0,
                        (ID3D11Resource*)multisampled.ColorTexture.Texture,
                        0,
                        DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UNORM
                    );
                }
            }

            var pixels = new byte[size * size * 4];
            device.ReadTexture(target.ColorTexture!, 0, 0, size, size, pixels);
            return pixels;
        }

        // A glyph's AA band is centred on the edge whatever the batch's style, so the letters come
        // out at the weight the font drew them. Asking for the band outward instead - which is what
        // every other shape wants, and what the batch's default style says - puts the whole fade
        // outside the glyph: the letter fills out to about 2.4x the ink at every size, and under the
        // counter's black pass it goes nearly solid black. Pixels are the only place that shows, so
        // the check is a bound on how much ink the counter's own text may lay down.
        //
        // The same decision is asserted at the vertex in SlugSmoke (a CPU suite, and the only place
        // it can be read directly). This is the rendered counterpart, and it lives here rather than
        // in the GL smoke because that one runs headless ANGLE, where ddx/ddy come back zero and
        // every fade collapses to a hard step - a glyph is byte-identical there whatever the band.
        int plainInk = Ink(Render(false, fontSize));
        Report("MSDF", $"the counter's text drew something ({plainInk} lit pixels)", plainInk > 800);
        Report(
            "MSDF",
            $"the counter's own text keeps its weight ({plainInk} lit pixels, under {InkBound})",
            plainInk < InkBound
        );

        if (plainInk >= InkBound)
        {
            foreach (float sz in new[] { fontSize, 12.5f, 24.25f })
                Console.WriteLine($"MSDF: at size {sz:0.###} the counter's text lights {Ink(Render(false, sz))} pixels");
        }

        Func<bool, float, bool, float, float, float, byte[]> draw =
            (outlined, em, fill, penX, aa, width) => Render(outlined, em, fill, penX, aa, width);
        // At 48px, where a stem is wide enough to have a body at all. At the counter's own 16 the
        // letter is barely wider than the band, so every pixel of it is inside one fade or other.
        CheckLetterKeepsItsWeight(draw, 48f, size);
        CheckBandIsBounded(draw, fontSize, size);

        var fillOnly = Render(false, fontSize, true, 10f, 1.5f, 0f);
        // A glyph's edge fades over one pixel under the fill's own sqrt profile, which is what the
        // old text used. Averaged over a whole edge the mean partial-coverage pixel is 2/3 of white;
        // a smoothstep over a band instead is antisymmetric about its centre pixel, so its mean is
        // exactly 1/2 - the same letter drawn lighter and fading away slower on both sides.
        CheckFillEdgeProfile(Render(false, 48f, back: new ColorRgba(0f, 0f, 0f, 1f)), size);
        var outlined = Render(true, fontSize, true, 10f, 1.5f, 1f);
        // The outline must not leave ink standing away from the glyph. A 1px band with its fade
        // reaches at most two pixels past the letter's own ink, so a dark pixel with no ink of the
        // fill pass within two pixels of it is a stray - the specks the median's false near-edge
        // readings paint outside the letter.
        foreach (bool msaa in new[] { false, true })
        {
            var inkOnly = msaa ? Render(false, fontSize, true, 10f, 1.5f, 0f, msaa: true) : fillOnly;
            var ringed = msaa ? Render(true, fontSize, true, 10f, 1.5f, 1f, msaa: true) : outlined;
            int strays = 0;
            var strayAt = new List<(int X, int Y)>();
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (Value(ringed, size, x, y) >= 110) continue;
                    bool nearInk = false;
                    for (int dy = -2; dy <= 2 && !nearInk; dy++)
                    for (int dx = -2; dx <= 2 && !nearInk; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= size || ny >= size) continue;
                        if (Value(inkOnly, size, nx, ny) > 140) nearInk = true;
                    }

                    if (!nearInk)
                    {
                        strays++;
                        if (strayAt.Count < 8) strayAt.Add((x, y));
                    }
                }
            }

            Report(
                "MSDF",
                $"the outline leaves no ink standing away from the glyph{(msaa ? " under 8x MSAA" : "")} "
                + $"({strays} stray pixels{(strays > 0 ? " at " + string.Join(" ", strayAt) : "")})",
                strays == 0
            );
        }

        var blue = new ColorRgba(0f, 0f, 1f, 1f);
        CheckOutlineCoverage(penX => Render(true, fontSize, true, penX, 1.5f, 1f, back: blue), size);

        // The letter's edge keeps its antialiasing over the band. Where the fill alone half covers
        // a pixel, the outlined pass is the letter over black there, so the same coverage of white:
        // a step between the band and the letter turns those pixels solid black or solid white
        // instead, which is an aliased inner edge on every letter.
        int edges = 0, stepped = 0;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int alone = Value(fillOnly, size, x, y);
                if (alone < 159 || alone > 223) continue;
                edges++;
                float coverage = (alone - 127) / 128f;
                if (Math.Abs(Value(outlined, size, x, y) - coverage * 255f) > 30f) stepped++;
            }
        }

        Report(
            "MSDF",
            $"the letter's edge stays antialiased against its outline "
            + $"({stepped} of {edges} half-covered edge pixels off the letter-over-band value)",
            edges > 0 && stepped * 10 <= edges
        );

        // The outline is a stroke of the width the caller named, and the game's is one pixel. It
        // must lay that pixel down as ink: faded by the letter's own coverage, which is already
        // spent where the band starts, the stroke comes out as a translucent smear the background
        // reads through. The fill is white and the band is black, so the darkest pixel the frame
        // holds is the band's, and it only reaches black if the band is drawn at full weight.
        int darkest = 255;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
            darkest = Math.Min(darkest, Value(outlined, size, x, y));
        Report(
            "MSDF",
            $"the counter's outline is drawn opaquely, not as a translucent smear " + $"(darkest pixel {darkest})",
            darkest < 40
        );
    }

    /// <summary>
    /// The counter's outline must reach the letter it belongs to, with no seam between them: the
    /// background must not show through between the band and the letter's edge, which reads as a
    /// letter standing clear of its own outline.
    ///
    /// Drawn over a blue background, because on a grey one the fault cannot be told from correct
    /// output: the letter's antialiased edge over a black band is a mix of black and white, which is
    /// the same grey. Any mix of black and white keeps its red and blue equal, so blue in excess of
    /// red is background and nothing else.
    ///
    /// The pen is swept across a pixel because the fault only bites at some subpixel phases; a
    /// whole-pixel pen hides it entirely.
    /// </summary>
    /// <summary>
    /// The fill's edge fades the way the font's own coverage did, and not as a smoothstep. Slug solved
    /// a crossing count, which is a one-pixel ramp, then took its square root: at the edge that is
    /// sqrt(0.5) = 0.707, and the ramp's own mean over the pixels it touches is 2/3. A smoothstep over
    /// a band is 0.5 at its centre and antisymmetric about it, so the mean of the pixels it fades is
    /// exactly 1/2. Averaging every partial pixel of a stem's straight edges separates the two.
    ///
    /// Drawn at 48px, where a stem is wide enough that its edge pixels and its body are distinct.
    /// </summary>
    private static void CheckFillEdgeProfile(byte[] pixels, int size)
    {
        int partial = 0;
        long sum = 0;
        for (int y = 1; y < size - 1; y++)
        {
            for (int x = 1; x < size - 1; x++)
            {
                int v = Value(pixels, size, x, y);
                if (v <= 8 || v >= 247) continue;
                // Only pixels with an edge on both sides, which is a fade pixel and not a corner's
                // two-dimensional smear: the neighbours across it are solid fill and background.
                int l = Value(pixels, size, x - 1, y), r = Value(pixels, size, x + 1, y);
                int u = Value(pixels, size, x, y - 1), d = Value(pixels, size, x, y + 1);
                bool acrossY = (l >= 247 && r <= 8) || (l <= 8 && r >= 247);
                bool acrossX = (u >= 247 && d <= 8) || (u <= 8 && d >= 247);
                if (!acrossY && !acrossX) continue;
                partial ++;
                sum += v;
            }
        }

        double mean = partial > 0 ? (double)sum / partial : 0;
        // A letter's body is not merely near-white: the fill is solid inside it, and anything less
        // reads as a translucent letter however sharp its edge is.
        int solid = 0, deepest = 0;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int v = Value(pixels, size, x, y);
            if (v >= 250) solid++;
            deepest = Math.Max(deepest, v);
        }

        Console.WriteLine($"           {partial} edge pixels average {mean:0.0} of 255, {solid} solid, peak {deepest}");
        Report(
            "MSDF",
            $"a glyph's edge fades under the fill's own profile, not a smoothstep "
            + $"({partial} edge pixels average {mean:0.0}, not 128) "
            + $"and its body is solid ({solid} pixels, peak {deepest})",
            partial > 40 && Math.Abs(mean - 128) > 12 && solid > 1000 && deepest == 255
        );
    }

    private static void CheckOutlineCoverage(Func<float, byte[]> renderOnBlue, int size)
    {
        int gaps = 0;
        var first = new List<(int X, int Y)>();
        for (int phase = 0; phase < 4; phase++)
        {
            var found = Gaps(renderOnBlue(10f + phase / 4f), size);
            gaps += found.Count;
            first.AddRange(found.Take(4 - Math.Min(4, first.Count)));
        }

        Report(
            "MSDF",
            $"the counter's outline meets its fill with no seam between them "
            + $"({gaps} seam pixels over four subpixel pens{(gaps > 0 ? ", at " + string.Join(" ", first) : "")})",
            gaps == 0
        );
    }

    /// <summary>
    /// Background showing through with the band on one side and the letter on the other, along a
    /// row or a column. The band's own outer edge cannot match, since the side away from the letter
    /// there is background rather than the fill.
    /// </summary>
    private static List<(int X, int Y)> Gaps(byte[] pixels, int size)
    {
        int Red(int x, int y) => pixels[(y * size + x) * 4];
        int Background(int x, int y) => pixels[(y * size + x) * 4 + 2] - pixels[(y * size + x) * 4];
        bool Band(int x, int y) => Red(x, y) <= 60 && Background(x, y) < 40;
        bool Letter(int x, int y) => Red(x, y) >= 200;

        var found = new List<(int, int)>();
        for (int y = 1; y < size - 1; y++)
        {
            for (int x = 1; x < size - 1; x++)
            {
                if (Background(x, y) < 77) continue;
                if ((Band(x - 1, y) && Letter(x + 1, y))
                    || (Letter(x - 1, y) && Band(x + 1, y))
                    || (Band(x, y - 1) && Letter(x, y + 1))
                    || (Letter(x, y - 1) && Band(x, y + 1)))
                    found.Add((x, y));
            }
        }

        return found;
    }

    /// <summary>
    /// The band must have an outside. A text outline is a band around the letter, so past its width
    /// there is background again; a composite that never bounds it covers the whole quad, which on
    /// the counter's black outline is the letter's box going solid black.
    ///
    /// The quad is padded to hold the band and its fade, so the far corner is always outside the
    /// band however narrow it is drawn.
    /// </summary>
    private static void CheckBandIsBounded(
        Func<bool, float, bool, float, float, float, byte[]> render,
        float fontSize,
        int size
    )
    {
        var pixels = render(true, fontSize, true, 9f, 1.5f, 1f);

        // The outermost ink the frame holds, and the box the band could reach at most.
        int minX = size, minY = size, maxX = -1, maxY = -1;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                if (Math.Abs(Value(pixels, size, x, y) - 128) <= 24) continue;
                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
            }
        }

        // The pen is at (9,12) and the first line is 16px tall, so nothing the band draws can reach
        // the left column or the bottom of the frame.
        bool leftClear = true, bottomClear = true;
        for (int y = 0; y < size; y++)
            if (Value(pixels, size, 0, y) < 100)
                leftClear = false;
        for (int x = 0; x < size; x++)
            if (Value(pixels, size, x, size - 1) < 100)
                bottomClear = false;

        Report(
            "MSDF",
            "the outline band stops at its own width rather than covering the whole quad "
            + $"(left column and bottom row stay background)",
            leftClear && bottomClear
        );
        Console.WriteLine($"           ink spans x {minX}..{maxX}, y {minY}..{maxY} of {size}");
    }

    /// <summary>
    /// An outline may not thin the letter it rings. A text outline is a band OUTSIDE the glyph, so
    /// the letter's own body must keep exactly the coverage the fill alone drew it at; a band cut
    /// from the glyph's edge inward takes the whole of its inner side out of the letter instead.
    ///
    /// Only the body is compared, since nearer the edge the ring is meant to stand in for what was
    /// the letter's own fade. Where the letter is wide enough to have a body at all is the same
    /// question the ink bound already asks, so this is run at a size it holds at: at the counter's
    /// 16px a stem is about one pixel, and every pixel of it is inside one fade or the other.
    ///
    /// The comparison is per phase against the SAME pen, because a half-pixel shift moves a stem's
    /// own coverage far more than the band does and would read as the band thinning every letter.
    /// </summary>
    private static void CheckLetterKeepsItsWeight(
        Func<bool, float, bool, float, float, float, byte[]> render,
        float fontSize,
        int size
    )
    {
        int worst = 0, worstAt = -1, over = 0, body = 0;
        for (int phase = 0; phase < 4; phase++)
        {
            var mine = render(false, fontSize, true, 9f + phase / 4f, 1.5f, 0f);
            var drawn = render(true, fontSize, true, 9f + phase / 4f, 1.5f, 1f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int alone = Value(mine, size, x, y);
                    if (alone < 245 || NearFade(mine, size, x, y)) continue;
                    body++;
                    int drop = alone - Value(drawn, size, x, y);
                    if (drop > 20) over++;
                    if (drop > worst)
                    {
                        worst = drop;
                        worstAt = y * size + x;
                    }
                }
            }
        }

        Console.WriteLine(
            $"           {over} of {body} body pixels drop past 20, worst {worst} "
            + $"at ({worstAt % size},{worstAt / size}) over four subpixel pens"
        );
        Report(
            "MSDF",
            $"an outline leaves the letter's body as the fill drew it " + $"({over} of {body} move by more than 20)",
            over == 0 && body > 0
        );
    }

    /// <summary>
    /// A pixel with a fade within one of it. The pen sweeps a quarter pixel between phases, and a
    /// fade is a pixel and a half wide, so a pixel with nothing but solid fill in its own 3x3 keeps
    /// its value through the whole sweep whichever way the edge there moves. A letter drawn with a
    /// band of any width still has a core this wide - that is what "solid" means - so the guard is
    /// silent only when the band has eaten into the letter past its own edge.
    /// </summary>
    private static bool NearFade(byte[] pixels, int size, int x, int y)
    {
        for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
        {
            int nx = x + dx, ny = y + dy;
            if (nx < 0 || ny < 0 || nx >= size || ny >= size) return true;
            if (Value(pixels, size, nx, ny) < 245) return true;
        }

        return false;
    }

    private static int Value(byte[] pixels, int size, int x, int y) =>
        pixels[(y * size + x) * 4];

    // The counter's nine lines at 16px come to 4504 lit pixels with a centred band and 7251 with an
    // outward one. The bound sits between the two, well clear of either.
    private const int InkBound = 5800;

    /// <summary>How many pixels carry ink.</summary>
    private static int Ink(byte[] pixels)
    {
        int lit = 0;
        for (int i = 0; i < pixels.Length; i += 4)
            if (Math.Max(pixels[i], Math.Max(pixels[i + 1], pixels[i + 2])) > 200)
                lit++;
        return lit;
    }
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
            System.Text.Encoding.Unicode.GetBytes(ClassName + "\0"),
            GCHandleType.Pinned
        );
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
                $"RegisterClassExW failed with {Marshal.GetLastWin32Error()}; the class name "
                + $"'{ClassName}' may already be registered in this process."
            );
        }

        var handle = Windows.CreateWindowExW(
            (uint)WS.WS_EX_OVERLAPPEDWINDOW,
            className,
            className,
            (uint)WS.WS_POPUP,
            0,
            0,
            width,
            height,
            HWND.NULL,
            HMENU.NULL,
            instance,
            null
        );

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
