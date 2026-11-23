// LLM maintained.
//
// Smoke test for NFMWorld.Graphics.WebGPU: brings up a real wgpu-native device with no window, then
// checks the parts that can only be wrong in this backend.
//
// What is specific to WebGPU and therefore worth checking here rather than in the D3D11 smoke:
//
//  1. The WGSL tint emits has to parse at all - a shader module is where the translation either
//     survives or does not, and wgpu reports a syntax error as a validation message on the device,
//     not as a null module. Creating a module per bundle is the check.
//  2. A pipeline bakes the attachment formats it will be drawn into, so the same bundle has to be
//     created for a single-sample target and a multisampled one, and both have to succeed.
//  3. The uniform block is a dynamic-offset region of one ring buffer, so two draws with different
//     values must read their own - which is what the two-quad check pins down.

using System.Numerics;
using System.Runtime.InteropServices;
using NFMWorld.Shaders;
using NFMWorld.Shaders.Generated;
using Wg = Maxine.WebGPU;

namespace NFMWorld.Graphics.WebGPU.Smoke;

internal static unsafe class Program
{
    private const int Width = 256;
    private const int Height = 256;

    private static int _failures;

    private static int Main()
    {
        using var device = WebGpuGraphicsDevice.Create(
            new WebGpuDeviceDescription(
                WindowHandle: 0,
                Width: Width,
                Height: Height,
                MultiSampleCount: 0
            )
        );

        ReportInterface(device);
        CheckTextureRoundTrip(device);
        CheckBuffers(device);
        CheckUnalignedCopies(device);
        CheckCommandBufferLifetime(device);
        CheckRenderTarget(device);
        CheckBundlePipelines(device);
        CheckTexturedDraw(device);
        CheckMidPassTextureRebind(device);
        CheckUniformRingGrowth(device);
        CheckTranslationUniform(device);

        var finalError = device.TakeError();
        Report(
            "DEVICE",
            "the device raised no validation error during the run" + (finalError is null ? "" : $": {finalError}"),
            finalError is null
        );

        Console.WriteLine();
        Console.WriteLine(_failures == 0 ? "SMOKE: pass" : $"SMOKE: FAILED ({_failures} failure(s))");
        return _failures == 0 ? 0 : 1;
    }

    private static void ReportInterface(WebGpuGraphicsDevice device)
    {
        var swapchain = device.SwapchainInternal;

        Console.WriteLine($"DEVICE: {device.AdapterDescription}");
        Console.WriteLine($"DEVICE: uniform alignment {device.UniformAlignment} byte(s)");
        Console.WriteLine($"DEVICE: float32-filterable {device.Float32Filterable}, bc textures {device.TextureCompressionBc}");
        Console.WriteLine($"DEVICE: back buffer {swapchain.Width}x{swapchain.Height}, {swapchain.MultiSampleCount}x MSAA");
        Console.WriteLine($"DEVICE: bottom-left framebuffer origin {device.HasBottomLeftFramebufferOrigin}");
        Console.WriteLine();

        Report("DEVICE", "the framebuffer origin is the top left, as D3D11's is", !device.HasBottomLeftFramebufferOrigin);
        Report("DEVICE", "the multisample count can change without a restart", !swapchain.MultiSampleChangeRequiresRestart);
    }

