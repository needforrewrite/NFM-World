// LLM maintained.
//
// Smoke test for NFMWorld.Graphics.OpenGL: brings up a real ANGLE ES 3.0 context, compiles the real
// generated bundles' ES GLSL through the abstraction, draws with them, and reads the pixels back.
//
// Three things make this different from NFMWorld.Graphics.Sokol.Smoke, and all three are GL's doing
// rather than this test's:
//
//  1. GL is immediate-mode. Every command buffer call issues its GL call as it is made, so there is
//     no `sg_commit` frame boundary to bracket and no deferred-replay layer to catch mistakes. What
//     replaces sokol's validated-and-logged errors is glGetError, drained after each check, plus
//     real pixel readback.
//  2. GL *has* readback (glReadPixels) where sokol_gfx has none. That turns "did it rasterize, and
//     where" into a pixel assertion instead of an inference from logged errors - so several checks
//     here are strictly stronger than their sokol counterparts.
//  3. There is no window and no main loop: GlGraphicsDevice.CreateHeadless brings up its own EGL
//     pbuffer, so this is a plain top-to-bottom run rather than a callback inside somebody's frame.
//
// The shaders are the real generated bundles and the vertex layouts are the app's own real
// VertexLayoutDescs (SdlImGuiRenderer's, LineMesh's, InstanceData's, Mesh's). That is deliberate:
// the one thing no hand-written shader could test is whether the ES combined samplers are named
// after the textures the reflection reports. spirv-cross folds the separate texture and sampler into
// one uniform named from the SPIR-V id, so without the shader compiler's NameCombinedSamplers pass
// the lookup is for `ShadowMap0` while the program declares `_1062`. The failure is silent: the
// location comes back -1, the sampler keeps unit 0, and a draw reads whatever happens to be bound
// there. CheckBundlePrograms is the assertion that this does not happen, per bundle, by name.
using System.Runtime.InteropServices;
using NFMWorld.Graphics;
using NFMWorld.Shaders;
using Silk.NET.OpenGLES;
using ClearOptions = NFMWorld.Graphics.ClearOptions;

namespace NFMWorld.Graphics.OpenGL.Smoke;

internal static class Program
{
    private const int Width = 256;
    private const int Height = 256;

    private static int _failures;

    private static GL _gl = null!;

    private static int Main()
    {
        using var device = GlGraphicsDevice.CreateHeadless(Width, Height);
        _gl = device.Gl;
        ReportInterface(device);

        CheckTextureRoundTrip(device);
        CheckMipmappedTexture(device);
        CheckCompressedRefusal(device);
        CheckBuffers(device);
        CheckCommandBufferLifetime(device);
        CheckRenderTarget(device);
        CheckBundlePrograms(device);
        CheckImGuiTexturedDraw(device);
        CheckLineInstancedDraw(device);

        DrainGlErrors("end of run");

        Console.WriteLine();
        Console.WriteLine(_failures == 0 ? "SMOKE: pass" : $"SMOKE: FAILED ({_failures} failure(s))");
        return _failures == 0 ? 0 : 1;
    }

    /// <summary>
    /// What the context actually is.
    ///
    /// Reported rather than asserted, because the point is to make a silent fall back visible: a
    /// software rasterizer or a D3D9 backend would still come up and pass every check below, while
    /// describing a different renderer than the one this POC claims to exercise. The D3D11-platform
    /// request in Egl.CreateHeadless is what is supposed to prevent that, so the renderer string is
    /// how you tell whether it worked on this machine.
    ///
    /// The ANGLE path is reported for the same reason: the bindings' native package puts the right
    /// build next to the executable, and the path is how a reader confirms which one ran.
    /// </summary>
    private static void ReportInterface(GlGraphicsDevice device)
    {
        Console.WriteLine($"DEVICE: EGL {device.EglVersion}, vendor {device.EglVendor}");
        Console.WriteLine($"DEVICE: ANGLE loaded from {device.AngleDirectory}");
        Console.WriteLine($"DEVICE: GL_VENDOR   {glString(StringName.Vendor)}");
        Console.WriteLine($"DEVICE: GL_RENDERER {glString(StringName.Renderer)}");
        Console.WriteLine($"DEVICE: GL_VERSION  {glString(StringName.Version)}");
        Console.WriteLine($"DEVICE: GLSL        {glString(StringName.ShadingLanguageVersion)}");
        Console.WriteLine($"DEVICE: swapchain {device.Swapchain.Width}x{device.Swapchain.Height}, " +
                          $"{device.Swapchain.MultiSampleCount}x MSAA");
        Console.WriteLine();

        // Silk.NET returns the raw byte*; the string is only valid until the next GL call, so it is
        // marshalled immediately.
        static unsafe string glString(StringName name)
        {
            var pointer = _gl.GetString(name);
            return pointer is null ? "(null)" : Marshal.PtrToStringUTF8((nint)pointer) ?? "(unreadable)";
        }
    }

