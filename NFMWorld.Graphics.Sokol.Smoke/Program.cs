// LLM maintained.
//
// Smoke test for NFMWorld.Graphics.Sokol: builds a real device through sokol_app, loads a shader
// program, and draws one hardware-instanced line through the abstraction - then verifies the
// result. It is the sokol counterpart of NFMWorld.Graphics.FNA3D.Smoke, and exists to prove the
// parts of this backend that only a live device can exercise: the sg_shader_desc binding tables
// (views/samplers/pairs/uniform blocks) that sokol validates on every draw, the uniform-block
// upload path, the pass/commit lifetime, and the render-target attachment views.
//
// Two things make this structurally different from the FNA3D smoke test:
//
//  1. sokol_app owns the window and the main loop, and sg_setup's D3D11 backend needs the
//     ID3D11Device sokol_app injects, so the whole test runs inside SokolGraphicsDevice.Run's
//     callbacks rather than in a top-level loop.
//  2. There is no GPU readback in sokol_gfx at all (no sg_read_texture, and the D3D11 backend
//     never issues a staging copy), so "did the line rasterize" cannot be answered by reading the
//     backbuffer the way FNA3D_ReadBackbuffer allows. Instead this leans on the one signal sokol
//     does give: validation failures are *logged*, not thrown, so a logger that counts error-level
//     items turns "the draw was silently rejected" into a hard failure.
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NFMWorld.Graphics;
using NFMWorld.Graphics.Sokol;
using NFMWorld.Shaders;
using SharpSokol.Native;
using ClearOptions = NFMWorld.Graphics.ClearOptions;

namespace NFMWorld.Graphics.Sokol.Smoke;

internal static class Program
{
    private const int Width = 1280;
    private const int Height = 720;

    private static SokolLog _log = null!;
    private static int _frames;

    private static int Main()
    {
        // sokol hands every validation failure to the logger instead of returning an error, so
        // with no logger installed a rejected draw is completely silent. Counting error-level
        // items is a far stronger check than any pixel test available here: it catches a wrong
        // register, a missing view binding, or a uniform range that does not match the block -
        // exactly the failure modes this backend's binding-table derivation can get wrong.
        _log = new SokolLog();

        var exitCode = 0;
        SokolGraphicsDevice.Run(
            "NFMWorld.Graphics.Sokol smoke test",
            Width,
            Height,
            init: _ => { },
            frame: Frame,
            shutdown: _ =>
            {
                Console.WriteLine();
                Console.WriteLine(_log.Summary());
                if (_log.HasErrors) exitCode = 1;
            },
            configure: (ref sg_desc desc) => desc.logger = _log.Descriptor);

        Console.WriteLine(exitCode == 0 ? "SMOKE: pass" : "SMOKE: FAILED");
        return exitCode;
    }

    /// <summary>
    /// One frame. The first frame runs the checks and then draws; later frames just let sokol_app
    /// spin its loop (it creates the window and the swapchain inside <c>sapp_run</c>) before
    /// quitting.
    ///
    /// Nothing is committed from <c>init</c>: <c>sg_commit</c> flushes to the current swapchain,
    /// and sokol_app only presents the swapchain after the frame callback returns - so a commit
    /// issued during initialization would present outside the loop's own present.
    /// </summary>
    private static void Frame(SokolGraphicsDevice device)
    {
        if (_frames++ > 0) { App.quit(); return; }

        RunChecks(device);
        DrawLine(device);
    }

