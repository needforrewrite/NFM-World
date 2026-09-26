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
using NFMWorld.Shaders.Generated;
using Silk.NET.OpenGLES;
using ClearOptions = NFMWorld.Graphics.ClearOptions;

namespace NFMWorld.Graphics.OpenGL.Smoke;

internal static class Program
{
    private const int Width = 256;
    private const int Height = 256;

    private static int _failures;

    private static GL _gl = null!;

    private static int Main(string[] args)
    {
        // The same --angle-backend=/--angle-device= flags the game takes, so the driver knob can be
        // exercised here - headlessly, with no window and no UI - which is the only way to check
        // that the attribute list reaches ANGLE at all. The check is the GL_RENDERER line below:
        // run it once with the default and once with a different backend, and the strings must
        // differ. Both runs reporting the same renderer is exactly the bug this replaces, where the
        // platform type was ANGLE's null token and every selection collapsed onto one backend.
        var selection = AngleSelection.Parse(args);

        using var device = GlGraphicsDevice.CreateHeadless(Width, Height, selection);
        _gl = device.Gl;
        ReportInterface(device, selection);

        CheckTextureRoundTrip(device);
        CheckMipmappedTexture(device);
        CheckCompressedRefusal(device);
        CheckBuffers(device);
        CheckCommandBufferLifetime(device);
        CheckRenderTarget(device);
        CheckProgramBinary(device);
        MeasureAposCompile(device);
        CheckBundlePrograms(device);
        CheckImGuiTexturedDraw(device);
        CheckMatrixUniformOrientation(device);
        CheckBaseVertexDraw(device);
        CheckLineInstancedDraw(device);
        CheckShadowCascadePass(device);

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
    private static void ReportInterface(GlGraphicsDevice device, AngleSelection selection)
    {
        Console.WriteLine($"DEVICE: requested {selection.Describe()}");
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

    /// <summary>
    /// Reads a buffer back through glMapBufferRange.
    ///
    /// Mapping stalls until anything pending on the buffer has landed, so this is a synchronisation
    /// point rather than a cheap read - fine for a check that runs once, and the reason the
    /// abstraction does not expose buffer readback at all. glGetBufferSubData is the desktop-GL
    /// spelling and does not exist in ES 3.0.
    /// </summary>
    private static unsafe byte[] ReadBufferBytes(GlGraphicsDevice device, IBuffer buffer)
    {
        var gl = device.Gl;
        var glBuffer = (GlBuffer)buffer;
        var target = glBuffer.Kind.ToTarget();
        var bytes = new byte[glBuffer.SizeInBytes];

        gl.BindBuffer(target, glBuffer.Handle);
        var mapped = (byte*)gl.MapBufferRange(target, 0, (nuint)bytes.Length, MapBufferAccessMask.ReadBit);
        if (mapped is not null)
        {
            new ReadOnlySpan<byte>(mapped, bytes.Length).CopyTo(bytes);
            gl.UnmapBuffer(target);
        }
        gl.BindBuffer(target, 0);

        return bytes;
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

        // The calls above only prove they are accepted. What matters is the bytes, because the whole
        // buffer now reaches the GPU as one BufferData - a discard - with the CPU mirror as the
        // source, and it is the mirror that makes the narrow write non-destructive.
        //
        //   whole write   5f,6f,7f,8f  ->  00 00 A0 40 | 00 00 C0 40 | 00 00 E0 40 | 00 00 00 41
        //   sub-range     4 bytes @4   ->  AA AA AA AA  (replaces the 6f in bytes 4..7)
        //
        // So the readback must be 5f, 0xAAAAAAAA, 7f, 8f. The pattern is not a number anyone would
        // choose - it is the point: 0xAAAAAAAA is no float, so it cannot be confused with a real
        // value, and its being at index 1 with 5f before and 7f/8f after is what proves the sub-range
        // write landed where it was asked to *and* that the mirror preserved its surroundings. A
        // regression to a whole-buffer overwrite would lose the 5f and the 8f; a wrong offset would
        // put the pattern somewhere else.
        var floats = MemoryMarshal.Cast<byte, float>((ReadOnlySpan<byte>)ReadBufferBytes(device, dynamic));
        var pattern = BitConverter.Int32BitsToSingle(unchecked((int)0xAAAAAAAA));
        Report("BUFFER", $"a whole write then a 4-byte sub-range write read back as " +
                         $"{floats[0]}f, {floats[1]}f, {floats[2]}f, {floats[3]}f, " +
                         $"expected 5f, {pattern}f, 7f, 8f",
            floats.Length == 4 && floats[0] == 5f && floats[1] == pattern
                                && float.IsNaN(floats[1]) == false
                                && floats[2] == 7f && floats[3] == 8f);

        // Each update reaches the GPU on its own - nothing is held back for a later draw - so the two
        // orders a caller can write them in agree. Worth pinning down because a deferred scheme gets
        // exactly one of these wrong: flushing only at the bind loses the second write, and flushing
        // only at the draw loses both until something draws.
        var cb3 = device.AcquireCommandBuffer();
        cb3.UpdateBuffer(dynamic, MemoryMarshal.AsBytes((ReadOnlySpan<float>)[1f, 1f, 1f, 1f]));
        cb3.SetVertexBuffer(0, dynamic, strideBytes: sizeof(float));
        cb3.UpdateBuffer(dynamic, MemoryMarshal.AsBytes((ReadOnlySpan<float>)[9f]), offsetBytes: 0);
        device.Submit(cb3);
        var afterBind = MemoryMarshal.Cast<byte, float>((ReadOnlySpan<byte>)ReadBufferBytes(device, dynamic));
        Report("BUFFER", $"sub-range writes before and after the bind both land " +
                         $"(read back {afterBind[0]}f, {afterBind[3]}f, expected 9f, 1f)",
            afterBind[0] == 9f && afterBind[3] == 1f);
        DrainGlErrors("buffer readback");

        // An update is sent even when nothing draws the buffer, which is the case a flush hooked to
        // the draw would lose. No pipeline is bound here, so if the only upload path ran at draw time
        // this buffer would still hold the zeros it was created with.
        using var undrawn = device.CreateBuffer(
            new BufferDesc(BufferKind.Vertex, BufferUsage.Dynamic, 4 * sizeof(float)), new byte[16]);
        var cb4 = device.AcquireCommandBuffer();
        cb4.UpdateBuffer(undrawn, MemoryMarshal.AsBytes((ReadOnlySpan<float>)[2f, 3f, 4f, 5f]));
        device.Submit(cb4);
        var undrawnFloats = MemoryMarshal.Cast<byte, float>((ReadOnlySpan<byte>)ReadBufferBytes(device, undrawn));
        Report("BUFFER", $"a write to a buffer the frame never draws is still uploaded " +
                         $"(read back {undrawnFloats[0]}f, expected 2f)",
            undrawnFloats[0] == 2f && undrawnFloats[3] == 5f);
        DrainGlErrors("undrawn buffer readback");

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

        // There was a base-vertex assertion here that caught InvalidOperationException around a bare
        // DrawIndexed and read as "a non-zero baseVertex is refused". It was passing for the wrong
        // reason: with no pipeline bound, RequirePipeline throws that same exception type, so it
        // would have gone on passing even if base-vertex handling were deleted outright. The real
        // question - that a base vertex is honoured, or refused rather than silently misapplied - is
        // checked in CheckBaseVertexDraw, where a pipeline is actually bound.
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
    /// <summary>
    /// Probes whether this context can round-trip a program binary, which is what a shader cache
    /// would be built on.
    ///
    /// Reported rather than asserted: the point is to find out what the driver actually does, and a
    /// hard assert here would only encode a guess. The round trip is the real check - a format that
    /// is advertised but cannot be loaded back would be worse than none.
    /// </summary>
    private static unsafe void CheckProgramBinary(GlGraphicsDevice device)
    {
        var gl = device.Gl;

        var extensionPointer = gl.GetString(StringName.Extensions);
        var extensions = extensionPointer is null ? "(null)" : Marshal.PtrToStringUTF8((nint)extensionPointer) ?? "(unreadable)";
        Console.WriteLine($"BINARY: OES_get_program_binary = {extensions.Contains("GL_OES_get_program_binary")}");
        Console.WriteLine($"BINARY: ANGLE_program_binary  = {extensions.Contains("GL_ANGLE_program_binary")}");

        if (!gl.TryGetExtension(out Silk.NET.OpenGLES.Extensions.OES.OesGetProgramBinary ext))
        {
            Console.WriteLine("BINARY: OES_get_program_binary entry points NOT resolvable on this context");
            return;
        }

        Console.WriteLine("BINARY: OES_get_program_binary entry points resolved");

        var formats = stackalloc int[8];
        gl.GetInteger((GLEnum)Silk.NET.OpenGLES.Extensions.OES.OES.NumProgramBinaryFormatsOes, formats);
        var count = formats[0];
        Console.WriteLine($"BINARY: NUM_PROGRAM_BINARY_FORMATS = {count}");
        for (var i = 0; i < 8; i++) formats[i] = 0;
        gl.GetInteger((GLEnum)Silk.NET.OpenGLES.Extensions.OES.OES.ProgramBinaryFormatsOes, formats);
        Console.WriteLine($"BINARY: PROGRAM_BINARY_FORMATS[0] = 0x{formats[0]:X4}");

        if (count <= 0)
        {
            Console.WriteLine("BINARY: no binary formats advertised, cache cannot be enabled here");
            Report("BINARY", "a program binary format is advertised", false);
            return;
        }
        var advertisedFormat = (Silk.NET.OpenGLES.Extensions.OES.OES)formats[0];

        // A real program from a real generated bundle, so the round trip is measured on the same
        // kind of input the cache would see.
        var bundle = Bundles.First();
        using var pipeline = device.CreatePipeline(new PipelineDesc(
            VertexShader: bundle.Module,
            PixelShader: bundle.Module,
            VertexLayouts: bundle.Layouts,
            BlendState: BlendStateDesc.Opaque,
            DepthStencilState: DepthStencilStateDesc.Default,
            RasterizerState: RasterizerStateDesc.Default with { CullMode = CullMode.None },
            Topology: PrimitiveTopology.TriangleList));

        var program = ((GlPipelineState)pipeline).Program;
        var before = DrainGlErrors("program binary: before get");

        var length = 0;
        gl.GetProgram(program.Handle, (GLEnum)Silk.NET.OpenGLES.Extensions.OES.OES.ProgramBinaryLengthOes, &length);
        Console.WriteLine($"BINARY: {bundle.Name} PROGRAM_BINARY_LENGTH = {length}");

        if (length <= 0)
        {
            Console.WriteLine($"BINARY: no binary available (length {length}), errors before = {before}");
            Report("BINARY", $"{bundle.Name}: glGetProgramBinaryOES produced a usable blob", false);
            return;
        }

        var blob = new byte[length];
        uint written = 0;
        var format = advertisedFormat;
        fixed (byte* p = blob)
        {
            ext.GetProgramBinary(program.Handle, (uint)length, &written, &format, p);
        }
        var getErrors = DrainGlErrors("program binary: get");
        Console.WriteLine($"BINARY: got {written} byte(s), format 0x{(int)format:X4}, errors = {getErrors}");

        // Load it into a fresh program object and confirm it links from the blob alone.
        var restored = gl.CreateProgram();
        fixed (byte* p = blob)
        {
            ext.ProgramBinary(restored, format, p, (int)written);
        }

        var linkStatus = 0;
        gl.GetProgram(restored, GLEnum.LinkStatus, &linkStatus);
        var loadErrors = DrainGlErrors("program binary: load");
        Console.WriteLine($"BINARY: restored LINK_STATUS = {(linkStatus != 0 ? "TRUE" : "FALSE")}, errors = {loadErrors}");

        Report("BINARY", $"{bundle.Name}: binary round-trips into a linking program",
            written > 0 && linkStatus != 0);

        gl.DeleteProgram(restored);
    }

    /// <summary>
    /// Where the apos-shapes shader's compile time actually lands.
    ///
    /// Reported rather than asserted, and split into three numbers on purpose, because ANGLE
    /// defers: glCompileShader/glLinkProgram return in a fraction of a millisecond and the real
    /// translation happens when the program is first made current for a draw. Timing the
    /// constructor alone would therefore measure nothing and read as though this shader were
    /// cheap. The draw is the number that matters - it is what a shader cache has to remove.
    /// </summary>
    /// <summary>
    /// How long Apos.Shapes' shader takes to compile on this driver. Reported, never asserted.
    ///
    /// The number is not a property of this backend, which is why it must not gate the run: on ANGLE
    /// over D3D11 it is 130 seconds *per* stage translation - ANGLE's HLSL translator is quadratic
    /// enough on this shader's unrolled loops that it dominates the whole test, and a run that took
    /// ~1 s before is minutes with it. Treating that as a failure would make this suite unpassable on
    /// a correct driver, so the check is the timer line and nothing else.
    ///
    /// It is also why the GL error drain below ignores what it finds. The budget, not the shader, is
    /// what fails here, so a drained error says the compile was cut short and not that the backend
    /// misissued a call - and asserting on it would report the timeout as a backend bug.
    /// </summary>
    /// <summary>
    /// The cache's two-run story, through the real <see cref="GlProgramCache"/> rather than a
    /// reimplementation of it: the first <c>ShapeBatch</c> on a cold cache pays the full link and
    /// writes a binary, the second one - a fresh batch, a fresh program - loads it instead.
    ///
    /// Reported rather than asserted because both runs are legitimate: a cold run has to be slow, a
    /// warm one fast, and the test is what makes the difference visible. The numbers this produced
    /// are the reason the cache exists - roughly 140 s cold against 1 ms restored - and the warmup
    /// figure in the middle is what rules out ANGLE simply deferring the work to the first draw.
    /// </summary>
    private static void MeasureAposCompile(GlGraphicsDevice device)
    {
        var cold = TimeBatch(device, "cold");
        var warm = TimeBatch(device, "warm");

        Console.WriteLine($"APOS: cold {cold} ms, warm {warm} ms (cache {(warm < cold / 2 ? "helps" : "does NOT help")})");

        // Drained through GetError directly rather than through DrainGlErrors, which counts what it
        // finds as a failure. A link that runs this long leaves GL_INVALID_OPERATION behind under
        // ANGLE, and that is the driver answering its own timeout rather than the backend
        // misissuing a call. Leaving it undrained is not an option - the next check would read it as
        // its own and fail for the wrong reason - so it is cleared, named, and not counted.
        var drained = 0;
        while (_gl.GetError() != GLEnum.NoError && ++drained < 32) { }
        if (drained > 0)
            Console.WriteLine($"APOS: cleared {drained} GL error(s), not counted as failures");
    }

    private static long TimeBatch(GlGraphicsDevice device, string label)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();

        using var batch = new Apos.Shapes.ShapeBatch(device, warmup: false);
        var constructed = watch.ElapsedMilliseconds;

        // Warmup is the first draw, and so the driver's first chance to translate the program - it is
        // what separates "the constructor linked it" from "ANGLE deferred it to here".
        batch.Warmup();
        var warmed = watch.ElapsedMilliseconds;

        Console.WriteLine($"APOS {label}: create+link {constructed} ms, first draw {warmed - constructed} ms");
        return warmed;
    }

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
    /// check still, but that type is a build-time one (NFMWorld.ShaderCompiler) rather than part of
    /// an abstraction this runtime test references. What is available here is the linked program,
    /// which is what actually decides whether a draw works.
    /// </summary>
    private static void CheckVertexLayout(string name, GlShaderProgram program, IReadOnlyList<VertexLayoutDesc> layouts)
    {
        // Locations are numbered by position, so the layouts reach 0..declared-1 and "is this input
        // supplied" is simply a range test. The span set rather than the name table, so a mat4 input
        // counts as the four locations it occupies.
        var declared = layouts.Sum(l => l.Attributes.Count);
        var spans = program.AttributeLocationSpans;

        var unsupplied = spans.Where(location => location >= declared).OrderBy(location => location).ToList();

        Report("BUNDLE", $"{name}: the layouts' {declared} attribute(s) cover every input the vertex stage declares " +
                        $"({program.AttributeLocations.Count} input(s) over {spans.Count} location(s): " +
                        $"{string.Join(", ", program.AttributeLocations.OrderBy(a => a.Key).Select(a => a.Value))})",
            unsupplied.Count == 0);

        if (unsupplied.Count > 0)
            Console.WriteLine($"           locations {string.Join(", ", unsupplied)} are read by the shader but supplied by no layout attribute");

        // The other direction is legal and expected, so it is reported rather than failed on: a
        // layout may declare more attributes than the entry point uses, and BindVertexStreams' filter
        // skips the extras. That filter is not an optimisation - pointing a vertex array at a missing
        // attribute raises GL_INVALID_VALUE - so the surplus is required to be tolerated. Poly's two
        // techniques are the standing example: both are built with the app's one vertex layout, but
        // CreateShadowMap's vertex stage reads only the position and the instance matrix, so six of
        // the eleven attributes it is handed have no location to bind to. An earlier version of this
        // check asserted the converse - that the layout have no surplus at all - which only held
        // while every bundle was the one technique that uses all eleven.
        else if (declared > spans.Count)
            Console.WriteLine($"           {declared - spans.Count} layout attribute(s) are dropped by BindVertexStreams - this entry point does not read them");
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
        var program = ImGuiFullbright.Create();
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
        // Through the generated parameter, which is the same path the app uses: it owns the byte
        // offset and no-ops on a -1 (a uniform this entry point does not reference), so the test
        // does not have to know which of those happened.
        program.Bind().Projection.SetValue(cb, identity);
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
        var program = LineBasic.Create();
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

        // The shader's transforms are row-major and it does its own projection, so the identity
        // stands in for all three and leaves the geometry untransformed.
        Span<float> identity =
        [
            1f, 0f, 0f, 0f,
            0f, 1f, 0f, 0f,
            0f, 0f, 1f, 0f,
            0f, 0f, 0f, 1f,
        ];

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
        p.ViewProj.SetValue(cb, identity);
        p.Projection.SetValue(cb, identity);
        p.View.SetValue(cb, identity);
        p.NumCascades.SetValue(cb, 0f);
        p.HalfThickness.SetValue(cb, 4f);
        p.Alpha.SetValue(cb, 1f);
        p.Darken.SetValue(cb, 1f);
        p.Resolution.SetValue(cb, device.Swapchain.Width / 2f, device.Swapchain.Height / 2f);

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

    /// <summary>
    /// The shadow-cascade pass, reproduced end to end: the app's real cascade render target
    /// (<see cref="TextureFormat.Single"/> colour + Depth24Stencil8), the app's real
    /// <c>PolyCreateShadowMap</c> program, the app's real white clear, and the app's real
    /// <c>ReadTexture</c> readback - which is the whole pipeline <c>r_debugdisplay</c> shows.
    ///
    /// This exists because a cascade that comes back uniformly white is the one failure the rest of
    /// this file cannot see. Every check above asserts either "no GL error" or "the pixels I drew
    /// landed where I put them"; none of them covers a pass whose colour attachment has *never been
    /// written*, which is exactly what an all-white cascade means - white is the clear
    /// (<c>Scene.Render</c> clears every cascade to (1,1,1,1)), so an unwritten cascade and a
    /// correctly-lit one are the same image. What distinguishes them is a texel that geometry should
    /// have covered coming back with depth in it instead of 1.0.
    ///
    /// The camera and the geometry are the app's real ones, and getting them wrong is the trap this
    /// check was written twice to avoid. <c>BaseStageRenderingPhase</c> puts the light at the player
    /// minus 5000 in Y, looking at the player plus <c>(1,0,0)</c>, with the inherited <c>Up</c> of
    /// <c>-UnitY</c> - and because the look direction is 5000 units down against 1 unit sideways, the
    /// resulting basis is a camera looking almost straight down, with world +Z as its up and world +Y
    /// as its depth. Ground is therefore the face-on geometry. The first version of this check used a
    /// *vertical* wall instead, which that camera sees nearly edge-on, and "reproduced" an empty shadow
    /// map that was in fact correct - which is the whole reason the geometry here is stated in terms
    /// of the resulting basis rather than of the light's point.
    ///
    /// Two texels are asserted, one inside the square and one past its edge. A written texel alone
    /// would also be satisfied by a map that had been uniformly filled, and a clear texel alone by a
    /// pass that wrote nothing at all; together they pin down "wrote depth exactly where the geometry
    /// is", which is what the cascade should contain.
    /// </summary>
    private static void CheckShadowCascadePass(GlGraphicsDevice device)
    {
        const int resolution = 256;

        var program = PolyCreateShadowMap.Create();
        var module = GlGraphicsDevice.LoadProgram(program.Vertex, program.Pixel, program.Reflection);
        using var pipeline = device.CreatePipeline(new PipelineDesc(
            VertexShader: module,
            PixelShader: module,
            VertexLayouts: [PolyLayout, InstanceLayout],
            BlendState: BlendStateDesc.Opaque,
            DepthStencilState: DepthStencilStateDesc.Default,
            RasterizerState: RasterizerStateDesc.Default with { CullMode = CullMode.None, ScissorTestEnabled = true },
            Topology: PrimitiveTopology.TriangleList));

        // The app's own cascade target: R32F colour plus a depth-stencil attachment.
        using var cascade = device.CreateRenderTarget(new RenderTargetDesc(
            resolution, resolution, TextureFormat.Single,
            HasDepthStencil: true, TextureFormat.Depth24Stencil8));

        // The light camera, built as OrthoLightCamera does on top of the app's own values: a
        // 3000x3000 orthographic box (Near 50, Far 1e6), then the view matrix snapped to shadow-map
        // texels. The snap is a translation in view space of under one texel, so it cannot move the
        // cell off the region asserted below.
        const float orthoExtent = 3000f;
        var lightProjection = System.Numerics.Matrix4x4.CreateOrthographic(orthoExtent, orthoExtent, 50f, 1_000_000f);

        // Position, LookAt and Up are the app's: BaseStageRenderingPhase puts the light at the player
        // minus 5000 in Y, looking at the player + (1,0,0), and Camera's default Up is -UnitY.
        // Taking the player as the origin gives the two points below. The resulting basis is
        // right = -Z, up = +X, forward = -Y.
        var lightView = System.Numerics.Matrix4x4.CreateLookAt(
            new System.Numerics.Vector3(0f, -5000f, 0f),
            new System.Numerics.Vector3(1f, 0f, 0f),
            new System.Numerics.Vector3(0f, -1f, 0f));
        var texelSize = orthoExtent / resolution;
        var originInView = System.Numerics.Vector3.Transform(System.Numerics.Vector3.Zero, lightView);
        lightView *= System.Numerics.Matrix4x4.CreateTranslation(
            MathF.Floor(originInView.X / texelSize) * texelSize - originInView.X,
            MathF.Floor(originInView.Y / texelSize) * texelSize - originInView.Y,
            0f);

        // The view matrix's rows, in the orientation that matters here. They are what the assertions
        // below map a world point through, so the derivation is checked against them rather than
        // trusted - a transposed row would otherwise turn into an assertion about a texel nobody wrote.
        var upAxis = new System.Numerics.Vector3(lightView.M21, lightView.M22, lightView.M23);
        var rightAxis = new System.Numerics.Vector3(lightView.M11, lightView.M12, lightView.M13);

        // An asymmetric patch: it reaches further along +X than -X, and further along +Z than -Z. The
        // asymmetry is the point - see the sampling check at the end, which is the one that can tell a
        // mirrored lookup from a correct one.
        const float extentX = 600f;
        const float extentZ = 400f;
        const float minusX = -200f;
        const float minusZ = -1000f;
        byte[] geometry = PolyGround(minusX, extentX, minusZ, extentZ);
        byte[] instance = InstanceDataBytes();

        using var geometryBuffer = device.CreateBuffer(
            new BufferDesc(BufferKind.Vertex, BufferUsage.Immutable, geometry.Length), geometry);
        using var instanceBuffer = device.CreateBuffer(
            new BufferDesc(BufferKind.Vertex, BufferUsage.Immutable, instance.Length), instance);
        using var indexBuffer = device.CreateBuffer(
            new BufferDesc(BufferKind.Index, BufferUsage.Immutable, 6 * sizeof(ushort), IndexFormat.UInt16),
            MemoryMarshal.AsBytes((ReadOnlySpan<ushort>)[0, 1, 2, 0, 2, 3]));

        var cb = device.AcquireCommandBuffer();
        // Scene.cs's cascade setup, call for call.
        cb.SetRenderTarget(cascade);
        cb.SetViewport(new Viewport(0, 0, resolution, resolution));
        cb.SetScissorRect(new ScissorRect(0, 0, resolution, resolution));
        cb.Clear(ClearOptions.Color | ClearOptions.Depth, new ColorRgba(1f, 1f, 1f, 1f));
        cb.SetPipeline(pipeline);

        // Submesh.Render's shadow branch: only View and Projection are ever set on this program.
        var p = program.Bind();
        p.View.SetValue(cb, lightView);
        p.Projection.SetValue(cb, lightProjection);

        cb.SetVertexBuffer(0, geometryBuffer, PolyStride);
        cb.SetVertexBuffer(1, instanceBuffer, InstanceStride);
        cb.SetIndexBuffer(indexBuffer);
        cb.DrawIndexedInstanced(baseVertex: 0, startIndex: 0, primitiveCount: 2, instanceCount: 1);

        // Reading the target while it is bound is the one thing ReadTexture documents as forbidden.
        cb.SetRenderTarget(null);
        cb.SetViewport(new Viewport(0, 0, device.Swapchain.Width, device.Swapchain.Height));
        device.Submit(cb);
        DrainGlErrors("shadow cascade pass");

        var bytes = new byte[resolution * resolution * sizeof(float)];
        device.ReadTexture(cascade.ColorTexture!, 0, 0, resolution, resolution, bytes);
        DrainGlErrors("shadow cascade readback");

        var depths = MemoryMarshal.Cast<byte, float>(bytes);

        // The whole bug, in one number: geometry was drawn into this pass, so a texel the patch covers
        // must hold the depth the shadow-map program wrote (~0.005), not the 1.0 clear. A 1.0 here is
        // the white shadow map - the pass rasterized nothing.
        var centre = ShadowLookup(System.Numerics.Vector3.Zero, lightView * lightProjection, resolution);
        var centreDepth = depths[centre.Y * resolution + centre.X];

        Report("SHADOW", "the shadow-map lookup is on-axis for the world origin, so a disagreement below " +
                         "is a flip rather than an offset",
            Match(centre, (resolution / 2, resolution / 2), 1));

        Report("SHADOW", "the texel the patch's centre covers holds geometry depth " +
                         $"({centreDepth:0.####} at {centre.X},{centre.Y}, expected ~0.005, not the 1.0 clear)",
            centreDepth > 0.001f && centreDepth < 0.5f);

        // The check the rest of this file cannot make, and the one that pins the reported flip: the
        // shader's own lookup is replayed against a probe whose mirror image is *not* also geometry, so
        // a lookup reading the wrong row shows up as the clear rather than as a different depth. The
        // patch is deliberately lopsided for exactly this - +X reaches 600 against -X's -200.
        var probe = new System.Numerics.Vector3(extentX - 100f, 0f, 0f);
        var (shaderX, shaderY) = ShadowLookup(probe, lightView * lightProjection, resolution);
        var shaderDepth = depths[shaderY * resolution + shaderX];

        // Both halves are asserted, because the correction in Lighting.Prepare and the flip in
        // applyShadowingSingle only make sense as a pair. If someone removes the shader's `1 - y`, the
        // C# correction silently becomes a bug; if someone removes the correction, every shadow in the
        // world shifts by a mirror. Pinning the two against each other is the only way this test can
        // catch either without the other.
        Report("SHADOW", "the shader's lookup carries its own V flip, so on a bottom-left-origin " +
                         $"framebuffer it reads the row-mirrored texel ({shaderDepth:0.####} at {shaderX},{shaderY} is the clear)",
            shaderDepth >= 1f - 0.001f);

        // The correction, exactly as Lighting.Prepare applies it: negate the second *column* of
        // LightViewProj. Reading column 1 rather than row 1 is the convention the shaders fix
        // (row-vector mul), and it moves nothing but sampling - the shadow *pass* transforms by View
        // and Projection separately rather than by this product.
        var corrected = lightView * lightProjection;
        corrected.M12 = -corrected.M12;
        corrected.M22 = -corrected.M22;
        corrected.M32 = -corrected.M32;
        corrected.M42 = -corrected.M42;

        var (fixedX, fixedY) = ShadowLookup(probe, corrected, resolution);
        var fixedDepth = depths[fixedY * resolution + fixedX];
        Report("SHADOW", "negating LightViewProj's second column makes that same lookup land on the depth " +
                         $"the map actually holds ({fixedDepth:0.####} at {fixedX},{fixedY}, expected ~0.005)",
            fixedDepth > 0.001f && fixedDepth < 0.5f);
    }

    /// <summary>
    /// The shadow-map texel a world point resolves to, computed the way <c>PS_IsShadowed</c> computes
    /// it: row-vector multiply by the light's view-projection, perspective divide, NDC to <c>[0,1]</c>,
    /// then the shader's own <c>y = 1 - y</c>.
    ///
    /// This is replayed here rather than restated because the whole point is to test the shader's
    /// convention, not to assert a convention of this test's own that happens to agree with the
    /// readback. <paramref name="flipY"/> exists to show, in the run's own output, which texel the
    /// lookup would have addressed with the flip removed - the difference between the two is the
    /// only direct evidence of which one the data supports.
    /// </summary>
    private static (int X, int Y) ShadowLookup(System.Numerics.Vector3 world, System.Numerics.Matrix4x4 lightViewProj, int resolution, bool flipY = true)
    {
        var clip = System.Numerics.Vector4.Transform(new System.Numerics.Vector4(world, 1f), lightViewProj);
        var ndc = new System.Numerics.Vector2(clip.X / clip.W, clip.Y / clip.W);
        var uv = new System.Numerics.Vector2(0.5f * ndc.X + 0.5f, 0.5f * ndc.Y + 0.5f);
        if (flipY) uv.Y = 1f - uv.Y;

        return (Math.Clamp((int)(uv.X * resolution), 0, resolution - 1),
                Math.Clamp((int)(uv.Y * resolution), 0, resolution - 1));
    }

    private static bool Match((int X, int Y) got, (int X, int Y) expected, int tolerance) =>
        Math.Abs(got.X - expected.X) <= tolerance && Math.Abs(got.Y - expected.Y) <= tolerance;

    /// <summary>
    /// Four vertices in the app's real Poly vertex shape (<c>Mesh.VertexPositionNormalColorCentroid</c>'s
    /// stride 44: three float3s, packed byte4 colour, float), forming a horizontal rectangular patch
    /// of ground in the XZ plane - the face-on geometry for a light camera that looks almost straight
    /// down. The patch is given by its bounds rather than by a size, because a check that wants to
    /// catch a flip in the sampling needs geometry that is <em>not</em> symmetric about the axes it is
    /// mirrored in: a centred square reads back as geometry either way round.
    /// </summary>
    private static byte[] PolyGround(float minX, float maxX, float minZ, float maxZ)
    {
        Span<(float X, float Z)> corners = [(minX, minZ), (maxX, minZ), (maxX, maxZ), (minX, maxZ)];

        var bytes = new byte[PolyStride * corners.Length];
        for (var v = 0; v < corners.Length; v++)
        {
            var at = v * PolyStride;
            Write3(bytes, at + 0, corners[v].X, 0f, corners[v].Z);   // Position
            Write3(bytes, at + 12, 0f, 1f, 0f);                      // Normal, straight up at the sun
            Write3(bytes, at + 24, 0f, 0f, 0f);                      // Centroid
            bytes[at + 36] = 255; bytes[at + 37] = 255; bytes[at + 38] = 255; bytes[at + 39] = 255;
            BitConverter.TryWriteBytes(bytes.AsSpan(at + 40), 0f);    // DecalOffset
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
    /// A matrix uniform must arrive transposed, or the whole scene renders with exploded vertices.
    ///
    /// This is the check the rest of this file cannot make, and its absence is why the bug it detects
    /// shipped. Every other matrix in this test is the identity, which is *symmetric* - it is the one
    /// matrix a transpose leaves unchanged, so a backend that skipped the transpose entirely passed
    /// every check here while the game rendered nothing correctly. The fix is to use a matrix that is
    /// not its own transpose: a pure translation, which puts the offset in M41/M42/M43.
    ///
    /// The convention is fixed by the shaders, not chosen here. They use row-vector math -
    /// <c>mul(float4(p, 1), Projection)</c> - so the translation column is M41/M42/M43 and GL needs
    /// the matrix column-major on the GPU (the inverse of the row-major System.Numerics layout).
    /// FNA3D's backend did that transpose in its own SetUniform, and its comment is explicit that
    /// callers hand it row-major matrices. The GL backend was written without it.
    ///
    /// A translation in X is the sharpest probe: left as row-major it lands in the GPU's column 4,
    /// which is a perspective divide rather than an offset, so the quad smears across the screen
    /// instead of moving sideways. Conservative rasterization means a shifted quad still covers the
    /// sample points, so the assertion is on the *whole image* rather than on two pixels: the drawn
    /// quad must stop well short of the right edge either way.
    /// </summary>
    private static void CheckMatrixUniformOrientation(GlGraphicsDevice device)
    {
        const int size = 64;
        var program = ImGuiFullbright.Create();
        var module = GlGraphicsDevice.LoadProgram(program.Vertex, program.Pixel, program.Reflection);
        using var pipeline = device.CreatePipeline(new PipelineDesc(
            VertexShader: module,
            PixelShader: module,
            VertexLayouts: [ImGuiLayout],
            BlendState: BlendStateDesc.Opaque,
            DepthStencilState: DepthStencilStateDesc.None,
            RasterizerState: RasterizerStateDesc.Default with { CullMode = CullMode.None },
            Topology: PrimitiveTopology.TriangleList));

        using var target = device.CreateRenderTarget(
            new RenderTargetDesc(size, size, TextureFormat.Rgba8, HasDepthStencil: false));

        // Translate +0.5 in X and leave a clearly non-identity scale on the diagonal, so every
        // element that could be mistaken for another differs. Row 4 is the translation row, which is
        // where a row-vector convention keeps it.
        var translate = System.Numerics.Matrix4x4.Identity;
        translate.M41 = 0.5f;
        translate.M11 = 1f;

        // A quad covering only the left half of clip space, so a +0.5 shift lands it mid-screen and
        // an unshifted one leaves it hanging off the left edge. Positions are already in clip space.
        byte[] vertices = ImGuiVertices(
            -1f, -1f, 0f, 0f,
             0f, -1f, 1f, 0f,
             0f,  1f, 1f, 1f,
            -1f,  1f, 0f, 1f);
        Span<ushort> indices = [0, 1, 2, 0, 2, 3];

        using var vertexBuffer = device.CreateBuffer(
            new BufferDesc(BufferKind.Vertex, BufferUsage.Immutable, vertices.Length), vertices);
        using var indexBuffer = device.CreateBuffer(
            new BufferDesc(BufferKind.Index, BufferUsage.Immutable, indices.Length * sizeof(ushort), IndexFormat.UInt16),
            MemoryMarshal.AsBytes(indices));
        using var texture = device.CreateTexture(new TextureDesc(2, 2, TextureFormat.Rgba8), [
            255, 0, 255, 255,  255, 0, 255, 255,
            255, 0, 255, 255,  255, 0, 255, 255,
        ]);
        using var sampler = device.CreateSampler(
            new SamplerDesc(TextureFilter.Point, TextureAddressMode.Clamp, TextureAddressMode.Clamp));

        var cb = device.AcquireCommandBuffer();
        cb.SetRenderTarget(target);
        cb.SetViewport(new Viewport(0, 0, size, size));
        cb.Clear(ClearOptions.Color, new ColorRgba(0f, 0f, 0f));
        cb.SetPipeline(pipeline);
        program.Bind().Projection.SetValue(cb, translate);
        cb.SetShaderResource(0, texture, sampler);
        cb.SetVertexBuffer(0, vertexBuffer, ImGuiStride);
        cb.SetIndexBuffer(indexBuffer);
        cb.DrawIndexed(baseVertex: 0, startIndex: 0, primitiveCount: 2);
        cb.SetRenderTarget(null);
        device.Submit(cb);
        DrainGlErrors("matrix orientation draw");

        var pixels = new byte[size * size * 4];
        device.ReadTexture(target.ColorTexture!, 0, 0, size, size, pixels);

        // The rightmost column containing any drawn pixel. The quad covers clip x in [-1, 0] and the
        // matrix translates it by +0.5, so its right edge belongs at clip x = +0.5 - three quarters
        // of the width. Unshifted it stops at the centre; with the translation read as w it smears
        // past the right edge instead.
        var lastDrawn = -1;
        for (var x = 0; x < size; x++)
        {
            for (var y = 0; y < size; y++)
            {
                if (PixelAt(pixels, size, x, y).R > 100) { lastDrawn = x; break; }
            }
        }

        // Bounded on both sides: too far left means the translation was dropped, too far right means
        // it landed in the wash. Measured against this backend both ways before being written down -
        // a row-major write gives 31 (dropped) and a transposed one 47 (correct), which is where the
        // tolerance comes from rather than from the geometry alone.
        Report("DRAW", $"a translated matrix shifts the quad to mid-screen (rightmost drawn column " +
                       $"{lastDrawn}, expected about {3 * size / 4})",
            lastDrawn >= 3 * size / 4 - 3 && lastDrawn <= 3 * size / 4 + 2);
        Console.WriteLine("           an identity matrix cannot catch this: it is symmetric, so a missing");
        Console.WriteLine("           transpose leaves it unchanged - which is how the bug reached the game");
    }

    /// <summary>
    /// A base-vertex draw must move the geometry, and the pixels must prove it did.
    ///
    /// This is the check that the ES entry points are the ones being called. ANGLE exports the
    /// *desktop* spellings of these functions as stubs that raise GL_INVALID_OPERATION and draw
    /// nothing, and Silk.NET's <c>GL.DrawElementsBaseVertex</c> binds those - so a backend that
    /// reached for the obvious API would pass every error check in this file while drawing nothing,
    /// and every base-vertex draw in the app would silently vanish. Only pixels separate "the
    /// extension worked" from "the call was a no-op".
    ///
    /// The two entry points are checked in *separate* clear-and-draw passes, and that separation is
    /// load-bearing rather than tidiness. A first version drew all three calls into one target; when
    /// the non-instanced base vertex was deliberately sabotaged to 0 the check still passed, because
    /// the instanced draw - a different entry point, which the sabotage had not touched - painted the
    /// same right-half pixels over the top. Sharing a target let a working call mask a broken one.
    ///
    /// Within a pass the construction makes baseVertex the only variable. Six vertices form two
    /// triangles: 0-2 cover the left half and sample the texture's magenta column, 3-5 cover the right
    /// half and sample the yellow one. Both draws use the *same* three indices and the same count -
    /// only the base vertex differs, 0 then 3 - so if baseVertex is honoured the two triangles land on
    /// opposite halves and each sample point sees its own colour. If it is ignored, both draws paint
    /// the left triangle and the right sample stays black, which is exactly the silent no-op above.
    /// </summary>
    private static void CheckBaseVertexDraw(GlGraphicsDevice device)
    {
        const int size = 64;
        var program = ImGuiFullbright.Create();
        var module = GlGraphicsDevice.LoadProgram(program.Vertex, program.Pixel, program.Reflection);
        using var pipeline = device.CreatePipeline(new PipelineDesc(
            VertexShader: module,
            PixelShader: module,
            VertexLayouts: [ImGuiLayout],
            BlendState: BlendStateDesc.Opaque,
            DepthStencilState: DepthStencilStateDesc.None,
            RasterizerState: RasterizerStateDesc.Default with { CullMode = CullMode.None },
            Topology: PrimitiveTopology.TriangleList));

        using var target = device.CreateRenderTarget(
            new RenderTargetDesc(size, size, TextureFormat.Rgba8, HasDepthStencil: false));

        float[] identity =
        [
            1f, 0f, 0f, 0f,
            0f, 1f, 0f, 0f,
            0f, 0f, 1f, 0f,
            0f, 0f, 0f, 1f,
        ];

        // Left triangle first, right triangle second, because baseVertex 3 has to land on the second
        // one. The U coordinates stay inside their half's texture column so the sampled colour is
        // unambiguous - magenta is column 0, yellow is column 1.
        byte[] vertices = ImGuiVertices(
            -1f, -1f, 0.0f, 0.0f,
             0f, -1f, 0.4f, 0.0f,
             0f,  1f, 0.4f, 1.0f,
             0f, -1f, 0.6f, 0.0f,
             1f, -1f, 1.0f, 0.0f,
             1f,  1f, 1.0f, 1.0f);
        Span<ushort> indices = [0, 1, 2];

        using var vertexBuffer = device.CreateBuffer(
            new BufferDesc(BufferKind.Vertex, BufferUsage.Immutable, vertices.Length), vertices);
        using var indexBuffer = device.CreateBuffer(
            new BufferDesc(BufferKind.Index, BufferUsage.Immutable, indices.Length * sizeof(ushort), IndexFormat.UInt16),
            MemoryMarshal.AsBytes(indices));
        using var texture = device.CreateTexture(new TextureDesc(2, 2, TextureFormat.Rgba8), [
            255, 0, 255, 255,  255, 255, 0, 255,
            255, 0, 255, 255,  255, 255, 0, 255,
        ]);
        using var sampler = device.CreateSampler(
            new SamplerDesc(TextureFilter.Point, TextureAddressMode.Clamp, TextureAddressMode.Clamp));

        Report("DRAW", "context reports base-vertex support (glDrawElementsBaseVertexEXT present)",
            device.SupportsBaseVertex);

        if (!device.SupportsBaseVertex)
        {
            // Without the extension the draw has to be refused, not silently misapplied. This is the
            // branch that would have been a stub of the check above if the old BUFFER assertion had
            // been left in place - it asserted a refusal while never binding a pipeline, so the throw
            // it caught was RequirePipeline's, not base-vertex's.
            var refused = false;
            var cb = device.AcquireCommandBuffer();
            cb.SetRenderTarget(target);
            cb.SetPipeline(pipeline);
            cb.SetVertexBuffer(0, vertexBuffer, ImGuiStride);
            cb.SetIndexBuffer(indexBuffer);
            try { cb.DrawIndexed(baseVertex: 1, startIndex: 0, primitiveCount: 1); }
            catch (NotSupportedException) { refused = true; }
            device.Submit(cb);
            Report("DRAW", "a non-zero baseVertex is refused on a context without the extension", refused);
            Console.WriteLine("           skipping the pixel assertions: this context cannot do the draw at all");
            return;
        }

        // Each pass renders alone into its own cleared target, so a failure in one cannot be covered
        // by another. Returns the pixels it produced.
        byte[] Pass(string label, Action<ICommandBuffer> draw)
        {
            var cb = device.AcquireCommandBuffer();
            cb.SetRenderTarget(target);
            cb.SetViewport(new Viewport(0, 0, size, size));
            cb.Clear(ClearOptions.Color, new ColorRgba(0f, 0f, 0f));
            cb.SetPipeline(pipeline);
            program.Bind().Projection.SetValue(cb, identity);
            cb.SetShaderResource(0, texture, sampler);
            cb.SetVertexBuffer(0, vertexBuffer, ImGuiStride);
            cb.SetIndexBuffer(indexBuffer);

            var before = DrainGlErrors($"before {label}");
            draw(cb);
            cb.SetRenderTarget(null);
            device.Submit(cb);
            var errors = DrainGlErrors(label) - before;
            Report("DRAW", $"{label} reached the driver with {errors} GL error(s)", errors == 0);

            var pixels = new byte[size * size * 4];
            device.ReadTexture(target.ColorTexture!, 0, 0, size, size, pixels);
            return pixels;
        }

        // The non-instanced entry point, in isolation: baseVertex 0 then baseVertex 3.
        var indexed = Pass("non-instanced base-vertex draw", cb =>
        {
            cb.DrawIndexed(baseVertex: 0, startIndex: 0, primitiveCount: 1);
            cb.DrawIndexed(baseVertex: 3, startIndex: 0, primitiveCount: 1);
        });
        ReportHalf("non-instanced", indexed);

        // The instanced entry point, in isolation. instanceCount 2 is deliberate: the porting layer
        // routes an instanceCount of 1 through the non-instanced entry point, so a count of 1 would
        // not test this one at all. Both half-planes share baseVertex 3, so the right triangle is
        // drawn twice and then the left one - which is still enough to tell a honoured base vertex
        // from an ignored one.
        var instanced = Pass("instanced base-vertex draw", cb =>
            cb.DrawIndexedInstanced(baseVertex: 3, startIndex: 0, primitiveCount: 1, instanceCount: 2));
        var instRight = PixelAt(instanced, size, 3 * size / 4, size / 2);
        var instLeft = PixelAt(instanced, size, size / 4, size / 2);
        Report("DRAW", $"instanced baseVertex 3 drew the right half ({instRight.R},{instRight.G},{instRight.B}), expected yellow (255,255,0)",
            IsYellow(instRight));
        Report("DRAW", $"instanced baseVertex 3 left the left half black ({instLeft.R},{instLeft.G},{instLeft.B})",
            IsBlack(instLeft));

        void ReportHalf(string what, byte[] pixels)
        {
            var left = PixelAt(pixels, size, size / 4, size / 2);
            var right = PixelAt(pixels, size, 3 * size / 4, size / 2);
            Report("DRAW", $"{what} baseVertex 0 drew the left half ({left.R},{left.G},{left.B}), expected magenta (255,0,255)",
                IsMagenta(left));
            Report("DRAW", $"{what} baseVertex 3 drew the right half ({right.R},{right.G},{right.B}), expected yellow (255,255,0)",
                IsYellow(right));
            Console.WriteLine("           both draws used indices [0,1,2] and the same count, so only the base vertex");
            Console.WriteLine("           can have moved the second triangle - a black right half is baseVertex being ignored");
        }
    }

    private static bool IsMagenta(Pixel p) => p.R > 200 && p.G < 60 && p.B > 200;
    private static bool IsYellow(Pixel p) => p.R > 200 && p.G > 200 && p.B < 60;
    private static bool IsBlack(Pixel p) => p.R < 40 && p.G < 40 && p.B < 40;

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
    /// The bundles these come from are compiled by this project's own shader build (the
    /// NFMWorld.ShaderCompiler.targets import in the csproj), one entry-point pair each - the
    /// compiler emits one bundle per technique, so a shader that declares several contributes
    /// several. That last point is visible in the results: Nvg has four techniques and this list
    /// takes only `Simple`, whose entry point samples no texture, so its reflection names
    /// `g_texture` while the linked program declares no sampler at all. The other three techniques
    /// are the ones that do sample, and they are covered by the game's own Nvg renderer rather
    /// than here.
    ///
    /// Attribute locations are assigned by *position*: glslang numbers the HLSL front-end's inputs in
    /// declaration order, ignoring the D3D semantic indices, so what matters is the order of the GLSL
    /// `layout(location = ...)` declarations. All the layouts below are already in that order.
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

            // Both Poly techniques, unlike the POC's single bundle. The shadow pass is a distinct
            // entry-point pair with its own reflection, so it is a distinct program that has to
            // link - and it is the one whose block layout nothing else here would cover.
            var poly = PolyBasic.Create();
            yield return new Bundle("PolyBasic", poly.Vertex, poly.Pixel, poly.Reflection, [PolyLayout, InstanceLayout]);

            var polyShadow = PolyCreateShadowMap.Create();
            yield return new Bundle("PolyCreateShadowMap", polyShadow.Vertex, polyShadow.Pixel, polyShadow.Reflection, [PolyLayout, InstanceLayout]);

            // All four Nvg techniques: they share VSMain and differ only in the pixel entry point,
            // which is exactly the axis a sampler-name bug lives on - Simple samples nothing while
            // the other three sample g_texture.
            var nvgSimple = NvgSimple.Create();
            yield return new Bundle("NvgSimple", nvgSimple.Vertex, nvgSimple.Pixel, nvgSimple.Reflection, [NvgLayout]);

            var nvgFillGradient = NvgFillGradient.Create();
            yield return new Bundle("NvgFillGradient", nvgFillGradient.Vertex, nvgFillGradient.Pixel, nvgFillGradient.Reflection, [NvgLayout]);

            var nvgFillImage = NvgFillImage.Create();
            yield return new Bundle("NvgFillImage", nvgFillImage.Vertex, nvgFillImage.Pixel, nvgFillImage.Reflection, [NvgLayout]);

            var nvgTriangles = NvgTriangles.Create();
            yield return new Bundle("NvgTriangles", nvgTriangles.Vertex, nvgTriangles.Pixel, nvgTriangles.Reflection, [NvgLayout]);

            var particle = ParticleFullbright.Create();
            yield return new Bundle("ParticleFullbright", particle.Vertex, particle.Pixel, particle.Reflection, [PositionColorLayout]);
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