    /// <summary>
    /// A texture that is uploaded and then read back must come back byte-identical.
    ///
    /// This is the check that pins down row order, and it is stated as agreement between the two
    /// operations rather than as an absolute orientation on purpose: the abstraction documents no
    /// handedness, it documents that a caller handing <c>UpdateTexture</c> a span gets that span
    /// back. Upload and readback both go through GL's bottom-left origin, so the round trip holds
    /// as long as neither side flips; a flip on one side only - which is what this caught - shears
    /// the result in the most convincing possible way, since the bytes are all still there.
    ///
    /// The width is deliberately 3: at one byte per pixel that is a 3-byte row, which GL's default
    /// pack *and* unpack alignment of 4 would pad. Getting either wrong shows up as wrong bytes
    /// rather than as an error, so an odd width is exactly what catches it.
    /// </summary>
    private static void CheckTextureRoundTrip(GlGraphicsDevice device)
    {
        const int w = 3, h = 2;
        byte[] source = [200, 200, 200, 40, 40, 40];   // row 0 bright, row 1 dark

        using var texture = device.CreateTexture(new TextureDesc(w, h, TextureFormat.R8), source);
        var readBack = new byte[source.Length];
        device.ReadTexture(texture, 0, 0, w, h, readBack);
        Report("TEXTURE", "R8 upload + readback round-trip preserves row order (odd width: no row padding)",
            readBack.AsSpan().SequenceEqual(source));
        DrainGlErrors("texture round-trip");

        // A sub-rectangle update must leave everything else alone. With GL a wrong offset or a wrong
        // PixelStore state shows up as the wrong bytes rather than as an error.
        var cb = device.AcquireCommandBuffer();
        cb.UpdateTexture(texture, 1, 1, 1, 1, [250]);
        device.Submit(cb);

        device.ReadTexture(texture, 0, 0, w, h, readBack);
        Report("TEXTURE", "sub-rect update wrote the pixel it named", readBack[4] == 250);
        Report("TEXTURE", "sub-rect update left the rest of its row intact", readBack[3] == 40 && readBack[5] == 40);
        Report("TEXTURE", "sub-rect update left the other row intact", readBack[0] == 200 && readBack[2] == 200);
        DrainGlErrors("sub-rect update");
    }

    /// <summary>
    /// A mipmapped texture allocates its whole chain, because a sampler asking for a mipmap on a
    /// texture that only has level 0 reads as incomplete and returns transparent black rather than
    /// asserting. Reading level 0 back is what proves the chain allocation did not disturb the base
    /// level, and updating is refused outright because it would leave every other level stale.
    /// </summary>
    private static void CheckMipmappedTexture(GlGraphicsDevice device)
    {
        const int w = 8, h = 8;
        var source = new byte[w * h * 4];
        Array.Fill(source, (byte)255);

        using var texture = device.CreateTexture(new TextureDesc(w, h, TextureFormat.Rgba8, MipMapped: true), source);
        var readBack = new byte[source.Length];
        device.ReadTexture(texture, 0, 0, w, h, readBack);
        Report("TEXTURE", "mipmapped texture: level 0 round-trips, so the chain did not disturb it",
            readBack.AsSpan().SequenceEqual(source));

        var refused = false;
        var cb = device.AcquireCommandBuffer();
        try { cb.UpdateTexture(texture, 0, 0, 1, 1, [0, 0, 0, 0]); }
        catch (NotSupportedException) { refused = true; }
        device.Submit(cb);
        Report("TEXTURE", "updating a mipmapped texture fails loudly rather than leaving levels stale", refused);
        DrainGlErrors("mipmapped texture");
    }

    /// <summary>
    /// ES 3.0 core has no compressed texture formats at all - they arrive as extensions - so a DXT
    /// upload has to be rejected rather than handed to the driver as bytes it would misread as a
    /// mip chain.
    /// </summary>
    private static void CheckCompressedRefusal(GlGraphicsDevice device)
    {
        var refused = false;
        try { device.CreateTexture(new TextureDesc(4, 4, TextureFormat.Dxt1), new byte[8]); }
        catch (NotSupportedException) { refused = true; }
        Report("TEXTURE", "a block-compressed format is refused rather than misread as raw pixels", refused);
    }

    private static void CheckBuffers(GlGraphicsDevice device)
    {
        Span<float> data = [1f, 2f, 3f, 4f];
        using var dynamic = device.CreateBuffer(
            new BufferDesc(BufferKind.Vertex, BufferUsage.Dynamic, data.Length * sizeof(float)),
            MemoryMarshal.AsBytes(data));
        Report("BUFFER", "dynamic buffer created with initial data", dynamic.Usage == BufferUsage.Dynamic);

        var cb = device.AcquireCommandBuffer();
        cb.UpdateBuffer(dynamic, MemoryMarshal.AsBytes((ReadOnlySpan<float>)[5f, 6f, 7f, 8f]));
        cb.UpdateBuffer(dynamic, [0xAA, 0xAA, 0xAA, 0xAA], offsetBytes: 4);
        device.Submit(cb);
        DrainGlErrors("buffer updates");
        Report("BUFFER", "dynamic buffer takes a full and a sub-range update in one frame", true);

        // GL has no immutability concept for buffers - BufferUsage only picks a usage hint - so this
        // refusal is the backend's own, and it is what keeps a stale-content bug from looking like a
        // driver problem.
        using var immutable = device.CreateBuffer(
            new BufferDesc(BufferKind.Vertex, BufferUsage.Immutable, 16), new byte[16]);
        var refused = false;
        var cb2 = device.AcquireCommandBuffer();
        try { cb2.UpdateBuffer(immutable, [1, 2, 3, 4]); }
        catch (NotSupportedException) { refused = true; }
        device.Submit(cb2);
        Report("BUFFER", "updating an immutable buffer fails loudly", refused);

        // ES 3.0 has no base-vertex draw and ANGLE exposes no extension for one, so the backend
        // refuses rather than drawing from the wrong part of the buffer.
        using var index = device.CreateBuffer(
            new BufferDesc(BufferKind.Index, BufferUsage.Immutable, 6, IndexFormat.UInt16), new byte[6]);
        var baseVertexRefused = false;
        var cb3 = device.AcquireCommandBuffer();
        try { cb3.DrawIndexed(baseVertex: 1, startIndex: 0, primitiveCount: 1); }
        catch (InvalidOperationException) { baseVertexRefused = true; }
        device.Submit(cb3);
        Report("BUFFER", "a non-zero baseVertex is refused (ES 3.0 has no base-vertex draw)", baseVertexRefused);
    }