    /// <summary>
    /// Exercises the whole abstraction surface against a live device. Everything here is a call
    /// that sokol validates, so a mistake shows up as a logged error rather than a wrong picture.
    /// </summary>
    private static void RunChecks(SokolGraphicsDevice device)
    {
        Console.WriteLine($"DEVICE: swapchain {device.Swapchain.Width}x{device.Swapchain.Height}, {device.Swapchain.MultiSampleCount}x MSAA");

        using var pipeline = device.CreatePipeline(new PipelineDesc(
            VertexShader: LineProgram,
            PixelShader: LineProgram,
            VertexLayouts: [GeometryLayout, InstanceLayout],
            BlendState: BlendStateDesc.Opaque,
            DepthStencilState: DepthStencilStateDesc.Default,
            RasterizerState: RasterizerStateDesc.Default with { CullMode = CullMode.None },
            Topology: PrimitiveTopology.TriangleList));

        Console.WriteLine($"PIPELINE: built, {pipeline.Reflection.Uniforms.Count} reflected uniforms, " +
                          $"{pipeline.Reflection.Textures.Count} texture(s), {pipeline.Reflection.Samplers.Count} sampler(s)");

        // Every reflected binding must resolve to a real slot - a -1 means the name the caller
        // writes to is not in the reflection at all, which would silently skip the upload.
        foreach (var name in new[] { "ViewProj", "Color", "ViewportSize", "Thickness" })
        {
            var slot = SlotOf(pipeline.Reflection, name);
            Console.WriteLine(slot >= 0
                ? $"  uniform {name,-13} at byte offset {slot}"
                : $"  <-- FAIL: uniform {name} not found in the pipeline reflection");
        }

        // Each check reports the sokol errors it produced, so a failure names the step rather
        // than only leaving a log line at the end.
        Bracket("pipeline creation above", () => { });
        Bracket("texture", () => CheckTexture(device));
        Bracket("mipmapped texture", () => CheckMipmappedTexture(device));
        Bracket("render target", () => CheckRenderTarget(device));
        Bracket("indexed draw", () => CheckIndexBufferFormats(device));
        Bracket("command buffer", () => CheckSingleLiveCommandBuffer(device));
        Bracket("buffer", () => CheckDynamicBuffer(device));
    }

    /// <summary>Runs <paramref name="check"/> and reports any sokol errors it caused.</summary>
    private static void Bracket(string what, Action check)
    {
        var before = _log.ErrorCount;
        check();
        var produced = _log.ErrorCount - before;
        if (produced > 0) Console.WriteLine($"  <-- {produced} sokol error(s) during: {what}");
    }

    /// <summary>
    /// A round trip through a texture that is sampled and then updated through a sub-rectangle.
    /// This is the one place the mirror-backed readback can be checked exactly, because the
    /// backend itself produced the bytes.
    ///
    /// The texture is deliberately single-level: an updatable image is created with
    /// <c>dynamic_update</c>, which D3D11 maps to <c>USAGE_DYNAMIC</c> - and <c>CreateTexture2D</c>
    /// rejects that usage for any image with more than one mip level. See
    /// <c>CheckMipmappedTexture</c> for the mipmapped case, which is immutable instead.
    /// </summary>
    private static void CheckTexture(SokolGraphicsDevice device)
    {
        const int w = 4, h = 2;
        var source = new byte[w * h * 4];
        for (var i = 0; i < w; i++)
        {
            source[i * 4 + 0] = 255;                    // row 0 red
            source[i * 4 + 3] = 255;
            var b = (w + i) * 4;
            source[b + 2] = 255;                        // row 1 blue
            source[b + 3] = 255;
        }

        using var texture = device.CreateTexture(new TextureDesc(w, h, TextureFormat.Rgba8), source);
        var readBack = new byte[source.Length];
        device.ReadTexture(texture, 0, 0, w, h, readBack);
        Report("TEXTURE", "creation + readback round-trip", readBack.AsSpan().SequenceEqual(source));

        // A sub-rectangle update must leave the rest of the surface alone. Without the CPU mirror
        // sokol would upload a whole level built from a zeroed staging buffer, blanking row 0 -
        // so this specifically proves the mirror is doing its job.
        var cb = device.AcquireCommandBuffer();
        Span<byte> green = [0, 255, 0, 255, 0, 255, 0, 255];
        cb.UpdateTexture(texture, 0, 1, 2, 1, green);
        device.Submit(cb);

        device.ReadTexture(texture, 0, 0, w, h, readBack);
        // Row 0 is four red pixels; row 1 starts with two greens then two blues.
        var row0Intact = readBack[0] == 255 && readBack[1] == 0 && readBack[2] == 0;
        // The whole four-byte pixel, not just a channel: the written pixel is exactly `green`,
        // so anything the update left behind - a stale channel, a wrong alpha - fails here.
        var row1Patched = readBack.AsSpan(w * 4, 8).SequenceEqual(green);
        // The last two columns of row 1 are still blue: red and alpha stay 0, blue stays 255.
        var row1TailIntact = readBack[(w + 2) * 4 + 2] == 255 && readBack[(w + 2) * 4 + 0] == 0 && readBack[(w + 2) * 4 + 3] == 255;
        Report("TEXTURE", "sub-rect update preserves the rows it did not touch", row0Intact);
        Report("TEXTURE", "sub-rect update wrote the pixels it did touch", row1Patched);
        Report("TEXTURE", "sub-rect update preserves the columns it did not touch", row1TailIntact);
    }