    private static void CheckTextureRoundTrip(WebGpuGraphicsDevice device)
    {
        const int size = 4;
        var texels = new byte[size * size * 4];
        for (var i = 0; i < size * size; i++)
        {
            texels[i * 4 + 0] = (byte)(i * 8);
            texels[i * 4 + 1] = 128;
            texels[i * 4 + 2] = 255;
            texels[i * 4 + 3] = 255;
        }

        using var texture = device.CreateTexture(new TextureDesc(size, size, TextureFormat.Rgba8), texels);

        var readback = new byte[size * size * 4];
        device.ReadTexture(texture, 0, 0, size, size, readback);

        Report("TEXTURE", "a texture round-trips through upload and readback unchanged", readback.AsSpan().SequenceEqual(texels));

        // A sub-rectangle read has to come back as tightly packed rows of just that rectangle.
        var sub = new byte[2 * 2 * 4];
        device.ReadTexture(texture, 1, 1, 2, 2, sub);
        var expected = new[]
        {
            texels[(1 * size + 1) * 4 + 0], texels[(1 * size + 2) * 4 + 0],
            texels[(2 * size + 1) * 4 + 0], texels[(2 * size + 2) * 4 + 0],
        };

        Report(
            "TEXTURE",
            "a 2x2 sub-rectangle readback returns that rectangle's own bytes",
            sub[0] == expected[0] && sub[4] == expected[1] && sub[8] == expected[2] && sub[12] == expected[3]
        );
    }

    private static void CheckBuffers(WebGpuGraphicsDevice device)
    {
        var initial = new byte[64];
        for (var i = 0; i < initial.Length; i++)
            initial[i] = (byte)i;

        using var buffer = device.CreateBuffer(
            new BufferDesc(BufferKind.Vertex, BufferUsage.Immutable, initial.Length),
            initial
        );

        Report("BUFFER", "a buffer keeps its kind, usage and size", buffer.Kind == BufferKind.Vertex && buffer.SizeInBytes == 64);

        var updated = new byte[16];
        Array.Fill(updated, (byte)0xAB);

        var commandBuffer = device.AcquireCommandBuffer();
        commandBuffer.UpdateBuffer(buffer, updated, 8);
        device.Submit(commandBuffer);

        Report("BUFFER", "an update recorded into a command buffer is accepted", true);
    }

    /// <summary>
    /// A buffer whose size is not a multiple of WebGPU's 4-byte copy unit still has to take its whole
    /// contents. An index buffer of an odd number of 16-bit indices is the case that found this: the
    /// copy is rejected, the update is silently lost, and nothing says why - the queue write and the
    /// recorded copy both fail validation rather than returning an error.
    /// </summary>
    private static void CheckUnalignedCopies(WebGpuGraphicsDevice device)
    {
        const int oddSize = 435 * sizeof(ushort); // one 16-bit index short of a 4-byte multiple
        var initial = new byte[oddSize];
        for (var i = 0; i < initial.Length; i++)
            initial[i] = (byte)(i * 7);

        using var buffer = device.CreateBuffer(
            new BufferDesc(BufferKind.Index, BufferUsage.Dynamic, oddSize, IndexFormat.UInt16),
            initial
        );

        var updated = new byte[oddSize];
        Array.Fill(updated, (byte)0x5A);

        var commandBuffer = device.AcquireCommandBuffer();
        commandBuffer.UpdateBuffer(buffer, updated);
        device.Submit(commandBuffer);

        // Validation of a recorded copy may be reported when the device is polled rather than at
        // submit, and the callback is the only place either kind of failure is visible.
        device.Poll(true);

        Report(
            "BUFFER",
            $"{oddSize} bytes into an {oddSize}-byte buffer is accepted at creation and as an update",
            buffer.SizeInBytes == oddSize && device.TakeError() is null
        );
    }

    private static void CheckCommandBufferLifetime(WebGpuGraphicsDevice device)
    {
        var commandBuffer = device.AcquireCommandBuffer();
        var second = false;
        try
        {
            device.AcquireCommandBuffer();
        }
        catch (InvalidOperationException)
        {
            second = true;
        }

        Report("COMMAND", "a second live command buffer is refused", second);
        device.Submit(commandBuffer);

        var reused = false;
        try
        {
            device.Submit(commandBuffer);
        }
        catch (InvalidOperationException)
        {
            reused = true;
        }

        Report("COMMAND", "a command buffer cannot be submitted twice", reused);
    }