    /// <summary>
    /// The abstraction allows one live command buffer at a time, which is the rule that makes an
    /// immediate-mode backend legal. Both halves matter: refusing a second acquisition, and *not*
    /// staying wedged afterwards. A stale submit is checked too, because a double submit would
    /// otherwise silently reset state belonging to whatever is actually live.
    /// </summary>
    private static void CheckCommandBufferLifetime(GlGraphicsDevice device)
    {
        var cb = device.AcquireCommandBuffer();
        var refused = false;
        try { device.AcquireCommandBuffer(); }
        catch (InvalidOperationException) { refused = true; }
        Report("COMMAND BUFFER", "a second live command buffer is refused", refused);
        device.Submit(cb);

        var again = device.AcquireCommandBuffer();
        device.Submit(again);
        Report("COMMAND BUFFER", "a new command buffer can be acquired after Submit (the first did not wedge it)", true);

        var live = device.AcquireCommandBuffer();
        var staleRefused = false;
        try { device.Submit(again); }
        catch (InvalidOperationException) { staleRefused = true; }
        Report("COMMAND BUFFER", "submitting an already-submitted command buffer is refused", staleRefused);
        device.Submit(live);
    }

    /// <summary>
    /// An off-screen target: colour plus depth-stencil attachments, a clear, and a readback.
    ///
    /// The readback is the interesting half. It proves the clear actually reached colour attachment
    /// 0 - which a missing glDrawBuffers silently prevents, giving a black target and no error - and
    /// it is also the check that the depth-stencil texture is refused by name rather than read as if
    /// it had colour storage.
    /// </summary>
    private static void CheckRenderTarget(GlGraphicsDevice device)
    {
        const int size = 32;
        using var target = device.CreateRenderTarget(
            new RenderTargetDesc(size, size, TextureFormat.Rgba8, HasDepthStencil: true));
        Report("RENDER TARGET", "colour + depth-stencil attachments, framebuffer complete",
            target.ColorTexture is not null && target.DepthStencilTexture is not null);

        var cb = device.AcquireCommandBuffer();
        cb.SetRenderTarget(target);
        cb.SetViewport(new Viewport(0, 0, size, size));
        cb.Clear(ClearOptions.Color | ClearOptions.Depth, new ColorRgba(0.1f, 0.7f, 0.3f));
        cb.SetRenderTarget(null);
        cb.SetViewport(new Viewport(0, 0, device.Swapchain.Width, device.Swapchain.Height));
        device.Submit(cb);
        DrainGlErrors("render target clear");

        var pixels = new byte[size * size * 4];
        // Asserted non-null rather than compiler-proven: IRenderTarget models ColorTexture as
        // nullable, but GlRenderTarget always creates one.
        device.ReadTexture(target.ColorTexture!, 0, 0, size, size, pixels);

        var centre = (size / 2 * size + size / 2) * 4;
        var expected = ((byte)Math.Round(0.1f * 255), (byte)Math.Round(0.7f * 255), (byte)Math.Round(0.3f * 255));
        Report("RENDER TARGET", $"off-screen clear read back as ({pixels[centre]},{pixels[centre + 1]},{pixels[centre + 2]})",
            Math.Abs(pixels[centre] - expected.Item1) <= 1 &&
            Math.Abs(pixels[centre + 1] - expected.Item2) <= 1 &&
            Math.Abs(pixels[centre + 2] - expected.Item3) <= 1);
        DrainGlErrors("render target readback");

        var depthRefused = false;
        try { device.ReadTexture(target.DepthStencilTexture!, 0, 0, size, size, pixels); }
        catch (NotSupportedException) { depthRefused = true; }
        Report("RENDER TARGET", "reading the depth-stencil texture fails loudly", depthRefused);
    }

    /// <summary>
    /// The check this backend was most at risk of failing, run for every generated bundle.
    ///
    /// Each bundle's ES GLSL is compiled and linked, and then two things about the result are
    /// compared against the reflection the bundle carries:
    ///
    ///  - every reflected texture must resolve to a real sampler-uniform location. A -1 is the
    ///    <c>_1062</c>/<c>ShadowMap0</c> mismatch coming back, and it is silent at draw time.
    ///  - the driver's std140 block layout must agree with the reflection's byte offsets, because
    ///    those offsets are what SetUniform writes to and what the D3D packoffset layout the bundles
    ///    were generated against assumed. std140 and packoffset agree for this shader set; that is
    ///    what this asserts rather than takes on faith. The block sizes it confirms are Ground 372,
    ///    ImGui 64, Line 580, Mountains 372, Nvg 264, Particle 192, Poly 524 and Sky 64 bytes.
    ///
    /// A link failure is separately meaningful here: ES 3.0 links varyings by name and has no
    /// layout(location) on them, so a mismatch between the two stages fails at link and never at
    /// compile - see the shader compiler's HarmonizeVaryingNames.
    /// </summary>
    private static void CheckBundlePrograms(GlGraphicsDevice device)
    {
        foreach (var bundle in Bundles)
        {
            using var pipeline = device.CreatePipeline(new PipelineDesc(
                VertexShader: bundle.Module,
                PixelShader: bundle.Module,
                VertexLayouts: bundle.Layouts,
                BlendState: BlendStateDesc.Opaque,
                DepthStencilState: DepthStencilStateDesc.Default,
                RasterizerState: RasterizerStateDesc.Default with { CullMode = CullMode.None },
                Topology: PrimitiveTopology.TriangleList));

            var program = ((GlPipelineState)pipeline).Program;
            var reflection = bundle.Reflection;

            Report("BUNDLE", $"{bundle.Name}: ES 3.0 vertex+pixel stages compile and link", true);

            CheckSamplers(bundle.Name, program, reflection);
            CheckUniformBlock(bundle.Name, program, reflection);
            CheckVertexLayout(bundle.Name, program, bundle.Layouts);
        }

        DrainGlErrors("generated bundles");
    }