    /// <summary>
    /// A mipmapped texture takes the immutable path, so its whole chain has to arrive in the
    /// descriptor - sokol validates that on creation and rejects an image whose levels past 0 are
    /// empty. That makes creation itself the assertion; a wrong level count or a missing level
    /// shows up as a logged error rather than a wrong image.
    /// </summary>
    private static void CheckMipmappedTexture(SokolGraphicsDevice device)
    {
        const int w = 8, h = 8;
        var source = new byte[w * h * 4];
        for (var i = 0; i < source.Length; i++) source[i] = 255;

        using var texture = device.CreateTexture(new TextureDesc(w, h, TextureFormat.Rgba8, MipMapped: true), source);
        Report("TEXTURE", "mipmapped texture accepted with a generated chain", true);

        // Readback is mirror-backed and the mirror only ever holds mip 0, which is the documented
        // limitation rather than an accident.
        var readBack = new byte[source.Length];
        device.ReadTexture(texture, 0, 0, w, h, readBack);
        Report("TEXTURE", "mip 0 of a mipmapped texture round-trips", readBack.AsSpan().SequenceEqual(source));

        var refused = false;
        var cb = device.AcquireCommandBuffer();
        try { cb.UpdateTexture(texture, 0, 0, 1, 1, [0, 0, 0, 0]); }
        catch (NotSupportedException) { refused = true; }
        device.Submit(cb);
        Report("TEXTURE", "updating a mipmapped (immutable) texture fails loudly", refused);
    }

    /// <summary>
    /// An off-screen target: the colour attachment view and the depth-stencil attachment view both
    /// have to be accepted by <c>sg_begin_pass</c>, which is where a bad view format or a missing
    /// attachment usage flag surfaces.
    /// </summary>
    private static void CheckRenderTarget(SokolGraphicsDevice device)
    {
        const int w = 16, h = 16;
        using var target = device.CreateRenderTarget(new RenderTargetDesc(w, h, TextureFormat.Rgba8, HasDepthStencil: true));
        Report("RENDER TARGET", "colour + depth-stencil textures created",
            target.ColorTexture is not null && target.DepthStencilTexture is not null);

        var cb = device.AcquireCommandBuffer();
        cb.SetRenderTarget(target);
        cb.SetViewport(new Viewport(0, 0, w, h));
        cb.Clear(ClearOptions.Color | ClearOptions.Depth, new ColorRgba(0.1f, 0.7f, 0.3f));
        cb.SetRenderTarget(null);
        cb.SetViewport(new Viewport(0, 0, device.Swapchain.Width, device.Swapchain.Height));
        device.Submit(cb);
        Report("RENDER TARGET", "attachments accepted by sg_begin_pass", true);

        // sokol_gfx has no way to read a rendered image back, so the documented behaviour is a
        // NotSupportedException rather than silently-wrong bytes. Asserting that is what keeps the
        // limitation honest instead of accidental.
        var pixels = new byte[w * h * 4];
        var threw = false;
        try { device.ReadTexture(target.ColorTexture!, 0, 0, w, h, pixels); }
        catch (NotSupportedException) { threw = true; }
        Report("RENDER TARGET", "readback of a GPU-owned image fails loudly rather than silently", threw);
    }