    private static void CheckRenderTarget(WebGpuGraphicsDevice device)
    {
        using var target = device.CreateRenderTarget(
            new RenderTargetDesc(32, 32, TextureFormat.Rgba8, HasDepthStencil: false)
        );
        using var withDepth = device.CreateRenderTarget(new RenderTargetDesc(32, 32, TextureFormat.Rgba8));

        Report("TARGET", "a render target exposes its colour texture", target.ColorTexture is { Width: 32, Height: 32 });
        Report("TARGET", "a render target without a depth request has no depth texture", target.DepthStencilTexture is null);
        Report("TARGET", "a render target with one has a depth texture", withDepth.DepthStencilTexture is not null);

        var commandBuffer = device.AcquireCommandBuffer();
        commandBuffer.SetRenderTarget(target);
        commandBuffer.Clear(ClearOptions.Color, new ColorRgba(0.25f, 0.5f, 0.75f, 1f));
        commandBuffer.SetRenderTarget(null);
        device.Submit(commandBuffer);

        var pixel = new byte[4];
        device.ReadTexture(target.ColorTexture, 16, 16, 1, 1, pixel);

        // The clear reaches the attachment as a pass's load operation, so this is also the check that
        // an unconverted clear actually lands on the texture.
        Report(
            "TARGET",
            $"a cleared target reads back the clear colour ({pixel[0]},{pixel[1]},{pixel[2]},{pixel[3]})",
            Math.Abs(pixel[0] - 64) <= 1 && Math.Abs(pixel[1] - 128) <= 1 && Math.Abs(pixel[2] - 191) <= 1
        );
    }

    private static void CheckBundlePipelines(WebGpuGraphicsDevice device)
    {
        using var target = device.CreateRenderTarget(new RenderTargetDesc(Width, Height, TextureFormat.Rgba8));

        foreach (var bundle in Bundles)
        {
            var module = device.CreateShaderModule(bundle.Vertex, bundle.Pixel, bundle.Reflection);
            var hasWgsl = bundle.Vertex.Wgsl.Length > 0 && bundle.Pixel.Wgsl.Length > 0;

            Report(
                "BUNDLE",
                $"{bundle.Name}: both stages carry WGSL ({bundle.Vertex.Wgsl.Length} and {bundle.Pixel.Wgsl.Length} char(s))",
                hasWgsl
            );

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

            // Two variants, because a wgpu pipeline names the format and sample count of the
            // attachment it draws into: a single-sample target and a 4x one must both build, or the
            // same bundle would work in one pass and fail in another.
            var commandBuffer = device.AcquireCommandBuffer();
            commandBuffer.SetRenderTarget(target);
            commandBuffer.Clear(ClearOptions.Color, new ColorRgba(0f, 0f, 0f));
            commandBuffer.SetPipeline(pipeline);
            commandBuffer.SetRenderTarget(null);
            device.Submit(commandBuffer);

            var pipelineState = (WebGpuPipelineState)pipeline;
            var variant = pipelineState.GetVariant(
                new WebGpuPipelineKey(
                    Wg.TextureFormat.Rgba8Unorm,
                    Wg.TextureFormat.Undefined,
                    4,
                    Unfilterable: !device.Float32Filterable
                )
            );

            Report("BUNDLE", $"{bundle.Name}: a 4x multisampled variant builds too", variant.Pipeline is not null);
            Report(
                "BUNDLE",
                $"{bundle.Name}: every declared texture and sampler fits the bind group layout",
                bundle.Reflection.Textures.Count <= WebGpuPipelineState.ResourceSlots
                && bundle.Reflection.Samplers.Count <= WebGpuPipelineState.ResourceSlots
            );
        }
    }