    /// <summary>
    /// The NameCombinedSamplers proof: every texture the reflection names must resolve to a real
    /// sampler-uniform location in the linked program.
    ///
    /// The locations are printed rather than only their pass/fail, because they are what the draw
    /// path hands to glUniform1 and seeing three distinct non-negative values is what makes a
    /// failure legible. A bundle with no textures legitimately has nothing here - Particle and Sky
    /// reflect none.
    ///
    /// The count the program declares is compared against the reflection's first, and that
    /// comparison is not a formality - it is what stops this check from reporting a false failure.
    /// A reflection lists what the source declares; the linked program keeps only what the compiled
    /// entry point references. Nvg is built from its <c>PSMainSimple</c> entry point, which samples
    /// nothing, so its reflection names <c>g_texture</c> while the program declares no sampler at
    /// all - and a -1 there is correct, not a rename that failed. Only when the reflection names
    /// <em>more</em> samplers than the program declares is there something to prove, and then every
    /// one of them must resolve.
    /// </summary>
    private static void CheckSamplers(string name, GlShaderProgram program, ShaderReflection reflection)
    {
        if (reflection.Textures.Count == 0)
            return;

        var names = string.Join(", ", reflection.Textures.Select(t => t.Name));
        var locations = $"[{string.Join(", ", program.TextureLocations)}]";

        if (program.SamplerUniformCount < reflection.Textures.Count)
        {
            var dropped = reflection.Textures.Count - program.SamplerUniformCount;
            Report("BUNDLE", $"{name}: the linked program declares {program.SamplerUniformCount} sampler(s) " +
                            $"for the reflection's {reflection.Textures.Count} ({names}) -> locations {locations}",
                program.TextureLocations.Count(l => l >= 0) == program.SamplerUniformCount);
            Console.WriteLine($"           {dropped} reflected texture(s) were optimised out - this entry point does not sample them;");
            Console.WriteLine("           the resolve check below covers whatever the program does declare");
            return;
        }

        var unresolved = new List<string>();
        for (var i = 0; i < reflection.Textures.Count; i++)
        {
            if (program.TextureLocations[i] < 0)
                unresolved.Add(reflection.Textures[i].Name);
        }

        Report("BUNDLE", $"{name}: all {reflection.Textures.Count} sampler uniform(s) resolve ({names}) " +
                        $"-> locations {locations}",
            unresolved.Count == 0);

        if (unresolved.Count > 0)
            Console.WriteLine($"           unresolved: {string.Join(", ", unresolved)} - the ES combined sampler is not named after the texture");
    }

    /// <summary>
    /// The other half of the compiler's job: the driver's std140 layout has to be the layout the
    /// reflection's byte offsets describe, because those offsets are SetUniform's slots.
    ///
    /// The block size is checked first as an aggregate, then every member offset one by one, because
    /// the two fail differently: a size mismatch means the block itself is not the one the reflection
    /// describes, while a single wrong offset means one member - typically a vec3 sharing a register
    /// boundary - landed somewhere std140 and packoffset disagree about.
    /// </summary>
    private static void CheckUniformBlock(string name, GlShaderProgram program, ShaderReflection reflection)
    {
        if (reflection.Uniforms.Count == 0)
            return;

        var block = program.UniformBlock;
        if (block is null)
        {
            Report("BUNDLE", $"{name}: reflection declares {reflection.Uniforms.Count} uniform(s) and the linked program has a _Global block", false);
            return;
        }

        // std140 lays the block out as a byte range ending at the furthest member, and the driver may
        // report that bare end or the same value rounded to the block's 16-byte alignment. Both are
        // correct; anything else means the layouts differ.
        var end = 0;
        foreach (var uniform in reflection.Uniforms)
            end = Math.Max(end, uniform.Offset + uniform.SizeInBytes);
        var padded = (end + 15) & ~15;

        Report("BUNDLE", $"{name}: std140 block size {block.DataSize} matches the reflection's {end}" +
                        (padded != end ? $" (or its 16-byte-padded {padded})" : ""),
            block.DataSize == end || block.DataSize == padded);

        var mismatched = new List<string>();
        var missing = new List<string>();
        foreach (var uniform in reflection.Uniforms)
        {
            if (!block.MemberOffsets.TryGetValue(uniform.Name, out var offset))
                missing.Add(uniform.Name);
            else if (offset != uniform.Offset)
                mismatched.Add($"{uniform.Name}: reflection {uniform.Offset}, driver {offset}");
        }

        Report("BUNDLE", $"{name}: all {reflection.Uniforms.Count} reflected uniform offsets match the driver's std140 layout",
            mismatched.Count == 0 && missing.Count == 0);

        foreach (var mismatch in mismatched)
            Console.WriteLine($"           {mismatch}");
        if (missing.Count > 0)
            Console.WriteLine($"           not in the driver's block at all: {string.Join(", ", missing)}");
    }