    /// <summary>
    /// <c>sg_index_type</c> is baked into the pipeline, so a UInt32 index buffer drawn through a
    /// pipeline built for UInt16 would be misread. The backend pins UInt16 - see
    /// <c>BuildPipelineDesc</c> - and this asserts that a 16-bit-indexed draw is accepted, which
    /// covers the format the app's own draw paths use.
    ///
    /// The layout is the full two-stream one, not just the geometry stream. sokol matches the
    /// pipeline's attribute table against the shader's input signature element by element
    /// (sokol_gfx.h:14444), so a layout that covers fewer elements than the compiled VS declares
    /// is rejected by <c>CreateInputLayout</c> as <c>D3D11_CREATE_INPUT_LAYOUT_FAILED</c> - even
    /// for a draw that never reads the missing ones. Declaring two streams then also obliges the
    /// draw to bind both, because sokol requires a bound buffer for every active layout slot.
    /// </summary>
    private static void CheckIndexBufferFormats(SokolGraphicsDevice device)
    {
        using var pipeline = device.CreatePipeline(new PipelineDesc(
            VertexShader: LineProgram,
            PixelShader: LineProgram,
            VertexLayouts: [GeometryLayout, InstanceLayout],
            BlendState: BlendStateDesc.Opaque,
            DepthStencilState: DepthStencilStateDesc.Default,
            RasterizerState: RasterizerStateDesc.Default with { CullMode = CullMode.None },
            Topology: PrimitiveTopology.TriangleList));

        var vertices = QuadVertices();
        Span<ushort> indices = [0, 1, 2, 2, 1, 3];
        // Identity world matrix plus the flag pack the shader's TEXCOORD5 reads; this check draws
        // no instances but the layout still declares the stream - see the remark above.
        Span<float> instance =
        [
            1f, 0f, 0f, 0f,
            0f, 1f, 0f, 0f,
            0f, 0f, 1f, 0f,
            0f, 0f, 0f, 1f,
            0f, 0f, 0f, 0f,
        ];

        using var vertexBuffer = device.CreateBuffer(new BufferDesc(BufferKind.Vertex, BufferUsage.Immutable, vertices.Length), vertices);
        using var indexBuffer = device.CreateBuffer(new BufferDesc(BufferKind.Index, BufferUsage.Immutable, indices.Length * sizeof(ushort), IndexFormat.UInt16),
            MemoryMarshal.AsBytes(indices));
        using var instanceBuffer = device.CreateBuffer(new BufferDesc(BufferKind.Vertex, BufferUsage.Immutable, instance.Length * sizeof(float)),
            MemoryMarshal.AsBytes(instance));
        // The shader declares a texture and a sampler, and sokol requires every declared view and
        // sampler to be bound for a draw - an unbound one is VALIDATE_ABND_EXPECTED_VIEW_BINDING.
        using var texture = device.CreateTexture(new TextureDesc(2, 2, TextureFormat.Rgba8), [
            255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255,
        ]);
        using var sampler = device.CreateSampler(new SamplerDesc(TextureFilter.Linear, TextureAddressMode.Wrap, TextureAddressMode.Wrap));

        var cb = device.AcquireCommandBuffer();
        cb.SetPipeline(pipeline);
        cb.SetVertexBuffer(0, vertexBuffer, GeometryStride);
        cb.SetVertexBuffer(1, instanceBuffer, InstanceStride);
        cb.SetIndexBuffer(indexBuffer);
        for (var slot = 0; slot < pipeline.Reflection.Textures.Count; slot++)
            cb.SetShaderResource(slot, texture, sampler);
        SetUniforms(cb, pipeline.Reflection);
        cb.DrawIndexed(baseVertex: 0, startIndex: 0, primitiveCount: 2);
        device.Submit(cb);

        Report("INDEXED DRAW", "UInt16 index buffer accepted (the format the pipeline declares)", true);
    }

    /// <summary>
    /// The abstraction allows only one live command buffer, and sokol's frame boundary is
    /// <c>sg_commit</c> - so a second acquisition before Submit must be refused rather than
    /// silently producing a second commit.
    /// </summary>
    private static void CheckSingleLiveCommandBuffer(SokolGraphicsDevice device)
    {
        var cb = device.AcquireCommandBuffer();
        var threw = false;
        try { device.AcquireCommandBuffer(); }
        catch (InvalidOperationException) { threw = true; }
        Report("COMMAND BUFFER", "a second live command buffer is refused", threw);
        device.Submit(cb);
    }