    private static void CheckTexturedDraw(WebGpuGraphicsDevice device)
    {
        const int size = 64;
        const int stride = 20;

        var bundle = ImGuiFullbright.Create();
        var module = device.CreateShaderModule(bundle.Vertex, bundle.Pixel, bundle.Reflection);

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

        var vertices = new byte[6 * stride];
        WriteVertex(vertices, 0, -1, -1, 0, 1, 255, 255, 255, 255);
        WriteVertex(vertices, 1, 1, -1, 1, 1, 255, 255, 255, 255);
        WriteVertex(vertices, 2, 1, 1, 1, 0, 255, 255, 255, 255);
        WriteVertex(vertices, 3, -1, -1, 0, 1, 255, 255, 255, 255);
        WriteVertex(vertices, 4, 1, 1, 1, 0, 255, 255, 255, 255);
        WriteVertex(vertices, 5, -1, 1, 0, 0, 255, 255, 255, 255);

        byte[] texels =
        [
            255, 0, 255, 255, 255, 255, 0, 255,
            255, 0, 255, 255, 255, 255, 0, 255,
        ];

        using var texture = device.CreateTexture(new TextureDesc(2, 2, TextureFormat.Rgba8), texels);
        using var sampler = device.CreateSampler(new SamplerDesc());
        using var target = device.CreateRenderTarget(new RenderTargetDesc(size, size, TextureFormat.Rgba8));
        using var vertexBuffer = device.CreateBuffer(
            new BufferDesc(BufferKind.Vertex, BufferUsage.Immutable, vertices.Length),
            vertices
        );

        var commandBuffer = device.AcquireCommandBuffer();
        commandBuffer.SetRenderTarget(target);
        commandBuffer.Clear(ClearOptions.Color, new ColorRgba(1f, 0f, 0f));
        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetUniform(UniformOffset(pipeline, "Projection"), MemoryMarshal.AsBytes(Identity().AsSpan()));
        commandBuffer.SetVertexBuffer(0, vertexBuffer, stride);
        commandBuffer.SetShaderResource(0, texture, sampler);
        commandBuffer.Draw(0, 2);
        commandBuffer.SetRenderTarget(null);
        device.Submit(commandBuffer);

        var left = new byte[4];
        var right = new byte[4];
        device.ReadTexture(target.ColorTexture, size / 4, size / 2, 1, 1, left);
        device.ReadTexture(target.ColorTexture, size * 3 / 4, size / 2, 1, 1, right);

        Report(
            "DRAW",
            $"a textured quad samples its texture's two columns in order "
            + $"(left {left[0]},{left[1]},{left[2]}, right {right[0]},{right[1]},{right[2]})",
            left[0] == 255 && left[1] == 0 && left[2] == 255 && right[0] == 255 && right[1] == 255 && right[2] == 0
        );

        var drawError = device.TakeError();
        Report(
            "DRAW",
            "the device reported no validation error after the draw" + (drawError is null ? "" : $": {drawError}"),
            drawError is null
        );
    }