    /// <summary>
    /// The layout the caller supplies must describe exactly the inputs the vertex stage declares.
    ///
    /// This is the check that keeps <c>BindVertexStreams</c>'s attribute filtering honest. That
    /// filter skips a location the linked program does not declare, because pointing a vertex array
    /// at a missing attribute raises GL_INVALID_VALUE - but the *converse* mistake is silent: a
    /// location the shader declares and the layout never reaches is left at GL's default, which is
    /// to read zeros, and a draw using it renders wrong geometry with no error at all. Since
    /// locations are assigned by position rather than by name, an off-by-one anywhere in a layout
    /// shifts every attribute after it.
    ///
    /// Handing the layout to the shader compiler's own <c>HlslSemantics</c> would be a stronger
    /// check still, but that type lives in the ShaderPoc project rather than in an abstraction this
    /// test can reference. What is available here is the linked program, which is what actually
    /// decides whether a draw works.
    /// </summary>
    private static void CheckVertexLayout(string name, GlShaderProgram program, IReadOnlyList<VertexLayoutDesc> layouts)
    {
        var declared = layouts.Sum(l => l.Attributes.Count);

        // Named by location rather than by semantic: the semantic a layout carries is only a label
        // for the register the HLSL used, and the GLSL location is what binds. The span set rather
        // than the name table, so a mat4 input counts as the four locations it occupies.
        var missing = new List<int>();
        for (var location = 0; location < declared; location++)
        {
            if (!program.AttributeLocationSpans.Contains(location))
                missing.Add(location);
        }

        // A layout may declare *more* attributes than the shader uses - the filter in
        // BindVertexStreams skips those - so the two counts are only required to agree in the other
        // direction, where a location the shader reads has nothing feeding it.
        var spans = program.AttributeLocationSpans;
        Report("BUNDLE", $"{name}: the layout's {declared} attribute(s) cover every input the vertex stage declares " +
                        $"({program.AttributeLocations.Count} input(s) over {spans.Count} location(s): " +
                        $"{string.Join(", ", program.AttributeLocations.OrderBy(a => a.Key).Select(a => a.Value))})",
            missing.Count == 0 && spans.Count <= declared);

        if (missing.Count > 0)
            Console.WriteLine($"           locations {string.Join(", ", missing)} are declared by the shader but supplied by no layout attribute");
        if (spans.Count > declared)
            Console.WriteLine($"           the stage declares {spans.Count} locations but the layouts supply only {declared}");
    }

    /// <summary>
    /// A textured draw through the real ImGui bundle, then a readback of the pixels it produced.
    ///
    /// This is the end-to-end proof that the sampler binding works. The pixel stage multiplies a
    /// vertex colour by the sampled texture, so a sampler bound to the wrong unit - or to no unit at
    /// all, which is what a -1 location produces - gives a visibly wrong pixel. The vertex colours
    /// are all white and the 2x2 texture is magenta in one column and yellow in the other, so the
    /// output is exactly the sampled texel and the two halves are unmistakable.
    ///
    /// It renders into an off-screen target rather than the swapchain, for a reason that is about the
    /// abstraction rather than convenience: ReadTexture takes an ITexture, and the default
    /// framebuffer has no texture object in ES 3.0. Reading a target's colour texture is the
    /// documented way to inspect what a draw produced.
    /// </summary>
    private static void CheckImGuiTexturedDraw(GlGraphicsDevice device)
    {
        const int size = 64;
        var program = ImGui.Create();
        // The module is not disposable: the pipeline it builds owns the linked GL program, and
        // disposes it from its own Dispose. That is the abstraction's model - see
        // GlGraphicsDevice.CreatePipeline.
        var module = GlGraphicsDevice.LoadProgram(program.Vertex, program.Pixel, program.Reflection);
        using var pipeline = device.CreatePipeline(new PipelineDesc(
            VertexShader: module,
            PixelShader: module,
            VertexLayouts: [ImGuiLayout],
            BlendState: BlendStateDesc.Opaque,
            DepthStencilState: DepthStencilStateDesc.None,
            RasterizerState: RasterizerStateDesc.Default with { CullMode = CullMode.None },
            Topology: PrimitiveTopology.TriangleList));

        using var target = device.CreateRenderTarget(new RenderTargetDesc(size, size, TextureFormat.Rgba8, HasDepthStencil: false));

        // A full-screen quad in clip space with white vertex colours, so the fragment output is the
        // sampled texel exactly. The shader multiplies by a row-major projection matrix, so the
        // identity makes the vertex positions already-clip-space.
        Span<float> identity =
        [
            1f, 0f, 0f, 0f,
            0f, 1f, 0f, 0f,
            0f, 0f, 1f, 0f,
            0f, 0f, 0f, 1f,
        ];

        // The app's real ImGui vertex: float2 position, float2 uv, byte4-normalised colour. All the
        // colours are 255 so the shader's multiply is a no-op and the output is the sampled texel.
        byte[] vertices = ImGuiVertices(
            -1f, -1f, 0f, 0f,
             1f, -1f, 1f, 0f,
             1f,  1f, 1f, 1f,
            -1f,  1f, 0f, 1f);
        Span<ushort> indices = [0, 1, 2, 0, 2, 3];

        using var vertexBuffer = device.CreateBuffer(
            new BufferDesc(BufferKind.Vertex, BufferUsage.Immutable, vertices.Length), vertices);
        using var indexBuffer = device.CreateBuffer(
            new BufferDesc(BufferKind.Index, BufferUsage.Immutable, indices.Length * sizeof(ushort), IndexFormat.UInt16),
            MemoryMarshal.AsBytes(indices));

        // Magenta in the left column, yellow in the right. Both rows are identical, so the V axis
        // plays no part in the result: this says which *column* the sampler read, not how textures
        // are oriented.
        using var texture = device.CreateTexture(new TextureDesc(2, 2, TextureFormat.Rgba8), [
            255, 0, 255, 255,  255, 255, 0, 255,
            255, 0, 255, 255,  255, 255, 0, 255,
        ]);
        // Point filtering, so the sampled texel is well defined rather than an average of the four.
        using var sampler = device.CreateSampler(
            new SamplerDesc(TextureFilter.Point, TextureAddressMode.Clamp, TextureAddressMode.Clamp));

        var cb = device.AcquireCommandBuffer();
        cb.SetRenderTarget(target);
        cb.SetViewport(new Viewport(0, 0, size, size));
        cb.Clear(ClearOptions.Color, new ColorRgba(0f, 0f, 0f));
        cb.SetPipeline(pipeline);
        cb.SetUniform(program.Bind().Projection, MemoryMarshal.AsBytes(identity));
        cb.SetShaderResource(0, texture, sampler);
        cb.SetVertexBuffer(0, vertexBuffer, ImGuiStride);
        cb.SetIndexBuffer(indexBuffer);
        cb.DrawIndexed(baseVertex: 0, startIndex: 0, primitiveCount: 2);
        // Reading the target's colour texture while it is still bound is the one thing the
        // abstraction's ReadTexture documents as forbidden - the texture would be both source and
        // destination - so the target is unbound first.
        cb.SetRenderTarget(null);
        device.Submit(cb);
        DrainGlErrors("ImGui textured draw");

        var pixels = new byte[size * size * 4];
        // Asserted non-null rather than compiler-proven: IRenderTarget models ColorTexture as
        // nullable, but GlRenderTarget always creates one.
        device.ReadTexture(target.ColorTexture!, 0, 0, size, size, pixels);

        var left = PixelAt(pixels, size, size / 4, size / 2);
        var right = PixelAt(pixels, size, 3 * size / 4, size / 2);

        Report("DRAW", $"textured quad: left half sampled ({left.R},{left.G},{left.B}), expected magenta (255,0,255)",
            left.R > 200 && left.G < 60 && left.B > 200);
        Report("DRAW", $"textured quad: right half sampled ({right.R},{right.G},{right.B}), expected yellow (255,255,0)",
            right.R > 200 && right.G > 200 && right.B < 60);
        Console.WriteLine("           the two halves differing is what proves the sampler is bound to the unit the texture is on;");
        Console.WriteLine("           a black or uniformly wrong result is the -1 sampler location showing up as wrong pixels");
    }