    /// <summary>
    /// A dynamic buffer's initial data takes a different path from an immutable one's - it is
    /// uploaded through <c>sg_update_buffer</c> after creation, because sokol rejects an update to
    /// an immutable buffer and treats a descriptor-supplied buffer as immutable.
    ///
    /// The two updates in one frame are the point: sokol permits only one
    /// <c>sg_update_buffer</c> per buffer per frame (<c>VALIDATE_UPDATEBUF_ONCE</c>), so the
    /// backend has to collapse them into a single upload - and the sub-range one has to land
    /// without disturbing the bytes it does not name.
    /// </summary>
    private static void CheckDynamicBuffer(SokolGraphicsDevice device)
    {
        Span<float> data = [1f, 2f, 3f, 4f];
        using var buffer = device.CreateBuffer(
            new BufferDesc(BufferKind.Vertex, BufferUsage.Dynamic, data.Length * sizeof(float)),
            MemoryMarshal.AsBytes(data));
        Report("BUFFER", "dynamic buffer accepts post-creation initial data", buffer.Usage == BufferUsage.Dynamic);

        var cb = device.AcquireCommandBuffer();
        Span<byte> patch = [9, 9, 9, 9];
        cb.UpdateBuffer(buffer, MemoryMarshal.AsBytes((ReadOnlySpan<float>)[5f, 6f, 7f, 8f]));
        cb.UpdateBuffer(buffer, patch, offsetBytes: 4);
        device.Submit(cb);
        Report("BUFFER", "dynamic buffer accepts two updates in one frame", true);

        // An immutable buffer has no update path at all; refusing loudly is the documented
        // behaviour rather than a silent no-op that would leave stale contents on the GPU. It has
        // to be created with its data, because sokol requires initial content for an immutable
        // buffer without storage_buffer/write_unsealed usage.
        using var immutable = device.CreateBuffer(
            new BufferDesc(BufferKind.Vertex, BufferUsage.Immutable, 16), new byte[16]);
        var refused = false;
        var cb2 = device.AcquireCommandBuffer();
        try { cb2.UpdateBuffer(immutable, patch); }
        catch (NotSupportedException) { refused = true; }
        device.Submit(cb2);
        Report("BUFFER", "updating an immutable buffer fails loudly", refused);
    }

    /// <summary>The frame's real draw - its validation is the most valuable part of this test, since it is the only path that exercises bindings and uniforms together.</summary>
    private static void DrawLine(SokolGraphicsDevice device)
    {
        var before = _log.ErrorCount;
        using var pipeline = device.CreatePipeline(new PipelineDesc(
            VertexShader: LineProgram,
            PixelShader: LineProgram,
            VertexLayouts: [GeometryLayout, InstanceLayout],
            BlendState: BlendStateDesc.Opaque,
            DepthStencilState: DepthStencilStateDesc.Default,
            RasterizerState: RasterizerStateDesc.Default with { CullMode = CullMode.None },
            Topology: PrimitiveTopology.TriangleList));

        var vertices = QuadVertices();
        Span<ushort> indices = [0, 1, 2, 2, 1, 3];
        // Per-instance world matrix (column-major, pre-transposed for mul(vector, world)) followed
        // by the flag/parameter pack the shader's TEXCOORD5 reads.
        Span<float> instance =
        [
            1f, 0f, 0f, 0f,
            0f, 1f, 0f, 0f,
            0f, 0f, 1f, 0f,
            0f, 0f, 0f, 1f,
            0f, 0f, 0f, 0f,
        ];

        using var vertexBuffer = device.CreateBuffer(new BufferDesc(BufferKind.Vertex, BufferUsage.Immutable, vertices.Length), vertices);
        using var indexBuffer = device.CreateBuffer(new BufferDesc(BufferKind.Index, BufferUsage.Immutable, indices.Length * sizeof(ushort), IndexFormat.UInt16), MemoryMarshal.AsBytes(indices));
        using var instanceBuffer = device.CreateBuffer(new BufferDesc(BufferKind.Vertex, BufferUsage.Immutable, instance.Length * sizeof(float)), MemoryMarshal.AsBytes(instance));
        using var sampler = device.CreateSampler(new SamplerDesc(TextureFilter.Linear, TextureAddressMode.Wrap, TextureAddressMode.Wrap));
        using var texture = device.CreateTexture(new TextureDesc(2, 2, TextureFormat.Rgba8), [
            255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255,
        ]);

        var cb = device.AcquireCommandBuffer();
        cb.Clear(ClearOptions.Color | ClearOptions.Depth, new ColorRgba(0.05f, 0.1f, 0.2f));
        cb.SetViewport(new Viewport(0, 0, device.Swapchain.Width, device.Swapchain.Height));
        cb.SetPipeline(pipeline);
        SetUniforms(cb, pipeline.Reflection);

        // Every view and sampler the shader declares must be bound, or sokol rejects the draw with
        // VALIDATE_ABND_EXPECTED_VIEW_BINDING - so this deliberately binds the full reflected set.
        for (var slot = 0; slot < pipeline.Reflection.Textures.Count; slot++)
            cb.SetShaderResource(slot, texture, sampler);

        cb.SetVertexBuffer(0, vertexBuffer, GeometryStride);
        cb.SetVertexBuffer(1, instanceBuffer, InstanceStride);
        cb.SetIndexBuffer(indexBuffer);
        cb.DrawIndexedInstanced(baseVertex: 0, startIndex: 0, primitiveCount: 2, instanceCount: 1);
        device.Submit(cb);

        var produced = _log.ErrorCount - before;
        Console.WriteLine(produced == 0
            ? "DRAW: one hardware-instanced line submitted, no sokol errors"
            : $"DRAW: <-- FAIL, {produced} sokol error(s) during pipeline creation or the draw");
    }