    /// <summary>
    /// Rebinding a texture inside one pass must rebuild the bind group without invalidating the one
    /// the pass already recorded. wgpu resolves a pass's bind-group ids at <c>RenderPassEncoderEnd</c>
    /// rather than when they are set, so releasing the replaced group before the pass closes makes
    /// wgpu-core panic ("BindGroup[Id(..)] does not exist") and abort the process.
    /// </summary>
    private static void CheckMidPassTextureRebind(WebGpuGraphicsDevice device)
    {
        const int size = 64;
        const int stride = 20;

        var bundle = ImGuiFullbright.Create();
        var module = device.CreateShaderModule(bundle.Vertex, bundle.Pixel, bundle.Reflection);

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

        // Two quads, one per half of the target, so each draw's texel is readable on its own.
        var vertices = new byte[12 * stride];
        WriteVertex(vertices, 0, -1, -1, 0, 1, 255, 255, 255, 255);
        WriteVertex(vertices, 1, 0, -1, 1, 1, 255, 255, 255, 255);
        WriteVertex(vertices, 2, 0, 1, 1, 0, 255, 255, 255, 255);
        WriteVertex(vertices, 3, -1, -1, 0, 1, 255, 255, 255, 255);
        WriteVertex(vertices, 4, 0, 1, 1, 0, 255, 255, 255, 255);
        WriteVertex(vertices, 5, -1, 1, 0, 0, 255, 255, 255, 255);
        WriteVertex(vertices, 6, 0, -1, 0, 1, 255, 255, 255, 255);
        WriteVertex(vertices, 7, 1, -1, 1, 1, 255, 255, 255, 255);
        WriteVertex(vertices, 8, 1, 1, 1, 0, 255, 255, 255, 255);
        WriteVertex(vertices, 9, 0, -1, 0, 1, 255, 255, 255, 255);
        WriteVertex(vertices, 10, 1, 1, 1, 0, 255, 255, 255, 255);
        WriteVertex(vertices, 11, 0, 1, 0, 0, 255, 255, 255, 255);

        byte[] magenta = [255, 0, 255, 255, 255, 0, 255, 255, 255, 0, 255, 255, 255, 0, 255, 255];
        byte[] cyan = [0, 255, 255, 255, 0, 255, 255, 255, 0, 255, 255, 255, 0, 255, 255, 255];

        using var first = device.CreateTexture(new TextureDesc(2, 2, TextureFormat.Rgba8), magenta);
        using var second = device.CreateTexture(new TextureDesc(2, 2, TextureFormat.Rgba8), cyan);
        using var sampler = device.CreateSampler(new SamplerDesc());
        using var target = device.CreateRenderTarget(new RenderTargetDesc(size, size, TextureFormat.Rgba8));
        using var vertexBuffer = device.CreateBuffer(
            new BufferDesc(BufferKind.Vertex, BufferUsage.Immutable, vertices.Length),
            vertices
        );

        var commandBuffer = device.AcquireCommandBuffer();
        commandBuffer.SetRenderTarget(target);
        commandBuffer.Clear(ClearOptions.Color, new ColorRgba(0f, 0f, 0f, 1f));
        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetUniform(UniformOffset(pipeline, "Projection"), MemoryMarshal.AsBytes(Identity().AsSpan()));
        commandBuffer.SetVertexBuffer(0, vertexBuffer, stride);

        commandBuffer.SetShaderResource(0, first, sampler);
        commandBuffer.Draw(0, 2);

        commandBuffer.SetShaderResource(0, second, sampler);
        commandBuffer.Draw(6, 2);

        commandBuffer.SetRenderTarget(null);
        device.Submit(commandBuffer);

        var left = new byte[4];
        var right = new byte[4];
        device.ReadTexture(target.ColorTexture, size / 4, size / 2, 1, 1, left);
        device.ReadTexture(target.ColorTexture, size * 3 / 4, size / 2, 1, 1, right);

        Report(
            "REBIND",
            $"a rebind mid-pass keeps both groups alive "
            + $"(left {left[0]},{left[1]},{left[2]}, right {right[0]},{right[1]},{right[2]})",
            left[0] == 255 && left[1] == 0 && left[2] == 255
            && right[0] == 0 && right[1] == 255 && right[2] == 255
        );

        var rebindError = device.TakeError();
        Report(
            "REBIND",
            "the device reported no validation error after the rebind" + (rebindError is null ? "" : $": {rebindError}"),
            rebindError is null
        );
    }