    /// <summary>
    /// A draw through the real Line bundle - the shader the app uses for its world geometry, with
    /// three cascaded shadow maps, a 580-byte uniform block, and the app's real two-stream instanced
    /// layout: <c>LineMeshVertexAttribute</c> in slot 0 and <c>InstanceData</c> in slot 1.
    ///
    /// This covers what the ImGui draw does not: an instanced draw, a mat4 vertex input spanning four
    /// consecutive locations, and a shader with more than one texture - which is where a sampler list
    /// that resolved only its first entry would show.
    ///
    /// The draw is deliberately *not* pixel-asserted. The shader's vertex stage derives its geometry
    /// from about twenty uniforms - outline falloff distances, screen resolution, a random-expansion
    /// seed among them - and reproducing a known-visible shape from them here would test this test's
    /// model of the shader rather than the backend. What is asserted is that the draw reaches the
    /// driver without a GL error, which is a real signal in GL (unlike sokol, which logs validation
    /// failures instead of raising them) and is what would catch a bad VAO binding or a mismatched
    /// uniform block. The pixel-level proof that this backend rasterizes lives in the ImGui check.
    /// </summary>
    private static void CheckLineInstancedDraw(GlGraphicsDevice device)
    {
        var program = Line.Create();
        // Not disposable for the same reason as the ImGui module above: the pipeline owns the program.
        var module = GlGraphicsDevice.LoadProgram(program.Vertex, program.Pixel, program.Reflection);
        using var pipeline = device.CreatePipeline(new PipelineDesc(
            VertexShader: module,
            PixelShader: module,
            VertexLayouts: [LineGeometryLayout, InstanceLayout],
            BlendState: BlendStateDesc.Opaque,
            DepthStencilState: DepthStencilStateDesc.Default,
            RasterizerState: RasterizerStateDesc.Default with { CullMode = CullMode.None },
            Topology: PrimitiveTopology.TriangleList));

        // Three degenerate-ish vertices of the app's real LineMeshVertexAttribute shape. They sit at
        // the origin, so the outline expansion the shader applies moves them by a few pixels around
        // it - enough for the draw to be real work without depending on where the result lands.
        byte[] geometry = LineVertices();
        byte[] instance = InstanceDataBytes();

        using var geometryBuffer = device.CreateBuffer(
            new BufferDesc(BufferKind.Vertex, BufferUsage.Immutable, geometry.Length), geometry);
        using var instanceBuffer = device.CreateBuffer(
            new BufferDesc(BufferKind.Vertex, BufferUsage.Immutable, instance.Length), instance);
        using var indexBuffer = device.CreateBuffer(
            new BufferDesc(BufferKind.Index, BufferUsage.Immutable, 3 * sizeof(ushort), IndexFormat.UInt16),
            MemoryMarshal.AsBytes((ReadOnlySpan<ushort>)[0, 1, 2]));
        using var white = device.CreateTexture(new TextureDesc(2, 2, TextureFormat.Rgba8), [
            255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255,
        ]);
        using var shadowSampler = device.CreateSampler(
            new SamplerDesc(TextureFilter.Point, TextureAddressMode.Clamp, TextureAddressMode.Clamp));

        var cb = device.AcquireCommandBuffer();
        cb.SetRenderTarget(null);
        cb.SetViewport(new Viewport(0, 0, device.Swapchain.Width, device.Swapchain.Height));
        cb.Clear(ClearOptions.Color | ClearOptions.Depth, new ColorRgba(0f, 0f, 0f));
        cb.SetPipeline(pipeline);

        // Identity transforms plus the hand-written defaults the app's own effect wrapper supplies,
        // so the shader's ~20 uniforms are all defined. NumCascades 0 makes the shadow branch a
        // no-op; Resolution is the half-viewport size the screen-space outline math expects.
        var p = program.Bind();
        WriteMatrix(cb, p.ViewProj);
        WriteMatrix(cb, p.Projection);
        WriteMatrix(cb, p.View);
        Write(cb, p.NumCascades, 0f);
        Write(cb, p.HalfThickness, 4f);
        Write(cb, p.Alpha, 1f);
        Write(cb, p.Darken, 1f);
        Write(cb, p.Resolution, device.Swapchain.Width / 2f, device.Swapchain.Height / 2f);

        // All three shadow maps must be bound: the shader samples them by name, and binding the full
        // reflected set is what makes the samplers above exercise real units.
        for (var slot = 0; slot < program.Reflection.Textures.Count; slot++)
            cb.SetShaderResource(slot, white, shadowSampler);

        cb.SetVertexBuffer(0, geometryBuffer, LineGeometryStride);
        cb.SetVertexBuffer(1, instanceBuffer, InstanceStride);
        cb.SetIndexBuffer(indexBuffer);

        var before = DrainGlErrors("before Line draw");
        cb.DrawIndexedInstanced(baseVertex: 0, startIndex: 0, primitiveCount: 1, instanceCount: 1);
        device.Submit(cb);
        var after = DrainGlErrors("Line instanced draw");

        Report("DRAW", $"Line instanced draw reached the driver with {after - before} GL error(s) " +
                       $"({program.Reflection.Textures.Count} shadow maps bound, {program.Reflection.Uniforms.Count} uniforms set)",
            after == before);
    }