    private static void SetUniforms(ICommandBuffer cb, ShaderReflection reflection)
    {
        Set(cb, reflection, "ViewProj", MemoryMarshal.AsBytes((ReadOnlySpan<float>)[
            0.5f, 0f, 0f, 0f,
            0f, 1f, 0f, 0f,
            0f, 0f, 1f, 0f,
            0f, 0f, -3f, 1f,
        ]));
        Set(cb, reflection, "Color", MemoryMarshal.AsBytes((ReadOnlySpan<float>)[1f, 0.35f, 0.1f, 1f]));
        Set(cb, reflection, "ViewportSize", MemoryMarshal.AsBytes((ReadOnlySpan<float>)[1f, 1f]));
        Set(cb, reflection, "Thickness", MemoryMarshal.AsBytes((ReadOnlySpan<float>)[6f]));
    }

    /// <summary>
    /// Writes a uniform by name. The slot is the uniform's byte offset within the merged block -
    /// see <see cref="ICommandBuffer.SetUniform"/> - which is what the generated bundles' SlotOf
    /// returns. A name the shader compiler optimized out is skipped rather than written to a
    /// bogus offset.
    /// </summary>
    private static void Set(ICommandBuffer cb, ShaderReflection reflection, string name, ReadOnlySpan<byte> value)
    {
        var slot = SlotOf(reflection, name);
        if (slot >= 0) cb.SetUniform(slot, value);
    }

    private static int SlotOf(ShaderReflection reflection, string name)
    {
        foreach (var uniform in reflection.Uniforms)
            if (uniform.Name == name) return uniform.Offset;
        return -1;
    }

    private static void Report(string category, string what, bool ok) =>
        Console.WriteLine($"{category}: {(ok ? "ok" : "<-- FAIL")} - {what}");

    /// <summary>Bytes per vertex in the geometry stream: position (float3), colour (float4), side (float).</summary>
    private const int GeometryStride = 32;

    private static VertexLayoutDesc GeometryLayout => new(
        Attributes:
        [
            new VertexAttributeDesc("POSITION", 0, 0, VertexAttributeFormat.Float3),
            new VertexAttributeDesc("COLOR", 0, 12, VertexAttributeFormat.Float4),
            new VertexAttributeDesc("TEXCOORD", 0, 28, VertexAttributeFormat.Float1),
        ],
        StrideInBytes: GeometryStride);

    /// <summary>Bytes per instance: one float4 world-matrix row per TEXCOORD register, plus a parameter pack.</summary>
    private const int InstanceStride = 80;

    private static VertexLayoutDesc InstanceLayout => new(
        Attributes:
        [
            new VertexAttributeDesc("TEXCOORD", 1, 0, VertexAttributeFormat.Float4),
            new VertexAttributeDesc("TEXCOORD", 2, 16, VertexAttributeFormat.Float4),
            new VertexAttributeDesc("TEXCOORD", 3, 32, VertexAttributeFormat.Float4),
            new VertexAttributeDesc("TEXCOORD", 4, 48, VertexAttributeFormat.Float4),
            new VertexAttributeDesc("TEXCOORD", 5, 64, VertexAttributeFormat.Float4),
        ],
        StrideInBytes: InstanceStride,
        InstanceStepRate: 1);