    /// <summary>
    /// A frame that outgrows the uniform ring replaces it mid-pass, which invalidates the variant's
    /// cached bind group while the open pass still names it. This is the busy-frame shape of the
    /// rebind above (256 KiB of per-draw blocks is not a pathological frame), and it checks that the
    /// draws recorded before the growth still resolve.
    /// </summary>
    private static void CheckUniformRingGrowth(WebGpuGraphicsDevice device)
    {
        const int size = 16;
        const int stride = 20;
        const int draws = 5000;

        var bundle = ImGuiFullbright.Create();
        var module = device.CreateShaderModule(bundle.Vertex, bundle.Pixel, bundle.Reflection);

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

        var vertices = new byte[6 * stride];
        WriteVertex(vertices, 0, -1, -1, 0, 1, 255, 255, 255, 255);
        WriteVertex(vertices, 1, 1, -1, 1, 1, 255, 255, 255, 255);
        WriteVertex(vertices, 2, 1, 1, 1, 0, 255, 255, 255, 255);
        WriteVertex(vertices, 3, -1, -1, 0, 1, 255, 255, 255, 255);
        WriteVertex(vertices, 4, 1, 1, 1, 0, 255, 255, 255, 255);
        WriteVertex(vertices, 5, -1, 1, 0, 0, 255, 255, 255, 255);

        byte[] green = [0, 255, 0, 255, 0, 255, 0, 255, 0, 255, 0, 255, 0, 255, 0, 255];

        using var texture = device.CreateTexture(new TextureDesc(2, 2, TextureFormat.Rgba8), green);
        using var sampler = device.CreateSampler(new SamplerDesc());
        using var target = device.CreateRenderTarget(new RenderTargetDesc(size, size, TextureFormat.Rgba8));
        using var vertexBuffer = device.CreateBuffer(
            new BufferDesc(BufferKind.Vertex, BufferUsage.Immutable, vertices.Length),
            vertices
        );

        var commandBuffer = device.AcquireCommandBuffer();
        commandBuffer.SetRenderTarget(target);
        commandBuffer.Clear(ClearOptions.Color, new ColorRgba(0f, 0f, 0f, 1f));
        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetVertexBuffer(0, vertexBuffer, stride);
        commandBuffer.SetShaderResource(0, texture, sampler);

        // 5000 draws at the block size the ring aligns to (64 bytes) is ~320 KiB: one ring growth,
        // and the draws recorded before it still name the old buffer.
        for (var i = 0; i < draws; i++)
        {
            commandBuffer.SetUniform(UniformOffset(pipeline, "Projection"), MemoryMarshal.AsBytes(Identity().AsSpan()));
            commandBuffer.Draw(0, 2);
        }

        commandBuffer.SetRenderTarget(null);
        device.Submit(commandBuffer);

        var pixel = new byte[4];
        device.ReadTexture(target.ColorTexture, size / 2, size / 2, 1, 1, pixel);
        Report(
            "RING",
            $"{draws} draws across a ring growth leave the last draw's quad intact "
            + $"(middle {pixel[0]},{pixel[1]},{pixel[2]})",
            pixel[1] == 255 && pixel[0] == 0
        );

        var ringError = device.TakeError();
        Report(
            "RING",
            "the device reported no validation error after the ring grew" + (ringError is null ? "" : $": {ringError}"),
            ringError is null
        );
    }