    /// <summary>Writes a float uniform by its reflected byte offset. A name the compiler dropped resolves to -1 and is skipped.</summary>
    private static void Write(ICommandBuffer cb, int offset, params float[] values)
    {
        if (offset >= 0)
            cb.SetUniform(offset, MemoryMarshal.AsBytes(values.AsSpan()));
    }

    private static void WriteMatrix(ICommandBuffer cb, int offset) =>
        Write(cb, offset, 1f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f, 1f);

    private readonly record struct Pixel(byte R, byte G, byte B, byte A);

    /// <summary>Reads a pixel out of a tightly packed, top-down RGBA buffer.</summary>
    private static Pixel PixelAt(byte[] pixels, int width, int x, int y)
    {
        var i = (y * width + x) * 4;
        return new Pixel(pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]);
    }

    /// <summary>
    /// Packs the four corners of a full-screen quad as the app's real ImGui vertices: position and
    /// uv as floats, colour as four bytes - the layout SdlImGuiRenderer declares, not a made-up one.
    /// </summary>
    private static byte[] ImGuiVertices(params float[] positionsAndUvs)
    {
        var bytes = new byte[ImGuiStride * (positionsAndUvs.Length / 4)];
        for (var v = 0; v < positionsAndUvs.Length / 4; v++)
        {
            var at = v * ImGuiStride;
            for (var i = 0; i < 4; i++)
                BitConverter.TryWriteBytes(bytes.AsSpan(at + i * sizeof(float)), positionsAndUvs[v * 4 + i]);
            bytes[at + 16] = 255;   // R
            bytes[at + 17] = 255;   // G
            bytes[at + 18] = 255;   // B
            bytes[at + 19] = 255;   // A
        }

        return bytes;
    }

    /// <summary>Three vertices in the app's real <c>LineMeshVertexAttribute</c> shape (stride 60).</summary>
    private static byte[] LineVertices()
    {
        const int stride = LineGeometryStride;
        var bytes = new byte[stride * 3];
        for (var v = 0; v < 3; v++)
        {
            var at = v * stride;
            // PositionA at 0, PositionB at 12, Side at 24, Normal at 28, Centroid at 40,
            // colour (byte4) at 52, DecalOffset at 56.
            Write3(bytes, at + 0, 0f, 0f, 0f);      // PositionA
            Write3(bytes, at + 12, 0f, 0f, 0f);     // PositionB
            BitConverter.TryWriteBytes(bytes.AsSpan(at + 24), v == 0 ? -1f : 1f);   // Side
            Write3(bytes, at + 28, 0f, 1f, 0f);     // Normal
            Write3(bytes, at + 40, 0f, 0f, 0f);     // Centroid
            bytes[at + 52] = 255; bytes[at + 53] = 255; bytes[at + 54] = 255; bytes[at + 55] = 255;
            BitConverter.TryWriteBytes(bytes.AsSpan(at + 56), 0f);                  // DecalOffset
        }

        return bytes;

        static void Write3(byte[] destination, int offset, float x, float y, float z)
        {
            BitConverter.TryWriteBytes(destination.AsSpan(offset + 0), x);
            BitConverter.TryWriteBytes(destination.AsSpan(offset + 4), y);
            BitConverter.TryWriteBytes(destination.AsSpan(offset + 8), z);
        }
    }

    /// <summary>
    /// One instance in the app's real <c>InstanceData</c> shape (stride 96): an identity world matrix
    /// - already transposed, which is how the struct stores it - then the two parameter packs at zero,
    /// which is what the app passes for a plain, unshadowed, full-alpha object.
    /// </summary>
    private static byte[] InstanceDataBytes()
    {
        var bytes = new byte[InstanceStride];
        float[] values = [1f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f, 1f];
        for (var i = 0; i < values.Length; i++)
            BitConverter.TryWriteBytes(bytes.AsSpan(i * sizeof(float)), values[i]);
        return bytes;
    }

    /// <summary>
    /// Drains glGetError and reports anything raised since the last call, returning the count.
    ///
    /// GL accumulates errors in a queue, so one drain step per error is right - and the loop is
    /// bounded because a driver that returned errors forever would otherwise hang the test. This is
    /// what replaces sokol's error-counting logger: with GL, every command that can fail does so by
    /// setting this flag, and nothing else in the process looks at it.
    /// </summary>
    private static int DrainGlErrors(string where)
    {
        var errors = new List<GLEnum>();
        for (var i = 0; i < 32; i++)
        {
            var error = _gl.GetError();
            if (error == GLEnum.NoError) break;
            errors.Add(error);
        }

        if (errors.Count > 0)
        {
            _failures++;
            Console.WriteLine($"GL ERROR after {where}: {string.Join(", ", errors)}");
        }

        return errors.Count;
    }

    private static void Report(string category, string what, bool ok)
    {
        if (!ok) _failures++;
        Console.WriteLine($"{category}: {(ok ? "ok" : "<-- FAIL")} - {what}");
    }

    /// <summary>
    /// Every generated bundle, with the vertex layout its input signature implies.
    ///
    /// The layouts are the app's own definitions rather than hand-written stand-ins, because a
    /// layout the app does not use would prove nothing about the app: ImGui's is
    /// SdlImGuiRenderer's, Ground/Mountains/Sky share PositionColorVertex's, Poly is Mesh's
    /// VertexPositionNormalColorCentroid, Nvg is AbstractionNvgRenderer's, and Line is
    /// LineMeshVertexAttribute plus InstanceData. Particle is given Ground's layout, which is wrong
    /// for it in isolation but harmless here - this check builds a pipeline and links it, it never
    /// binds a stream.
    ///
    /// Every layout below is nonetheless checked against the linked program, because a layout that
    /// disagrees with its shader fails *silently* rather than loudly: an attribute the layout never
    /// reaches is left at GL's default of reading zeros, so the draw renders wrong geometry with no
    /// error reported. That is what CheckVertexLayout exists to catch, and it is why Particle's
    /// borrowed layout is legal here only as far as it agrees with Particle's own input signature -
    /// which it does, both being two float4 inputs.
    ///
    /// The bundles these come from were built by `scratch/ShaderPoc/build-all.sh`, one entry-point
    /// pair each, and that choice is visible in the results: Nvg is compiled from `PSMainSimple`,
    /// which samples no texture, so its reflection names `g_texture` while the linked program
    /// declares no sampler at all.
    ///
    /// Attribute locations are assigned by *position*: glslang numbers the HLSL front-end's inputs in
    /// declaration order, ignoring the D3D semantic indices, so what matters is the order of the GLSL
    /// `layout(location = ...)` declarations. All the layouts below are already in that order.
    /// </summary>
    private static IEnumerable<Bundle> Bundles
    {
        get
        {
            var imgui = ImGui.Create();
            yield return new Bundle("ImGui", imgui.Vertex, imgui.Pixel, imgui.Reflection, [ImGuiLayout]);

            var ground = Ground.Create();
            yield return new Bundle("Ground", ground.Vertex, ground.Pixel, ground.Reflection, [PositionColorLayout]);

            var mountains = Mountains.Create();
            yield return new Bundle("Mountains", mountains.Vertex, mountains.Pixel, mountains.Reflection, [PositionColorLayout]);

            var sky = Sky.Create();
            yield return new Bundle("Sky", sky.Vertex, sky.Pixel, sky.Reflection, [PositionColorLayout]);

            var poly = Poly.Create();
            yield return new Bundle("Poly", poly.Vertex, poly.Pixel, poly.Reflection, [PolyLayout, InstanceLayout]);

            var nvg = Nvg.Create();
            yield return new Bundle("Nvg", nvg.Vertex, nvg.Pixel, nvg.Reflection, [NvgLayout]);

            var particle = Particle.Create();
            yield return new Bundle("Particle", particle.Vertex, particle.Pixel, particle.Reflection, [PositionColorLayout]);
        }
    }

    private sealed record Bundle(
        string Name,
        ShaderStageSources Vertex,
        ShaderStageSources Pixel,
        ShaderReflection Reflection,
        IReadOnlyList<VertexLayoutDesc> Layouts)
    {
        public IShaderModule Module { get; } = GlGraphicsDevice.LoadProgram(Vertex, Pixel, Reflection);
    }

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
    /// not the GLSL's location order, so this is the interesting case: the layout is written in
    /// location order, and the semantic indices only label the registers the HLSL used.
    /// </summary>
    private const int LineGeometryStride = 60;

    private static VertexLayoutDesc LineGeometryLayout => new(
        Attributes:
        [
            new VertexAttributeDesc("POSITION", 0, 0, VertexAttributeFormat.Float3),      // location 0
            new VertexAttributeDesc("POSITION", 1, 12, VertexAttributeFormat.Float3),     // location 1
            new VertexAttributeDesc("TEXCOORD", 0, 24, VertexAttributeFormat.Float1),     // location 2
            new VertexAttributeDesc("NORMAL", 0, 28, VertexAttributeFormat.Float3),       // location 3
            new VertexAttributeDesc("POSITION", 2, 40, VertexAttributeFormat.Float3),     // location 4
            new VertexAttributeDesc("COLOR", 0, 52, VertexAttributeFormat.Byte4Normalized), // location 5
            new VertexAttributeDesc("TEXCOORD", 1, 56, VertexAttributeFormat.Float1),     // location 6
        ],
        StrideInBytes: LineGeometryStride);

    /// <summary>Bytes per Poly vertex - Mesh.VertexPositionNormalColorCentroid: three float3s, packed byte4 colour, float.</summary>
    private const int PolyStride = 44;

    private static VertexLayoutDesc PolyLayout => new(
        Attributes:
        [
            new VertexAttributeDesc("POSITION", 0, 0, VertexAttributeFormat.Float3),      // location 0
            new VertexAttributeDesc("NORMAL", 0, 12, VertexAttributeFormat.Float3),       // location 1
            new VertexAttributeDesc("POSITION", 1, 24, VertexAttributeFormat.Float3),     // location 2
            new VertexAttributeDesc("COLOR", 0, 36, VertexAttributeFormat.Byte4Normalized), // location 3
            new VertexAttributeDesc("TEXCOORD", 0, 40, VertexAttributeFormat.Float1),     // location 4
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
    /// mat4 input Poly's and Line's vertex stages declare at locations 5..8 and 7..10 respectively,
    /// pre-expanded into one float4 attribute per location, exactly as fxc expands a
    /// <c>float4x4 world : TEXCOORD3</c>.
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