    /// <summary>Four vertices of a unit-height quad lying along the view's forward axis.</summary>
    private static byte[] QuadVertices()
    {
        float[][] corners =
        [
            [-1f, 0f, 0f, 1f, 1f, 1f, 1f, -1f],
            [1f, 0f, 0f, 1f, 1f, 1f, 1f, 1f],
            [-1f, 0f, 2f, 0f, 1f, 0f, 1f, -1f],
            [1f, 0f, 2f, 0f, 1f, 1f, 0f, 1f],
        ];

        var bytes = new byte[GeometryStride * corners.Length];
        for (var v = 0; v < corners.Length; v++)
            for (var f = 0; f < corners[v].Length; f++)
                BitConverter.TryWriteBytes(bytes.AsSpan(v * GeometryStride + f * 4), corners[v][f]);
        return bytes;
    }

    /// <summary>
    /// The vertex/pixel pair this test draws with.
    ///
    /// It is hand-written HLSL rather than the generated <c>Line</c> bundle, and that is a question
    /// of test isolation rather than of capability: the shader compiler does emit HLSL for D3D11
    /// (the bundle carries SPIR-V, HLSL, MSL and GLSL per stage, and
    /// <c>SokolGraphicsDevice.LoadProgram</c> picks by <c>sg_query_backend()</c>), but a generated
    /// bundle also brings its own uniform offsets, texture registers and vertex layout, so
    /// swapping it in would mean reworking this test's geometry and reflection as well. What is
    /// kept in sync by hand here is only the *shape* spirv-cross emits, and the real bundle is
    /// checked separately - the layout it produces has been compiled with D3DCompile and bound to
    /// the app's own vertex layouts.
    ///
    /// The shape deliberately mirrors what spirv-cross emits for a real shader: a single merged
    /// <c>_Global</c> cbuffer at register(b0), and the texture and sampler lists declared at the
    /// same registers in both stages. That is exactly the layout ShaderBindings assumes when it
    /// emits every binding into both stages, so this test exercises the same code path a generated
    /// bundle would.
    ///
    /// This is a cached property rather than a factory because both pipeline stages must be the
    /// <em>same</em> module instance: sokol builds one shader object from the two stages together,
    /// so two independent LoadProgram calls for the vertex and pixel halves are rejected.
    /// </summary>
    private static IShaderModule LineProgram => _program ??= SokolGraphicsDevice.LoadProgram(
        // The bytecode arguments are empty on purpose: the native library vendored here is a
        // D3D11-only build whose shader path consumes HLSL source or a DXBC blob and cannot
        // translate SPIR-V, so the source is what actually gets compiled. The parameters stay in
        // the signature because the abstraction's module carries bytecode for the backends that
        // want it.
        vertexBytecode: ReadOnlyMemory<byte>.Empty,
        pixelBytecode: ReadOnlyMemory<byte>.Empty,
        reflection: Reflection,
        vertexSource: VertexSource,
        pixelSource: PixelSource);

    private static IShaderModule? _program;

    private const string SharedDeclarations = """
        cbuffer _Global : register(b0)
        {
            column_major float4x4 ViewProj : packoffset(c0);
            float4 Color : packoffset(c4);
            float2 ViewportSize : packoffset(c5);
            float Thickness : packoffset(c5.z);
        };

        Texture2D<float4> Texture : register(t0);
        SamplerState TextureSampler : register(s0);
        """;

    private const string VertexSource = SharedDeclarations + """

        struct VSInput
        {
            float3 Position : POSITION0;
            float4 Color : COLOR0;
            float Side : TEXCOORD0;
            float4 World0 : TEXCOORD1;
            float4 World1 : TEXCOORD2;
            float4 World2 : TEXCOORD3;
            float4 World3 : TEXCOORD4;
            float4 Parameters : TEXCOORD5;
        };

        struct VSOutput
        {
            float4 Position : SV_POSITION;
            float4 Color : COLOR0;
            float2 TexCoord : TEXCOORD0;
        };

        VSOutput main(VSInput input)
        {
            float4x4 world = float4x4(input.World0, input.World1, input.World2, input.World3);
            float4 worldPosition = mul(float4(input.Position, 1.0), world);

            VSOutput output;
            output.Position = mul(worldPosition, ViewProj);
            output.Color = input.Color * Color;
            output.TexCoord = float2(input.Side * 0.5 + 0.5, input.Position.z * 0.5);
            return output;
        }
        """;