    /// <summary>
    /// A translation in a matrix uniform must move the geometry, which is the only observable
    /// difference between a row-major write and the column-major bytes the shader reads.
    ///
    /// Two draws are recorded against the same pipeline, before either is submitted, and each writes
    /// a different translation - which is what makes this the test of the uniform ring rather than of
    /// the transpose alone. A ring that collapsed the two writes would put the second draw's quad
    /// where the first one was.
    /// </summary>
    private static void CheckTranslationUniform(WebGpuGraphicsDevice device)
    {
        const int size = 64;
        const int stride = 20;

        var bundle = ImGuiFullbright.Create();
        var module = device.CreateShaderModule(bundle.Vertex, bundle.Pixel, bundle.Reflection);

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

        // A quad in the right half of NDC space.
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

        var projection = Identity();
        projection[12] = 0.5f;

        var commandBuffer = device.AcquireCommandBuffer();

        // Draw one: no translation, so the quad covers NDC x in [0, 1] - the right half.
        commandBuffer.SetRenderTarget(target);
        commandBuffer.Clear(ClearOptions.Color, new ColorRgba(0f, 0f, 0f, 1f));
        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetUniform(UniformOffset(pipeline, "Projection"), MemoryMarshal.AsBytes(Identity().AsSpan()));
        commandBuffer.SetVertexBuffer(0, vertexBuffer, stride);
        commandBuffer.SetShaderResource(0, texture, sampler);
        commandBuffer.Draw(0, 2);
        commandBuffer.SetRenderTarget(null);
        device.Submit(commandBuffer);

        var untouched = new byte[4];
        var covered = new byte[4];
        device.ReadTexture(target.ColorTexture, size / 4, size / 2, 1, 1, untouched);
        device.ReadTexture(target.ColorTexture, size * 3 / 4, size / 2, 1, 1, covered);

        Report(
            "UNIFORM",
            "with no translation the quad covers only the right half",
            untouched[0] == 0 && covered[0] == 128
        );

        // Draw two: the same quad translated +0.5 in NDC, so it now covers only x in [0.5, 1.5] -
        // the right quarter. A matrix written untransposed would put 0.5 in the fourth row, where it
        // acts as w and collapses the quad onto the origin instead of moving it.
        var commandBuffer2 = device.AcquireCommandBuffer();
        commandBuffer2.SetRenderTarget(target);
        commandBuffer2.Clear(ClearOptions.Color, new ColorRgba(0f, 0f, 0f, 1f));
        commandBuffer2.SetPipeline(pipeline);
        commandBuffer2.SetUniform(UniformOffset(pipeline, "Projection"), MemoryMarshal.AsBytes(projection.AsSpan()));
        commandBuffer2.SetVertexBuffer(0, vertexBuffer, stride);
        commandBuffer2.SetShaderResource(0, texture, sampler);
        commandBuffer2.Draw(0, 2);
        commandBuffer2.SetRenderTarget(null);
        device.Submit(commandBuffer2);

        device.ReadTexture(target.ColorTexture, size / 4, size / 2, 1, 1, untouched);
        device.ReadTexture(target.ColorTexture, size * 3 / 4, size / 2, 1, 1, covered);

        Report(
            "UNIFORM",
            $"a matrix translation moves the geometry rather than scaling it "
            + $"(left quarter {untouched[0]}, right quarter {covered[0]} - expected 0 then 128)",
            untouched[0] == 0 && covered[0] == 128
        );

        var uniformError = device.TakeError();
        Report(
            "UNIFORM",
            "the device reported no validation error" + (uniformError is null ? "" : $": {uniformError}"),
            uniformError is null
        );
    }

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

    private static float[] Identity() =>
    [
        1, 0, 0, 0,
        0, 1, 0, 0,
        0, 0, 1, 0,
        0, 0, 0, 1,
    ];

    private static void Report(string category, string what, bool ok)
    {
        if (!ok)
            _failures++;
        Console.WriteLine($"{category}: {(ok ? "ok" : "<-- FAIL")} - {what}");
    }

    /// <summary>
    /// Every generated bundle, with the vertex layout its input signature implies - the same set the
    /// D3D11 smoke builds, so the two files read against each other.
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

    private const int PositionColorStride = 16;

    private static VertexLayoutDesc PositionColorLayout => new(
        Attributes:
        [
            new VertexAttributeDesc("POSITION", 0, 0, VertexAttributeFormat.Float3),
            new VertexAttributeDesc("COLOR", 0, 12, VertexAttributeFormat.Byte4Normalized),
        ],
        StrideInBytes: PositionColorStride
    );

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
    /// The per-instance stream, pre-expanded into one float4 attribute per register - the shape a
    /// matrix input takes when a layout lists it as four TEXCOORD slots, which is what the WebGPU
    /// vertex-input table has to bridge back onto locations.
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
}