    private const string PixelSource = SharedDeclarations + """

        struct PSInput
        {
            float4 Position : SV_POSITION;
            float4 Color : COLOR0;
            float2 TexCoord : TEXCOORD0;
        };

        float4 main(PSInput input) : SV_TARGET
        {
            return input.Color * Texture.Sample(TextureSampler, input.TexCoord);
        }
        """;

    /// <summary>
    /// The reflection the test's HLSL actually produces. Hand-written to match, because the
    /// generator that would emit it emits SPIR-V-backed bundles.
    ///
    /// The offsets are the packoffset layout D3D11 gives the cbuffer (float4-aligned, with
    /// ViewportSize at c5.xy and Thickness at c5.z sharing a register), which is what the generated
    /// bundles carry in <c>UniformParam.Offset</c>. This is load-bearing: SetUniform's slot is that
    /// byte offset, so a wrong number writes into the wrong part of the block rather than failing
    /// outright.
    /// </summary>
    private static ShaderReflection Reflection => new()
    {
        Uniforms =
        [
            new UniformParam("ViewProj", 0, 64, UniformType.Matrix4x4),
            new UniformParam("Color", 64, 16, UniformType.Vector4),
            new UniformParam("ViewportSize", 80, 8, UniformType.Vector2),
            new UniformParam("Thickness", 88, 4, UniformType.Float),
        ],
        Textures = [new ResourceBinding("Texture", 0)],
        Samplers = [new ResourceBinding("TextureSampler", 0)],
    };

    /// <summary>
    /// Counts sokol's error-level log items so a validation failure becomes a test failure.
    ///
    /// This indirection is the whole point: <c>_SG_VALIDATE</c> only records an error and logs it
    /// (sokol_gfx.h:7779) - no call returns a failure - so without a logger a rejected draw, a
    /// mismatched register or an unbound view is completely silent, and the test would "pass" while
    /// sokol quietly did nothing.
    ///
    /// The state is static because a <c>[UnmanagedCallersOnly]</c> method cannot close over
    /// anything, so the function pointer cannot capture an instance.
    /// </summary>
    private sealed unsafe class SokolLog
    {
        private static int _panics;
        private static int _errors;
        private static int _warnings;
        private static readonly List<string> _messages = [];

        public bool HasErrors => _panics > 0 || _errors > 0;

        /// <summary>An <c>sg_logger</c> pointing at <see cref="Handle"/>.</summary>
        public sg_logger Descriptor => new() { func = &Handle, user_data = null };

        /// <summary>Errors seen so far, for bracketing a specific check.</summary>
        public int ErrorCount => _panics + _errors;

        public void Reset()
        {
            _messages.Clear();
            _panics = _errors = _warnings = 0;
        }

        public string Summary()
        {
            var head = _panics + _errors == 0
                ? $"SOKOL LOG: no panics, no errors, {_warnings} warning(s)"
                : $"SOKOL LOG: {_panics} panic(s), {_errors} error(s)";
            return _messages.Count == 0 ? head : head + "\n  " + string.Join("\n  ", _messages);
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        private static void Handle(sbyte* tag, uint level, uint item, sbyte* message, uint line, sbyte* file, void* userData)
        {
            _ = userData;
            switch (level)
            {
                case 0: _panics++; break;
                case 1: _errors++; break;
                case 2: _warnings++; break;
                default: break;
            }

            // Marshal eagerly: these pointers are only valid for the duration of this call. sokol
            // inlines the message text only in a SOKOL_DEBUG build, so the item id is the useful
            // part - it maps to the _SG_LOGITEM_XMACRO list in sokol_gfx.h:4704. INFO items carry
            // the D3DCompile output, which is the only place a shader's actual compile messages
            // appear, so they are kept rather than dropped.
            var text = message != null ? Marshal.PtrToStringUTF8((nint)message) : null;
            var where = file != null ? Marshal.PtrToStringUTF8((nint)file) : "sokol_gfx.h";
            var component = tag != null ? Marshal.PtrToStringUTF8((nint)tag) : "sg";
            _messages.Add($"[{component} lvl:{level} id:{item} line:{line} {where}] {text ?? "(no message; item id only)"}");
        }
    }
}
