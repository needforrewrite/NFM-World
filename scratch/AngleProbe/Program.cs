// LLM maintained.
// Throwaway probe: what would an OpenGL ES 3.0 / ANGLE path actually cost NFMWorld?
//
// ANGLE is a runtime abstraction - it translates GLSL ES to HLSL and then runs D3DCompile - so
// the questions that decide whether it is viable are (a) the shader-compile hit, (b) the per-draw
// CPU cost of the translation layer, (c) whether the ES 3.0 feature set covers what the game
// renders, and (d) whether std140 packs uniforms at the same byte offsets the game's D3D-derived
// reflection already assumes. This measures all four.
//
// It uses raw P/Invoke to libEGL/libGLESv2 rather than Silk.NET's GLES binding: the point is to
// measure the driver, and a binding layer would only add its own noise on top. See the csproj for
// why the ANGLE binaries are not the ones that appear throughout this repository.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace AngleProbe;

internal static class Program
{
    private static unsafe int Main(string[] args)
    {
        // `--check-es <dir>` skips the measurements and instead compiles the shader compiler's
        // real `#version 300 es` output. That is the question the measurements cannot answer: the
        // hand-written probe shaders were written for ES 3.0 by hand, while the generated ones come
        // out of spirv-cross via HLSL -> SPIR-V, which is where an ES 3.0 constraint (an ES 3.10
        // builtin, a missing precision qualifier) would actually surface.
        var checkIndex = Array.IndexOf(args, "--check-es");
        if (checkIndex >= 0)
        {
            if (checkIndex + 1 >= args.Length)
            {
                Console.WriteLine("usage: AngleProbe --check-es <dir-of-generated-es-glsl>");
                return 2;
            }

            return RunEsCheck(args[checkIndex + 1]);
        }

        Console.WriteLine("== ANGLE / OpenGL ES 3.0 probe ==\n");

        var display = CreateDisplay();
        if (display == IntPtr.Zero)
        {
            Console.WriteLine("eglGetPlatformDisplayEXT failed - no ANGLE display.");
            return 1;
        }

        if (Egl.eglInitialize(display, out var major, out var minor) != EGL_TRUE)
        {
            Console.WriteLine($"eglInitialize failed (0x{Egl.eglGetError():X4}).");
            return 1;
        }

        Console.WriteLine($"EGL {major}.{minor}, vendor: {String(Egl.eglQueryString(display, EGL_VENDOR))}");

        if (!CreateContext(display, out var context, out var surface))
            return 1;

        Egl.eglMakeCurrent(display, surface, surface, context);

        ReportCapabilities();
        CheckMultisampleTarget();
        var shader = BenchmarkShaderCompile();
        BenchmarkDraws(shader);
        CheckUniformLayout();
        CheckMatrixOrientation();

        Egl.eglMakeCurrent(display, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        return 0;
    }

    /// <summary>
    /// Asks ANGLE for an explicit D3D11 display. Going through
    /// <c>EGL_ANGLE_platform_angle</c> rather than a default display also makes the probe print
    /// which backend it actually landed on, so a silent fall back to D3D9 or to a software device
    /// cannot be mistaken for a D3D11 measurement.
    /// </summary>
    private static IntPtr CreateDisplay()
    {
        var attribs = new[]
        {
            EGL_PLATFORM_ANGLE_TYPE_ANGLE, EGL_PLATFORM_ANGLE_TYPE_D3D11_ANGLE,
            EGL_PLATFORM_ANGLE_DEVICE_TYPE_ANGLE, EGL_PLATFORM_ANGLE_DEVICE_TYPE_HARDWARE_ANGLE,
            EGL_NONE,
        };

        unsafe
        {
            fixed (int* p = attribs)
                return Egl.eglGetPlatformDisplayEXT(EGL_PLATFORM_ANGLE_ANGLE, IntPtr.Zero, (IntPtr)p);
        }
    }

    private static bool CreateContext(IntPtr display, out IntPtr context, out IntPtr surface)
    {
        context = IntPtr.Zero;
        surface = IntPtr.Zero;

        var configAttribs = new[]
        {
            EGL_SURFACE_TYPE, EGL_PBUFFER_BIT,
            EGL_RENDERABLE_TYPE, EGL_OPENGL_ES3_BIT,
            EGL_RED_SIZE, 8,
            EGL_GREEN_SIZE, 8,
            EGL_BLUE_SIZE, 8,
            EGL_ALPHA_SIZE, 8,
            EGL_DEPTH_SIZE, 24,
            EGL_NONE,
        };

        unsafe
        {
            fixed (int* p = configAttribs)
            {
                if (Egl.eglChooseConfig(display, (IntPtr)p, out var config, 1, out var count) != EGL_TRUE
                    || count < 1)
                {
                    Console.WriteLine($"eglChooseConfig found no ES3 config (0x{Egl.eglGetError():X4}).");
                    return false;
                }

                var contextAttribs = new[] { EGL_CONTEXT_CLIENT_VERSION, 3, EGL_NONE };
                fixed (int* c = contextAttribs)
                    context = Egl.eglCreateContext(display, config, IntPtr.Zero, (IntPtr)c);

                if (context == IntPtr.Zero)
                {
                    Console.WriteLine($"eglCreateContext failed (0x{Egl.eglGetError():X4}).");
                    return false;
                }

                // A pbuffer is enough: nothing here is shown, and it is the only surface that
                // works without a window system.
                var surfaceAttribs = new[] { EGL_WIDTH, 1920, EGL_HEIGHT, 1080, EGL_NONE };
                fixed (int* s = surfaceAttribs)
                    surface = Egl.eglCreatePbufferSurface(display, config, (IntPtr)s);

                if (surface == IntPtr.Zero)
                {
                    Console.WriteLine($"eglCreatePbufferSurface failed (0x{Egl.eglGetError():X4}).");
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>What ES version and feature set this ANGLE build actually exposes.</summary>
    private static unsafe void ReportCapabilities()
    {
        Console.WriteLine("\n-- capabilities --");
        Console.WriteLine($"VENDOR:   {String(Gl.glGetString(GL_VENDOR))}");
        Console.WriteLine($"RENDERER: {String(Gl.glGetString(GL_RENDERER))}");
        Console.WriteLine($"VERSION:  {String(Gl.glGetString(GL_VERSION))}");
        Console.WriteLine($"GLSL:     {String(Gl.glGetString(GL_SHADING_LANGUAGE_VERSION))}");

        Gl.glGetIntegerv(GL_MAX_TEXTURE_SIZE, out var maxTexture);
        Gl.glGetIntegerv(GL_MAX_VERTEX_ATTRIBS, out var maxAttribs);
        Gl.glGetIntegerv(GL_MAX_DRAW_BUFFERS, out var maxDrawBuffers);
        Gl.glGetIntegerv(GL_MAX_UNIFORM_BUFFER_BINDINGS, out var maxUbo);
        Gl.glGetIntegerv(GL_MAX_SAMPLES, out var maxSamples);

        Console.WriteLine($"max texture 2D: {maxTexture}, vertex attribs: {maxAttribs}, draw buffers: {maxDrawBuffers}");
        Console.WriteLine($"max uniform buffer bindings: {maxUbo}, max samples: {maxSamples}");

        // The extensions that decide whether the two things NFMWorld leans on hardest - shadow
        // sampling and MSAA render targets - are even available.
        var extensions = String(Gl.glGetString(GL_EXTENSIONS));
        foreach (var wanted in new[]
        {
            "GL_ANGLE_program_cache_control",
            "GL_EXT_disjoint_timer_query",
            "GL_EXT_texture_filter_anisotropic",
            "GL_OES_texture_float_linear",
            "GL_EXT_color_buffer_float",
            "GL_ANGLE_framebuffer_multisample",
            "GL_EXT_sRGB",
        })
        {
            Console.WriteLine($"  {(extensions.Contains(wanted) ? "yes" : "no ")} {wanted}");
        }
    }

    /// <summary>
    /// Whether an MSAA colour+depth target can be created, and at what sample counts.
    ///
    /// This is the one thing in the game's renderer that ES 3.0 lacks a direct equivalent for:
    /// there is no <c>glTexImage2DMultisample</c> (that is ES 3.1), so a multisampled *texture*
    /// cannot be made. A multisampled *renderbuffer* is fine, but a renderbuffer cannot be
    /// sampled - so MSAA has to go through a resolve blit into a regular texture. That is
    /// workable, but it is a real difference from both the D3D11 and sokol paths, so it is worth
    /// confirming rather than assuming.
    /// </summary>
    private static unsafe void CheckMultisampleTarget()
    {
        Console.WriteLine("\n-- MSAA target --");

        Gl.glGetIntegerv(GL_MAX_SAMPLES, out var maxSamples);
        Console.WriteLine($"  GL_MAX_SAMPLES: {maxSamples}");

        foreach (var samples in new[] { 2, 4, 8 })
        {
            if (samples > maxSamples)
            {
                Console.WriteLine($"  {samples}x: above GL_MAX_SAMPLES");
                continue;
            }

            // glGetBoundFramebuffer is ES 3.1 and this context is 3.0, so the binding is tracked
            // by hand - the only thing in this probe that has to pander to the version. Nothing
            // upstream binds a framebuffer, so it starts at the default.
            Gl.glGenFramebuffers(1, out var fbo);
            const uint previous = 0;
            Gl.glBindFramebuffer(GL_FRAMEBUFFER, fbo);

            Gl.glGenRenderbuffers(1, out var colour);
            Gl.glBindRenderbuffer(GL_RENDERBUFFER, colour);
            Gl.glRenderbufferStorageMultisample(GL_RENDERBUFFER, samples, GL_RGBA8, 1920, 1080);

            Gl.glGenRenderbuffers(1, out var depth);
            Gl.glBindRenderbuffer(GL_RENDERBUFFER, depth);
            Gl.glRenderbufferStorageMultisample(GL_RENDERBUFFER, samples, GL_DEPTH_COMPONENT24, 1920, 1080);

            Gl.glFramebufferRenderbuffer(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, GL_RENDERBUFFER, colour);
            Gl.glFramebufferRenderbuffer(GL_FRAMEBUFFER, GL_DEPTH_ATTACHMENT, GL_RENDERBUFFER, depth);

            var status = Gl.glCheckFramebufferStatus(GL_FRAMEBUFFER);
            var ok = status == GL_FRAMEBUFFER_COMPLETE;
            var error = Gl.glGetError();

            Console.WriteLine($"  {samples}x renderbuffer MSAA 1920x1080:"
                + $" {(ok ? "complete" : $"INCOMPLETE (0x{status:X4})")}"
                + $"{(error != 0 ? $" glError 0x{error:X4}" : "")}");

            Gl.glBindFramebuffer(GL_FRAMEBUFFER, previous);
        }

        Gl.glBindFramebuffer(GL_FRAMEBUFFER, 0);
    }

    /// <summary>
    /// Checks that a std140 uniform block lays out at the offsets the game already assumes.
    ///
    /// This is the quiet correctness risk in a GLES port. The game's uniform offsets are byte
    /// offsets into a cbuffer, produced by D3D packoffset rules, and the command buffer writes
    /// into them by offset. GLES has no packoffset - it has std140, which has its own rules about
    /// alignment and about how a vec3 is padded. If the two disagree, uniforms do not fail to
    /// bind; they silently read as the wrong values, which is the worst kind of bug to chase.
    ///
    /// The block below is laid out exactly like the game's: a mat4 at offset 0, then scalars and
    /// a vec3 whose alignment is where std140 and packoffset are most likely to diverge.
    /// </summary>
    private static unsafe void CheckUniformLayout()
    {
        Console.WriteLine("\n-- std140 uniform layout --");

        // The layout risk in a GLES port, tested rather than assumed.
        //
        // The game's uniforms are byte offsets from D3D packoffset rules, and the command buffer
        // writes into them by offset. GLES has no packoffset - it has std140 repacking. Where the
        // two disagree, a uniform does not fail to bind; it reads the wrong bytes and produces
        // wrong output with no error at all, which is the worst failure mode to chase down.
        //
        // The block below reproduces the generated Line bundle's real block up to BaseColor, with
        // the offsets that bundle's ShaderReflection actually carries:
        //
        //     LightViewProj0 0    DepthBias 192    LightDirection 208   ViewProj 352
        //     SnapColor    416    IsFullbright 428  UseBaseColor 432    BaseColor 448
        //
        // SnapColor -> IsFullbright is the interesting pair: a vec3 immediately followed by a
        // scalar. If ANGLE padded the vec3 out to a full 16 bytes the way some std140 readings
        // suggest, IsFullbright would sit at 432 and silently read UseBaseColor's value instead.
        //
        // The test fills the buffer so that the word at byte offset N holds the float N/4, then
        // reads four members back out of a rendered pixel. A correct layout returns each member's
        // own offset; a divergent one returns a neighbouring member's, which is unmistakable.
        const string fragmentSource = """
            #version 300 es
            precision highp float;
            precision highp int;
            layout(std140) uniform Block
            {
                mat4 LightViewProj0;
                mat4 LightViewProj1;
                mat4 LightViewProj2;
                float DepthBias;
                float NumCascades;
                vec3 LightDirection;
                mat4 View;
                mat4 Projection;
                mat4 ViewProj;
                vec3 SnapColor;
                uint IsFullbright;
                uint UseBaseColor;
                vec3 BaseColor;
            };
            out vec4 fragColor;
            void main()
            {
                // Normalised by 256 so every tested value lands inside the 0..1 an RGBA8 target
                // can hold, and scaled back up on the CPU.
                // Every member's value is the layout word it was read from - for the floats because
                // word i holds the float i, for the uints because those two words were filled with
                // their own index. Nothing is rescaled; the target is RGBA32F and stores it
                // exactly, so the readback names the offset directly.
                fragColor = vec4(SnapColor.x, float(IsFullbright), BaseColor.x, float(UseBaseColor));
            }
            """;

        const string vertexSource = """
            #version 300 es
            layout(location = 0) in vec2 inPos;
            void main() { gl_Position = vec4(inPos, 0.0, 1.0); }
            """;

        var vertexShader = CompileStage(GL_VERTEX_SHADER, vertexSource, null);
        var fragmentShader = CompileStage(GL_FRAGMENT_SHADER, fragmentSource, null);

        var program = Gl.glCreateProgram();
        Gl.glAttachShader(program, vertexShader);
        Gl.glAttachShader(program, fragmentShader);
        Gl.glLinkProgram(program);
        Gl.glGetProgramiv(program, GL_LINK_STATUS, out var linked);
        if (linked == 0)
        {
            Console.WriteLine($"  link failed: {InfoLog(program, program: true)}");
            return;
        }

        Gl.glUseProgram(program);
        var blockIndex = Gl.glGetUniformBlockIndex(program, "Block");
        if (blockIndex == GL_INVALID_INDEX)
        {
            Console.WriteLine("  block \"Block\" not found (optimized out?)");
            return;
        }

        Gl.glGetActiveUniformBlockiv(program, blockIndex, GL_UNIFORM_BLOCK_DATA_SIZE, out var blockSize);
        Console.WriteLine($"  block data size (std140, driver-computed): {blockSize} bytes");
        Console.WriteLine($"  the same block's D3D reflection extends to {448 + 12} bytes");

        // Fill the block so word k holds the float k, one word per 4 bytes.
        const int uintWords = 128;
        var words = new float[uintWords];
        for (var i = 0; i < uintWords; i++)
            words[i] = i;

        // Every word holds the float i, so a float member's value *is* its own word index. The two
        // words the uint members read are the exception: there the raw bytes are a small int32,
        // because reinterpreting 428.0f's bits as a uint yields ~1.1e9 and float(1.1e9) saturates
        // to 1.0 for every possible offset. The first version of this test did exactly that and
        // mistook its own encoding for a layout mismatch.
        var raw = new byte[uintWords * 4];
        for (var i = 0; i < uintWords; i++)
            BitConverter.GetBytes(words[i]).CopyTo(raw, i * 4);
        foreach (var word in new[] { 428 / 4, 432 / 4 })
            BitConverter.GetBytes(word).CopyTo(raw, word * 4);

        Gl.glGenBuffers(1, out var ubo);
        Gl.glBindBuffer(GL_UNIFORM_BUFFER, ubo);
        fixed (byte* bp = raw)
            Gl.glBufferData(GL_UNIFORM_BUFFER, uintWords * 4, (IntPtr)bp, GL_STATIC_DRAW);
        Gl.glUniformBlockBinding(program, blockIndex, 0);
        Gl.glBindBufferBase(GL_UNIFORM_BUFFER, 0, ubo);

        // A 1x1 target is all this needs.
        Gl.glGenFramebuffers(1, out var fbo);
        Gl.glBindFramebuffer(GL_FRAMEBUFFER, fbo);
        Gl.glGenTextures(1, out var target);
        Gl.glBindTexture(GL_TEXTURE_2D, target);
        // RGBA32F rather than RGBA8: an 8-bit target quantises to 1/255, which is far too coarse
        // to read a 32-bit-offset word index back out of, and it is the reason the first version
        // of this test reported offsets 400 bytes short.
        Gl.glTexImage2D(GL_TEXTURE_2D, 0, GL_RGBA32F, 1, 1, 0, GL_RGBA, GL_FLOAT, IntPtr.Zero);
        Gl.glFramebufferTexture2D(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, GL_TEXTURE_2D, target, 0);
        if (Gl.glCheckFramebufferStatus(GL_FRAMEBUFFER) != GL_FRAMEBUFFER_COMPLETE)
        {
            Console.WriteLine("  readback framebuffer incomplete - layout check skipped");
            return;
        }

        Gl.glViewport(0, 0, 1, 1);
        var twoTriangles = new[] { -1f, -1f, 3f, -1f, -1f, 3f };
        Gl.glGenBuffers(1, out var vbo);
        Gl.glBindBuffer(GL_ARRAY_BUFFER, vbo);
        fixed (float* vp = twoTriangles)
            Gl.glBufferData(GL_ARRAY_BUFFER, twoTriangles.Length * 4, (IntPtr)vp, GL_STATIC_DRAW);
        Gl.glEnableVertexAttribArray(0);
        Gl.glVertexAttribPointer(0, 2, GL_FLOAT, 0, 8, IntPtr.Zero);
        Gl.glDrawArrays(GL_TRIANGLES, 0, 3);
        Gl.glFinish();

        var pixel = new float[4];
        fixed (float* pp = pixel)
            Gl.glReadPixels(0, 0, 1, 1, GL_RGBA, GL_FLOAT, (IntPtr)pp);

        // Each float member's value is the word index it was read from, so the offset is 4x the
        // value; each integer member's value is that same word index, so the offset is the value
        // divided by 4. Comparing both against the bundle's reflection is the whole point.
        var checks = new (string Name, int ExpectedOffset, int Channel)[]
        {
            ("SnapColor.x", 416, 0),
            ("IsFullbright", 428, 1),
            ("BaseColor.x", 448, 2),
            ("UseBaseColor", 432, 3),
        };

        var matches = true;
        foreach (var (name, want, channel) in checks)
        {
            var value = (int)MathF.Round(pixel[channel]) * 4;

            var ok = value == want;
            matches &= ok;
            Console.WriteLine($"  {name,-14} shader read offset {value,4}   reflection expects {want,4}"
                + $"   {(ok ? "match" : $"DIFFER by {value - want}")}");
        }

        Console.WriteLine(matches
            ? "  std140 agrees with the D3D offsets - the existing reflection stays valid under GLES"
            : "  std140 DIFFERS from the D3D offsets - a GLES backend needs its own offset table");
    }

    /// <summary>
    /// Checks that a matrix uploaded as raw bytes produces the same transform under ES as it does
    /// under the D3D path that already works.
    ///
    /// This is the silent-failure risk the offset check above cannot see. Matching offsets prove the
    /// block's size and member positions agree; they say nothing about how the sixteen floats of a
    /// matrix are interpreted, and a transposed transformation matrix fails nothing - it just renders
    /// a world that is subtly wrong everywhere, which reads as a gameplay bug rather than a graphics
    /// one.
    ///
    /// The two paths state opposite things about the same matrices, and it is worth being precise
    /// about why that is correct rather than alarming. For the same bytes <c>b</c>:
    ///
    ///   HLSL: <c>column_major float4x4 V;</c> read as <c>V(i,j) = b[j*4+i]</c>, used as <c>mul(v, V)</c>
    ///   GLSL: <c>layout(row_major) mat4 V';</c> read as <c>V'(i,j) = b[i*4+j]</c>, used as <c>V' * v</c>
    ///
    /// The two reads make <c>V'</c> the transpose of <c>V</c>, and the two multiplication orders
    /// transpose again, so both compute <c>sum_j v_j * V(j,i)</c>. The conventions are not in
    /// conflict; they cancel. What this test has to rule out is a driver ignoring
    /// <c>row_major</c> (which would break that cancellation), so it asserts against a D3D
    /// reference computed on the CPU rather than against a hard-coded number.
    /// </summary>
    private static unsafe void CheckMatrixOrientation()
    {
        Console.WriteLine("\n-- matrix orientation (ES vs the D3D convention) --");

        // `M * vec4(1,0,0,0)` is M's first column, i.e. V'(i,0) for each i.
        const string vertexSource = """
            #version 300 es
            layout(std140) uniform Block { layout(row_major) mat4 M; };
            layout(location = 0) in vec2 inPos;
            void main() { gl_Position = vec4(inPos, 0.0, 1.0); }
            """;

        const string fragmentSource = """
            #version 300 es
            precision highp float;
            precision highp int;
            layout(std140) uniform Block { layout(row_major) mat4 M; };
            out vec4 fragColor;
            void main() { fragColor = M * vec4(1.0, 0.0, 0.0, 0.0); }
            """;

        var vertexShader = CompileStage(GL_VERTEX_SHADER, vertexSource, null);
        var fragmentShader = CompileStage(GL_FRAGMENT_SHADER, fragmentSource, null);

        var program = Gl.glCreateProgram();
        Gl.glAttachShader(program, vertexShader);
        Gl.glAttachShader(program, fragmentShader);
        Gl.glLinkProgram(program);
        Gl.glGetProgramiv(program, GL_LINK_STATUS, out var linked);
        if (linked == 0)
        {
            Console.WriteLine($"  link failed: {InfoLog(program, program: true)}");
            return;
        }

        Gl.glUseProgram(program);
        var blockIndex = Gl.glGetUniformBlockIndex(program, "Block");
        if (blockIndex == GL_INVALID_INDEX)
        {
            Console.WriteLine("  block \"Block\" not found (optimized out?)");
            return;
        }

        // Values with no symmetry, so a transpose cannot look like a match by accident: every
        // element differs from its mirror across the diagonal.
        var matrix = new float[16];
        for (var i = 0; i < 16; i++)
            matrix[i] = (i + 1) * 1.5f + i * i * 0.25f;

        Gl.glGenBuffers(1, out var ubo);
        Gl.glBindBuffer(GL_UNIFORM_BUFFER, ubo);
        fixed (float* mp = matrix)
            Gl.glBufferData(GL_UNIFORM_BUFFER, matrix.Length * 4, (IntPtr)mp, GL_STATIC_DRAW);
        Gl.glUniformBlockBinding(program, blockIndex, 0);
        Gl.glBindBufferBase(GL_UNIFORM_BUFFER, 0, ubo);

        Gl.glGenFramebuffers(1, out var fbo);
        Gl.glBindFramebuffer(GL_FRAMEBUFFER, fbo);
        Gl.glGenTextures(1, out var target);
        Gl.glBindTexture(GL_TEXTURE_2D, target);
        Gl.glTexImage2D(GL_TEXTURE_2D, 0, GL_RGBA32F, 1, 1, 0, GL_RGBA, GL_FLOAT, IntPtr.Zero);
        Gl.glFramebufferTexture2D(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, GL_TEXTURE_2D, target, 0);
        if (Gl.glCheckFramebufferStatus(GL_FRAMEBUFFER) != GL_FRAMEBUFFER_COMPLETE)
        {
            Console.WriteLine("  readback framebuffer incomplete - matrix check skipped");
            return;
        }

        Gl.glViewport(0, 0, 1, 1);
        var twoTriangles = new[] { -1f, -1f, 3f, -1f, -1f, 3f };
        Gl.glGenBuffers(1, out var vbo);
        Gl.glBindBuffer(GL_ARRAY_BUFFER, vbo);
        fixed (float* vp = twoTriangles)
            Gl.glBufferData(GL_ARRAY_BUFFER, twoTriangles.Length * 4, (IntPtr)vp, GL_STATIC_DRAW);
        Gl.glEnableVertexAttribArray(0);
        Gl.glVertexAttribPointer(0, 2, GL_FLOAT, 0, 8, IntPtr.Zero);
        Gl.glDrawArrays(GL_TRIANGLES, 0, 3);
        Gl.glFinish();

        var pixel = new float[4];
        fixed (float* pp = pixel)
            Gl.glReadPixels(0, 0, 1, 1, GL_RGBA, GL_FLOAT, (IntPtr)pp);

        // The D3D reference, computed here from the same bytes: V(i,j) = b[j*4+i], and
        // mul(v, V)_j = sum_i v_i * V(i,j) with v = (1,0,0,0), so the result is V(0,j) for each j.
        var d3d = new float[4];
        for (var j = 0; j < 4; j++)
            d3d[j] = matrix[j * 4 + 0];

        var ok = true;
        for (var i = 0; i < 4; i++)
            ok &= MathF.Abs(pixel[i] - d3d[i]) < 0.01f;

        Console.WriteLine($"  ES  (row_major, M * v):   ({pixel[0]:F3}, {pixel[1]:F3}, {pixel[2]:F3}, {pixel[3]:F3})");
        Console.WriteLine($"  D3D (column_major, mul):  ({d3d[0]:F3}, {d3d[1]:F3}, {d3d[2]:F3}, {d3d[3]:F3})");
        Console.WriteLine(ok
            ? "  the two conventions agree on the same bytes - transforms are not transposed under ES"
            : "  MATRIX CONVENTIONS DISAGREE - every transform would be transposed under ES");
    }

    private const string BlockVertexSource = """
        #version 300 es
        precision highp float;
        layout(std140) uniform Block
        {
            mat4 Matrix;
            float ScalarA;
            float ScalarB;
            vec3 Direction;
            vec2 Resolution;
        };
        void main()
        {
            gl_Position = Matrix * vec4(Direction * ScalarA, 1.0) + vec4(Resolution, ScalarB, 0.0);
        }
        """;

    /// <summary>
    /// Compiles a pair of shaders shaped like the real ones and reports how long each attempt
    /// takes, so the fixed cost can be told apart from the per-shader cost.
    ///
    /// The shape is taken from the migrated Line shader: a large merged uniform block, a
    /// per-instance world matrix arriving as four consecutive attributes, and a fragment stage
    /// doing three unrolled shadow-map lookups. That matters because the cost being measured is
    /// GLSL-to-HLSL translation plus D3DCompile, and both scale with the shader's size.
    /// </summary>
    private static unsafe uint BenchmarkShaderCompile()
    {
        Console.WriteLine("\n-- shader compile --");

        // The critical distinction is *when* the GLSL->HLSL translation and the D3DCompile run.
        // ANGLE defers a lot of that work, so glLinkProgram returning quickly does not mean the
        // cost was avoided - it may simply have moved to the first draw. All three are timed.
        var first = BuildProgram(VertexSource, FragmentSource);
        Console.WriteLine($"  first pair: compile {first.CompileMs:F1} ms, link {first.LinkMs:F1} ms, use {first.UseMs:F1} ms");

        if (first.Program == 0)
            return 0;

        // A draw is what actually forces the pipeline to be realized on the D3D11 backend.
        var vao = MakeDummyGeometry();
        Gl.glUseProgram(first.Program);
        var drawSw = Stopwatch.StartNew();
        DrawOnce(vao);
        Gl.glFinish();
        drawSw.Stop();
        Console.WriteLine($"  first draw after link: {drawSw.Elapsed.TotalMilliseconds:F1} ms"
            + "   <- deferred cost lands here, not in glLinkProgram");

        // The real per-shader cost: a fresh, *distinct* program each time. The source is perturbed
        // per iteration so a content-addressed cache cannot collapse them together, which is what
        // a game with a dozen different shaders actually pays.
        const int distinct = 15;
        var totalCompile = 0.0;
        var totalLink = 0.0;
        var worst = 0.0;
        for (var i = 0; i < distinct; i++)
        {
            var r = BuildProgram(Perturb(VertexSource, i), Perturb(FragmentSource, i));
            totalCompile += r.CompileMs;
            totalLink += r.LinkMs;
            worst = Math.Max(worst, r.CompileMs + r.LinkMs);
        }
        Console.WriteLine($"  {distinct} distinct shader pairs (compile+link, no draw):");
        Console.WriteLine($"    compile {totalCompile:F1} ms, link {totalLink:F1} ms,"
            + $" total {totalCompile + totalLink:F1} ms (worst pair {worst:F1} ms)");

        // Same source again: two identical builds must share ANGLE's internal shader cache.
        var warmA = BuildProgram(VertexSource, FragmentSource);
        var warmB = BuildProgram(VertexSource, FragmentSource);
        Console.WriteLine($"  identical source rebuilt: {warmA.CompileMs + warmA.LinkMs:F1} ms,"
            + $" then {warmB.CompileMs + warmB.LinkMs:F1} ms");

        // Fixed overhead, to show how much of the above scales with shader complexity.
        var trivial = BuildProgram(TrivialVertexSource, TrivialFragmentSource);
        Console.WriteLine($"  trivial pair: compile {trivial.CompileMs:F1} ms, link {trivial.LinkMs:F1} ms");

        // Whether any of this can be paid once rather than on every launch.
        Console.Write("  GL_OES_get_program_binary: ");
        Gl.glGetIntegerv(GL_NUM_PROGRAM_BINARY_FORMATS, out var binaryFormats);
        Console.WriteLine(binaryFormats > 0
            ? $"yes ({binaryFormats} format(s)) - a translated program can be baked to disk"
            : "no - programs cannot be pre-baked, so the translation cost recurs every launch");

        return first.Program;
    }

    private sealed record BuildResult(uint Program, double CompileMs, double LinkMs, double UseMs);

    private static unsafe BuildResult BuildProgram(string vertexSource, string fragmentSource)
    {
        var compileSw = Stopwatch.StartNew();
        var vertex = CompileStage(GL_VERTEX_SHADER, vertexSource, null);
        var fragment = CompileStage(GL_FRAGMENT_SHADER, fragmentSource, null);
        compileSw.Stop();

        var program = Gl.glCreateProgram();
        Gl.glAttachShader(program, vertex);
        Gl.glAttachShader(program, fragment);

        var linkSw = Stopwatch.StartNew();
        Gl.glLinkProgram(program);
        linkSw.Stop();

        Gl.glGetProgramiv(program, GL_LINK_STATUS, out var linked);

        var useSw = Stopwatch.StartNew();
        if (linked != 0)
        {
            Gl.glUseProgram(program);
            Gl.glGetError();
        }
        useSw.Stop();

        if (linked == 0)
            Console.WriteLine($"  LINK FAILED: {InfoLog(program, program: true)}");

        Gl.glDeleteShader(vertex);
        Gl.glDeleteShader(fragment);

        return new BuildResult(program, compileSw.Elapsed.TotalMilliseconds,
            linkSw.Elapsed.TotalMilliseconds, useSw.Elapsed.TotalMilliseconds);
    }

    /// <summary>
    /// Changes the source text without changing what it computes, so a content-addressed cache
    /// cannot merge the iterations. A comment is enough: it alters the string but not the code.
    /// </summary>
    private static string Perturb(string source, int index) =>
        source.Replace("#version 300 es", $"#version 300 es\n// variant {index}", StringComparison.Ordinal);

    private static unsafe uint MakeDummyGeometry()
    {
        Gl.glGenVertexArrays(1, out var vao);
        Gl.glBindVertexArray(vao);
        Gl.glGenBuffers(1, out var vbo);
        Gl.glBindBuffer(GL_ARRAY_BUFFER, vbo);
        Gl.glBufferData(GL_ARRAY_BUFFER, 60 * 3, IntPtr.Zero, GL_STATIC_DRAW);
        for (var i = 0; i < 7; i++)
        {
            Gl.glEnableVertexAttribArray((uint)i);
            Gl.glVertexAttribPointer((uint)i, i is 2 or 6 ? 1 : 3, GL_FLOAT, 0, 60, (IntPtr)(i * 12));
        }
        Gl.glGenBuffers(1, out var ibo);
        Gl.glBindBuffer(GL_ELEMENT_ARRAY_BUFFER, ibo);
        Gl.glBufferData(GL_ELEMENT_ARRAY_BUFFER, 6, IntPtr.Zero, GL_STATIC_DRAW);
        return vao;
    }

    private static void DrawOnce(uint vao)
    {
        Gl.glBindVertexArray(vao);
        Gl.glDrawElementsInstanced(GL_TRIANGLES, 3, GL_UNSIGNED_SHORT, IntPtr.Zero, 1);
    }

    private static unsafe uint CompileStage(uint type, string source, string? label)
    {
        var sw = Stopwatch.StartNew();
        var shader = Gl.glCreateShader(type);
        var bytes = Encoding.UTF8.GetBytes(source);
        fixed (byte* p = bytes)
        {
            var ptr = (IntPtr)p;
            Gl.glShaderSource(shader, 1, &ptr, null);
        }
        Gl.glCompileShader(shader);
        sw.Stop();

        Gl.glGetShaderiv(shader, GL_COMPILE_STATUS, out var status);
        if (status == 0)
            Console.WriteLine($"  {label} compile FAILED: {InfoLog(shader, program: false)}");
        else if (label is not null)
            Console.WriteLine($"  {label}: {sw.Elapsed.TotalMilliseconds:F1} ms");

        return shader;
    }

    /// <summary>
    /// Per-draw CPU cost. The draws are issued in a tight loop with no glFinish inside, which is
    /// what measures how much work the translation layer adds on the CPU; the glFinish at the end
    /// is reported separately as the GPU-side total.
    /// </summary>
    private static unsafe void BenchmarkDraws(uint program)
    {
        Console.WriteLine("\n-- draw throughput --");
        if (program == 0)
        {
            Console.WriteLine("  skipped: no program");
            return;
        }

        // A 1080p colour+depth target, i.e. what the game renders into.
        Gl.glGenFramebuffers(1, out var fbo);
        Gl.glBindFramebuffer(GL_FRAMEBUFFER, fbo);

        Gl.glGenTextures(1, out var colour);
        Gl.glBindTexture(GL_TEXTURE_2D, colour);
        Gl.glTexImage2D(GL_TEXTURE_2D, 0, GL_RGBA8, 1920, 1080, 0, GL_RGBA, GL_UNSIGNED_BYTE, IntPtr.Zero);
        Gl.glFramebufferTexture2D(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, GL_TEXTURE_2D, colour, 0);

        Gl.glGenRenderbuffers(1, out var depth);
        Gl.glBindRenderbuffer(GL_RENDERBUFFER, depth);
        Gl.glRenderbufferStorage(GL_RENDERBUFFER, GL_DEPTH_COMPONENT24, 1920, 1080);
        Gl.glFramebufferRenderbuffer(GL_FRAMEBUFFER, GL_DEPTH_ATTACHMENT, GL_RENDERBUFFER, depth);

        var fboStatus = Gl.glCheckFramebufferStatus(GL_FRAMEBUFFER);
        if (fboStatus != GL_FRAMEBUFFER_COMPLETE)
        {
            Console.WriteLine($"  framebuffer incomplete (0x{fboStatus:X4}) - draw benchmark skipped");
            return;
        }

        Gl.glViewport(0, 0, 1920, 1080);
        Gl.glEnable(GL_DEPTH_TEST);
        Gl.glUseProgram(program);

        // The Line shader's vertex layout: 7 per-vertex attributes, plus a per-instance mat4
        // (four consecutive locations) and two per-instance vec4s.
        Gl.glGenVertexArrays(1, out var vao);
        Gl.glBindVertexArray(vao);

        var stride = 60;
        Gl.glGenBuffers(1, out var vbo);
        Gl.glBindBuffer(GL_ARRAY_BUFFER, vbo);
        Gl.glBufferData(GL_ARRAY_BUFFER, stride * 3, IntPtr.Zero, GL_STATIC_DRAW);

        // Per-vertex: 3 floats each at locations 0-6 (with a couple of tighter packs).
        var perVertexSizes = new[] { 3, 3, 1, 3, 3, 3, 1 };
        for (var i = 0; i < perVertexSizes.Length; i++)
        {
            Gl.glEnableVertexAttribArray((uint)i);
            Gl.glVertexAttribPointer((uint)i, perVertexSizes[i], GL_FLOAT, 0, stride, (IntPtr)(i * 12));
        }

        var instanceStride = 96;
        Gl.glGenBuffers(1, out var instanceVbo);
        Gl.glBindBuffer(GL_ARRAY_BUFFER, instanceVbo);
        Gl.glBufferData(GL_ARRAY_BUFFER, instanceStride * 256, IntPtr.Zero, GL_STATIC_DRAW);
        for (var i = 0; i < 6; i++)
        {
            var location = (uint)(7 + i);
            Gl.glEnableVertexAttribArray(location);
            Gl.glVertexAttribPointer(location, 4, GL_FLOAT, 0, instanceStride, (IntPtr)(i * 16));
            Gl.glVertexAttribDivisor(location, 1);
        }

        Gl.glGenBuffers(1, out var ibo);
        Gl.glBindBuffer(GL_ELEMENT_ARRAY_BUFFER, ibo);
        Gl.glBufferData(GL_ELEMENT_ARRAY_BUFFER, 3 * 2, IntPtr.Zero, GL_STATIC_DRAW);

        var batch = 0;
        Gl.glGenBuffers(1, out var ubos);
        Gl.glBindBuffer(GL_UNIFORM_BUFFER, ubos);
        Gl.glBufferData(GL_UNIFORM_BUFFER, 512, IntPtr.Zero, GL_DYNAMIC_DRAW);
        var blockIndex = Gl.glGetUniformBlockIndex(program, "_Global");
        Gl.glUniformBlockBinding(program, blockIndex, 0);
        Gl.glBindBufferBase(GL_UNIFORM_BUFFER, 0, ubos);

        const int draws = 10_000;
        const int instancesPerDraw = 8;

        // Warm up, so the first-call path (lazy state setup, shader patching) is not what the
        // measurement reports.
        for (var i = 0; i < 100; i++)
            Gl.glDrawElementsInstanced(GL_TRIANGLES, 3, GL_UNSIGNED_SHORT, IntPtr.Zero, instancesPerDraw);
        Gl.glFinish();

        var cpuSw = Stopwatch.StartNew();
        for (var i = 0; i < draws; i++)
            Gl.glDrawElementsInstanced(GL_TRIANGLES, 3, GL_UNSIGNED_SHORT, IntPtr.Zero, instancesPerDraw);
        cpuSw.Stop();

        var finishSw = Stopwatch.StartNew();
        Gl.glFinish();
        finishSw.Stop();

        Console.WriteLine($"  {draws} draws x {instancesPerDraw} instances (80k triangles):");
        Console.WriteLine($"    CPU submit: {cpuSw.Elapsed.TotalMilliseconds:F1} ms"
            + $"  ({cpuSw.Elapsed.TotalMilliseconds * 1000 / draws:F2} us/draw)");
        Console.WriteLine($"    GPU finish: {finishSw.Elapsed.TotalMilliseconds:F1} ms");

        // A single draw's latency when the pipeline is drained, which is what a frame with few
        // draws actually pays.
        Gl.glFinish();
        var singleSw = Stopwatch.StartNew();
        for (var i = 0; i < 100; i++)
        {
            Gl.glDrawElementsInstanced(GL_TRIANGLES, 3, GL_UNSIGNED_SHORT, IntPtr.Zero, instancesPerDraw);
            Gl.glFinish();
        }
        singleSw.Stop();
        Console.WriteLine($"    synchronous (finish per draw): {singleSw.Elapsed.TotalMilliseconds / 100:F2} ms/draw");

        // The loop above is the best case: nothing changes between draws, so ANGLE's validation
        // and state-shadowing all hit their fast paths. A real frame is nothing like that - every
        // draw changes uniforms and often the program or the texture - so this repeats it with
        // the state churn a renderer actually produces.
        Console.WriteLine("\n  with realistic per-draw state churn:");
        var churnSw = Stopwatch.StartNew();
        for (var i = 0; i < draws; i++)
        {
            Gl.glBindBuffer(GL_UNIFORM_BUFFER, ubos);
            Gl.glBufferSubData(GL_UNIFORM_BUFFER, 0, 64, IntPtr.Zero);
            Gl.glBindBufferBase(GL_UNIFORM_BUFFER, 0, ubos);
            Gl.glActiveTexture(GL_TEXTURE0 + (uint)(i & 1));
            Gl.glBindTexture(GL_TEXTURE_2D, colour);
            Gl.glDrawElementsInstanced(GL_TRIANGLES, 3, GL_UNSIGNED_SHORT, IntPtr.Zero, instancesPerDraw);
        }
        churnSw.Stop();
        Gl.glFinish();
        Console.WriteLine($"    {draws} draws with a uniform upload + texture rebind each:"
            + $" {churnSw.Elapsed.TotalMilliseconds:F1} ms"
            + $"  ({churnSw.Elapsed.TotalMilliseconds * 1000 / draws:F2} us/draw)");

        _ = batch;
    }

    private static unsafe string String(byte* value) =>
        value == null ? "(null)" : Marshal.PtrToStringUTF8((nint)value) ?? "(null)";

    /// <summary>
    /// Compiles and links the shader compiler's own ES output, one program per shader, under real
    /// ANGLE.
    ///
    /// Compiling is the meaningful test rather than linking alone: ANGLE validates the source in
    /// glCompileShader, and every ES 3.0 restriction an HLSL-first compiler could plausibly miss -
    /// a 3.10 builtin, a missing default precision - is a compile error rather than a link one.
    /// Linking is still run because a vertex input the fragment stage agrees on, a varying without
    /// a matching declaration, or a uniform block that survives dead-code elimination differently
    /// in the two stages all surface only there.
    ///
    /// The shader pairs are discovered from the directory, so this stays in step with whatever the
    /// compiler emits rather than with a list written here.
    /// </summary>
    /// <summary>
    /// Brings up a context and hands it to <see cref="CheckGeneratedSources"/>. Split out of
    /// <c>Main</c> so the display/context locals do not collide with the measurement path's own.
    /// </summary>
    private static int RunEsCheck(string directory)
    {
        var display = CreateDisplay();
        if (display == IntPtr.Zero || Egl.eglInitialize(display, out _, out _) != EGL_TRUE)
        {
            Console.WriteLine("no ANGLE display.");
            return 1;
        }

        if (!CreateContext(display, out var context, out var surface))
            return 1;

        Egl.eglMakeCurrent(display, surface, surface, context);
        var result = CheckGeneratedSources(directory);
        Egl.eglMakeCurrent(display, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        return result;
    }

    /// <summary>
    /// The extent of each bundle's uniform block, taken from the generated <c>.g.cs</c> reflection
    /// rather than restated here - a table written into this file would agree with itself and prove
    /// nothing. Read out of the emitted <c>new("Name", offset, size, UniformType.X)</c> entries.
    /// </summary>
    private static readonly Dictionary<string, int> ReflectionBlockSizes = LoadReflectionBlockSizes();

    private static Dictionary<string, int> LoadReflectionBlockSizes()
    {
        var sizes = new Dictionary<string, int>(StringComparer.Ordinal);

        // The bundles sit in ShaderPoc/generated; this probe is a sibling directory, and the
        // generated dir is passed in as the ES dump target by the usual invocation, so look there.
        var generated = Environment.GetEnvironmentVariable("SHADERPOC_GENERATED")
            ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ShaderPoc", "generated");
        if (!Directory.Exists(generated))
            return sizes;

        foreach (var file in Directory.GetFiles(generated, "*.g.cs"))
        {
            var text = File.ReadAllText(file);
            var end = 0;
            foreach (System.Text.RegularExpressions.Match m in
                     System.Text.RegularExpressions.Regex.Matches(
                         text, @"new\(""[A-Za-z_0-9]+"", (\d+), (\d+), UniformType\."))
            {
                end = Math.Max(end, int.Parse(m.Groups[1].Value) + int.Parse(m.Groups[2].Value));
            }

            if (end > 0)
                sizes[Path.GetFileNameWithoutExtension(file).Replace(".g", "")] = end;
        }

        return sizes;
    }

    /// <summary>The reflection's block extent for a shader, or -1 if the reflection is unavailable.</summary>
    private static int ExpectedBlockSize(string name) =>
        ReflectionBlockSizes.GetValueOrDefault(name, -1);

    private static unsafe int CheckGeneratedSources(string directory)
    {
        Console.WriteLine($"== compiling the generated ES 3.0 output from {directory} ==\n");

        var vertexFiles = Directory.GetFiles(directory, "*.Vertex.es.glsl").Order().ToArray();
        const string suffix = ".Vertex.es.glsl";
        if (vertexFiles.Length == 0)
        {
            Console.WriteLine("no *.Vertex.es.glsl files - run the compiler with --dump-sources first.");
            return 2;
        }

        var failures = 0;
        var totalMs = 0.0;

        foreach (var vertexFile in vertexFiles)
        {
            var name = Path.GetFileName(vertexFile)[..^suffix.Length];
            var fragmentFile = Path.Combine(directory, $"{name}.Fragment.es.glsl");
            if (!File.Exists(fragmentFile))
            {
                Console.WriteLine($"  {name,-10} no fragment source");
                failures++;
                continue;
            }

            var vertexSource = File.ReadAllText(vertexFile);
            var fragmentSource = File.ReadAllText(fragmentFile);

            var sw = Stopwatch.StartNew();
            var vertex = CompileStage(GL_VERTEX_SHADER, vertexSource, null);
            var fragment = CompileStage(GL_FRAGMENT_SHADER, fragmentSource, null);

            Gl.glGetShaderiv(vertex, GL_COMPILE_STATUS, out var vertexOk);
            Gl.glGetShaderiv(fragment, GL_COMPILE_STATUS, out var fragmentOk);

            var program = Gl.glCreateProgram();
            Gl.glAttachShader(program, vertex);
            Gl.glAttachShader(program, fragment);
            Gl.glLinkProgram(program);
            Gl.glGetProgramiv(program, GL_LINK_STATUS, out var linked);
            sw.Stop();
            totalMs += sw.Elapsed.TotalMilliseconds;

            // The driver's own std140 size for the block against the size the generated reflection
            // implies. This is the per-shader version of the std140 check elsewhere in this probe:
            // that one proves the rules for a hand-written block, this one proves every block the
            // game actually ships packs to the same extent the command buffer writes.
            var blockSize = 0;
            var blockIndex = linked != 0 ? Gl.glGetUniformBlockIndex(program, "_Global") : GL_INVALID_INDEX;
            if (blockIndex != GL_INVALID_INDEX)
                Gl.glGetActiveUniformBlockiv(program, blockIndex, GL_UNIFORM_BLOCK_DATA_SIZE, out blockSize);

            var expected = ExpectedBlockSize(name);
            var sizeOk = expected < 0 || blockSize == expected;

            var ok = vertexOk != 0 && fragmentOk != 0 && linked != 0 && sizeOk;
            if (!ok) failures++;

            Console.WriteLine($"  {name,-10} vertex {(vertexOk != 0 ? "ok" : "FAIL")}"
                + $"   fragment {(fragmentOk != 0 ? "ok" : "FAIL")}"
                + $"   link {(linked != 0 ? "ok" : "FAIL")}"
                + (blockIndex == GL_INVALID_INDEX
                    ? "   block optimised out"
                    : $"   block {blockSize} B vs reflection {expected} B {(sizeOk ? "ok" : "DIFFER")}")
                + $"   {sw.Elapsed.TotalMilliseconds,6:F1} ms");

            if (vertexOk == 0) Console.WriteLine($"    [vs] {InfoLog(vertex, program: false)}");
            if (fragmentOk == 0) Console.WriteLine($"    [fs] {InfoLog(fragment, program: false)}");
            if (linked == 0) Console.WriteLine($"    [link] {InfoLog(program, program: true)}");

            Gl.glDeleteShader(vertex);
            Gl.glDeleteShader(fragment);
        }

        Console.WriteLine();
        Console.WriteLine(failures == 0
            ? $"  all {vertexFiles.Length} generated shader programs compile and link under ANGLE ES 3.0"
              + $" ({totalMs:F1} ms total, {totalMs / vertexFiles.Length:F1} ms each)"
            : $"  {failures} of {vertexFiles.Length} generated shader programs FAILED");

        return failures == 0 ? 0 : 1;
    }

    private static unsafe string InfoLog(uint handle, bool program)
    {
        const int LogSize = 4096;
        var buffer = stackalloc byte[LogSize];
        if (program)
        {
            Gl.glGetProgramInfoLog(handle, LogSize, out var written, buffer);
            return Encoding.UTF8.GetString(buffer, Math.Max(0, written - 1));
        }
        Gl.glGetShaderInfoLog(handle, LogSize, out var count, buffer);
        return Encoding.UTF8.GetString(buffer, Math.Max(0, count - 1));
    }

    // ---------------------------------------------------------------------------------------
    // Shader sources. ES 3.0: `#version 300 es`, explicit precision (required in the fragment
    // stage), and layout qualifiers on every vertex input.
    // ---------------------------------------------------------------------------------------

    private const string UniformBlock = """
        layout(std140) uniform _Global
        {
            mat4 LightViewProj0;
            mat4 LightViewProj1;
            mat4 LightViewProj2;
            float DepthBias;
            float NumCascades;
            vec3 LightDirection;
            mat4 View;
            mat4 Projection;
            mat4 ViewProj;
            vec3 SnapColor;
            uint IsFullbright;
            uint UseBaseColor;
            vec3 BaseColor;
            vec3 FogColor;
            float FogDistance;
            float FogLogDensity;
            vec2 EnvironmentLight;
            vec3 CameraPosition;
            float Alpha;
            uint Expand;
            float RandomFloat;
            float Darken;
            float ChargedBlinkAmount;
            float HalfThickness;
            vec2 Resolution;
            float OutlineFalloffStartDistance;
            float OutlineFalloffCutoffDistance;
            float OutlineFalloffLinearFadeStartDistance;
        };
        """;

    private const string VertexSource = """
        #version 300 es
        precision highp float;
        precision highp int;
        """ + "\n" + UniformBlock + """

        layout(location = 0) in vec3 input_PositionA;
        layout(location = 1) in vec3 input_PositionB;
        layout(location = 2) in float input_Side;
        layout(location = 3) in vec3 input_Normal;
        layout(location = 4) in vec3 input_Color;
        layout(location = 5) in vec3 input_Centroid;
        layout(location = 6) in float input_DecalOffset;
        layout(location = 7) in mat4 world;
        layout(location = 11) in vec4 parameters;
        layout(location = 12) in vec4 parameters2;

        out vec4 vColor;
        out vec3 vNormal;
        out vec3 vWorldPos;

        void main()
        {
            vec3 local = mix(input_PositionA, input_PositionB, clamp(input_Side, 0.0, 1.0));
            vec4 worldPos = world * vec4(local, 1.0);
            worldPos.xyz += input_Normal * (parameters2.x * 0.5);
            gl_Position = ViewProj * worldPos;
            vColor = vec4(input_Color, 1.0) * parameters.x;
            vNormal = normalize(mat3(world) * input_Normal);
            vWorldPos = worldPos.xyz;
        }
        """;

    private const string FragmentSource = """
        #version 300 es
        precision highp float;
        precision highp int;
        precision highp sampler2D;
        """ + "\n" + UniformBlock + """

        uniform sampler2D ShadowMap0;
        uniform sampler2D ShadowMap1;
        uniform sampler2D ShadowMap2;

        in vec4 vColor;
        in vec3 vNormal;
        in vec3 vWorldPos;
        out vec4 fragColor;

        float shadowLookup(sampler2D map, mat4 lightViewProj, float bias)
        {
            vec4 projected = lightViewProj * vec4(vWorldPos, 1.0);
            vec3 coord = projected.xyz / projected.w;
            coord = coord * 0.5 + 0.5;
            if (coord.x < 0.0 || coord.x > 1.0 || coord.y < 0.0 || coord.y > 1.0) return 1.0;
            float closest = texture(map, coord.xy).x;
            float current = coord.z - bias;
            float occluded = step(current, closest);
            return mix(0.35, 1.0, occluded);
        }

        void main()
        {
            float lit = 0.0;
            if (NumCascades > 0.5)
            {
                lit += shadowLookup(ShadowMap0, LightViewProj0, DepthBias);
                lit += shadowLookup(ShadowMap1, LightViewProj1, DepthBias);
                lit += shadowLookup(ShadowMap2, LightViewProj2, DepthBias);
                lit /= max(1.0, NumCascades);
            }
            else
            {
                // No cascade fit: a fifth-order falloff, which keeps the loop/fma count realistic
                // rather than letting D3DCompile fold the whole branch away.
                float d = 0.0;
                for (int i = 0; i < 5; i++)
                {
                    d = d * 0.5 + 0.125;
                    lit = lit * 0.5 + d;
                }
            }

            // `fma` is GLSL ES 3.10 and this context is 3.00, so the multiply-add idiom is
            // written longhand - see the capability report.
            vec3 normal = normalize(vNormal);
            float ndotl = clamp(dot(normal, -normalize(LightDirection)), 0.0, 1.0);
            vec3 base = mix(BaseColor, vColor.rgb, float(UseBaseColor));
            vec3 colour = base * (EnvironmentLight.x + EnvironmentLight.y * ndotl) * lit;
            colour = mix(colour, FogColor, clamp(FogLogDensity * FogDistance, 0.0, 1.0));
            float alpha = Alpha * vColor.a;
            fragColor = vec4(colour, alpha);
        }
        """;

    private const string TrivialVertexSource = """
        #version 300 es
        precision highp float;
        layout(location = 0) in vec2 position;
        void main() { gl_Position = vec4(position, 0.0, 1.0); }
        """;

    private const string TrivialFragmentSource = """
        #version 300 es
        precision highp float;
        uniform vec4 Color;
        out vec4 fragColor;
        void main() { fragColor = Color; }
        """;

    // ---------------------------------------------------------------------------------------
    // EGL
    // ---------------------------------------------------------------------------------------

    private const int EGL_TRUE = 1;
    private const int EGL_NONE = 0x3038;
    private const int EGL_VENDOR = 0x3053;
    private const int EGL_PBUFFER_BIT = 0x0001;
    private const int EGL_RED_SIZE = 0x3024;
    private const int EGL_GREEN_SIZE = 0x3023;
    private const int EGL_BLUE_SIZE = 0x3022;
    private const int EGL_ALPHA_SIZE = 0x3021;
    private const int EGL_DEPTH_SIZE = 0x3025;
    private const int EGL_SURFACE_TYPE = 0x3033;
    private const int EGL_RENDERABLE_TYPE = 0x3040;
    private const int EGL_OPENGL_ES3_BIT = 0x0040;
    private const int EGL_CONTEXT_CLIENT_VERSION = 0x3098;
    private const int EGL_WIDTH = 0x3057;
    private const int EGL_HEIGHT = 0x3056;
    private const int EGL_PLATFORM_ANGLE_ANGLE = 0x3202;
    private const int EGL_PLATFORM_ANGLE_TYPE_ANGLE = 0x3203;
    private const int EGL_PLATFORM_ANGLE_TYPE_D3D11_ANGLE = 0x3208;
    private const int EGL_PLATFORM_ANGLE_DEVICE_TYPE_ANGLE = 0x3209;
    private const int EGL_PLATFORM_ANGLE_DEVICE_TYPE_HARDWARE_ANGLE = 0x320A;

    private static class Egl
    {
        private const string Lib = "libEGL.dll";

        [DllImport(Lib, EntryPoint = "eglGetError")]
        public static extern int eglGetError();

        // ANGLE exports this one directly, so no eglGetProcAddress indirection is needed.
        // (An entry point decorated with CharSet.Ansi but not ExactSpelling makes the CLR look
        // for an "A"-suffixed name, which is why a lookup here fails.)
        [DllImport(Lib, EntryPoint = "eglGetPlatformDisplayEXT")]
        public static extern IntPtr eglGetPlatformDisplayEXT(int platform, IntPtr nativeDisplay, IntPtr attribs);

        [DllImport(Lib, EntryPoint = "eglInitialize")]
        public static extern int eglInitialize(IntPtr display, out int major, out int minor);

        [DllImport(Lib, EntryPoint = "eglQueryString")]
        private static extern unsafe byte* eglQueryStringNative(IntPtr display, int name);

        public static unsafe byte* eglQueryString(IntPtr display, int name) =>
            eglQueryStringNative(display, name);

        [DllImport(Lib, EntryPoint = "eglChooseConfig")]
        public static extern int eglChooseConfig(IntPtr display, IntPtr attribs, out IntPtr configs, int configSize, out int count);

        [DllImport(Lib, EntryPoint = "eglCreateContext")]
        public static extern IntPtr eglCreateContext(IntPtr display, IntPtr config, IntPtr share, IntPtr attribs);

        [DllImport(Lib, EntryPoint = "eglCreatePbufferSurface")]
        public static extern IntPtr eglCreatePbufferSurface(IntPtr display, IntPtr config, IntPtr attribs);

        [DllImport(Lib, EntryPoint = "eglMakeCurrent")]
        public static extern int eglMakeCurrent(IntPtr display, IntPtr draw, IntPtr read, IntPtr context);
    }

    // ---------------------------------------------------------------------------------------
    // GLES 3.0 - the core entry points are exported directly by ANGLE's libGLESv2.
    // ---------------------------------------------------------------------------------------

    private const int GL_VENDOR = 0x1F00;
    private const int GL_RENDERER = 0x1F01;
    private const int GL_VERSION = 0x1F02;
    private const int GL_EXTENSIONS = 0x1F03;
    private const int GL_SHADING_LANGUAGE_VERSION = 0x8B8C;
    private const int GL_MAX_TEXTURE_SIZE = 0x0D33;
    private const int GL_MAX_VERTEX_ATTRIBS = 0x8869;
    private const int GL_MAX_DRAW_BUFFERS = 0x8824;
    private const int GL_MAX_UNIFORM_BUFFER_BINDINGS = 0x8A2F;
    private const int GL_MAX_SAMPLES = 0x8D57;
    private const int GL_NUM_PROGRAM_BINARY_FORMATS = 0x87FE;
    private const int GL_ACTIVE_UNIFORMS = 0x8B86;
    private const int GL_UNIFORM_OFFSET = 0x8A3B;
    private const int GL_UNIFORM_BLOCK_DATA_SIZE = 0x8A40;
    private const uint GL_INVALID_INDEX = 0xFFFFFFFF;
    private const int GL_VERTEX_SHADER = 0x8B31;
    private const int GL_FRAGMENT_SHADER = 0x8B30;
    private const int GL_COMPILE_STATUS = 0x8B81;
    private const int GL_LINK_STATUS = 0x8B82;
    private const int GL_ARRAY_BUFFER = 0x8892;
    private const int GL_ELEMENT_ARRAY_BUFFER = 0x8893;
    private const int GL_UNIFORM_BUFFER = 0x8A11;
    private const int GL_STATIC_DRAW = 0x88E4;
    private const int GL_DYNAMIC_DRAW = 0x88E8;
    private const int GL_FLOAT = 0x1406;
    private const int GL_UNSIGNED_SHORT = 0x1403;
    private const int GL_UNSIGNED_BYTE = 0x1401;
    private const int GL_TRIANGLES = 0x0004;
    private const int GL_FRAMEBUFFER = 0x8D40;
    private const int GL_RENDERBUFFER = 0x8D41;
    private const int GL_COLOR_ATTACHMENT0 = 0x8CE0;
    private const int GL_DEPTH_ATTACHMENT = 0x8D00;
    private const int GL_FRAMEBUFFER_COMPLETE = 0x8CD5;
    private const int GL_TEXTURE_2D = 0x0DE1;
    private const int GL_TEXTURE0 = 0x84C0;
    private const int GL_RGBA = 0x1908;
    private const int GL_RGBA8 = 0x8058;
    private const int GL_RGBA32F = 0x8814;
    private const int GL_TEXTURE1 = 0x84C1;
    private const int GL_DEPTH_COMPONENT24 = 0x81A6;
    private const int GL_DEPTH_TEST = 0x0B71;

    private static class Gl
    {
        private const string Lib = "libGLESv2.dll";

        [DllImport(Lib, EntryPoint = "glGetString")]
        private static extern unsafe byte* glGetStringNative(int name);

        public static unsafe byte* glGetString(int name) => glGetStringNative(name);

        [DllImport(Lib, EntryPoint = "glGetIntegerv")]
        public static extern void glGetIntegerv(int name, out int value);

        [DllImport(Lib, EntryPoint = "glGetError")]
        public static extern int glGetError();

        [DllImport(Lib, EntryPoint = "glCreateShader")]
        public static extern uint glCreateShader(uint type);

        [DllImport(Lib, EntryPoint = "glShaderSource")]
        public static extern unsafe void glShaderSource(uint shader, int count, IntPtr* strings, int* lengths);

        [DllImport(Lib, EntryPoint = "glCompileShader")]
        public static extern void glCompileShader(uint shader);

        [DllImport(Lib, EntryPoint = "glGetShaderiv")]
        public static extern void glGetShaderiv(uint shader, int name, out int value);

        [DllImport(Lib, EntryPoint = "glGetShaderInfoLog")]
        public static extern unsafe void glGetShaderInfoLog(uint shader, int max, out int written, byte* log);

        [DllImport(Lib, EntryPoint = "glCreateProgram")]
        public static extern uint glCreateProgram();

        [DllImport(Lib, EntryPoint = "glAttachShader")]
        public static extern void glAttachShader(uint program, uint shader);

        [DllImport(Lib, EntryPoint = "glDeleteShader")]
        public static extern void glDeleteShader(uint shader);

        [DllImport(Lib, EntryPoint = "glLinkProgram")]
        public static extern void glLinkProgram(uint program);

        [DllImport(Lib, EntryPoint = "glGetProgramiv")]
        public static extern void glGetProgramiv(uint program, int name, out int value);

        [DllImport(Lib, EntryPoint = "glGetProgramInfoLog")]
        public static extern unsafe void glGetProgramInfoLog(uint program, int max, out int written, byte* log);

        [DllImport(Lib, EntryPoint = "glUseProgram")]
        public static extern void glUseProgram(uint program);

        [DllImport(Lib, EntryPoint = "glGetUniformBlockIndex", CharSet = CharSet.Ansi)]
        public static extern uint glGetUniformBlockIndex(uint program, string name);

        [DllImport(Lib, EntryPoint = "glUniformBlockBinding")]
        public static extern void glUniformBlockBinding(uint program, uint index, uint binding);

        [DllImport(Lib, EntryPoint = "glGetActiveUniformBlockiv")]
        public static extern void glGetActiveUniformBlockiv(uint program, uint index, int pname, out int value);

        [DllImport(Lib, EntryPoint = "glGetActiveUniform")]
        public static extern unsafe void glGetActiveUniform(uint program, uint index, int maxLength, out int length, out int size, out uint type, ref byte* name);

        [DllImport(Lib, EntryPoint = "glGetActiveUniformsiv")]
        public static extern unsafe void glGetActiveUniformsiv(uint program, int count, uint* indices, int pname, int* values);

        [DllImport(Lib, EntryPoint = "glGenVertexArrays")]
        public static extern void glGenVertexArrays(int count, out uint arrays);

        [DllImport(Lib, EntryPoint = "glBindVertexArray")]
        public static extern void glBindVertexArray(uint array);

        [DllImport(Lib, EntryPoint = "glGenBuffers")]
        public static extern void glGenBuffers(int count, out uint buffers);

        [DllImport(Lib, EntryPoint = "glBindBuffer")]
        public static extern void glBindBuffer(int target, uint buffer);

        [DllImport(Lib, EntryPoint = "glBufferData")]
        public static extern void glBufferData(int target, nint size, IntPtr data, int usage);

        [DllImport(Lib, EntryPoint = "glBindBufferBase")]
        public static extern void glBindBufferBase(int target, uint index, uint buffer);

        [DllImport(Lib, EntryPoint = "glEnableVertexAttribArray")]
        public static extern void glEnableVertexAttribArray(uint index);

        [DllImport(Lib, EntryPoint = "glVertexAttribPointer")]
        public static extern void glVertexAttribPointer(uint index, int size, int type, byte normalized, int stride, IntPtr pointer);

        [DllImport(Lib, EntryPoint = "glVertexAttribDivisor")]
        public static extern void glVertexAttribDivisor(uint index, uint divisor);

        [DllImport(Lib, EntryPoint = "glDrawElementsInstanced")]
        public static extern void glDrawElementsInstanced(int mode, int count, int type, IntPtr indices, int instances);

        [DllImport(Lib, EntryPoint = "glDrawArrays")]
        public static extern void glDrawArrays(int mode, int first, int count);

        [DllImport(Lib, EntryPoint = "glReadPixels")]
        public static extern void glReadPixels(int x, int y, int width, int height, int format, int type, IntPtr pixels);

        [DllImport(Lib, EntryPoint = "glFinish")]
        public static extern void glFinish();

        [DllImport(Lib, EntryPoint = "glViewport")]
        public static extern void glViewport(int x, int y, int width, int height);

        [DllImport(Lib, EntryPoint = "glEnable")]
        public static extern void glEnable(int capability);

        [DllImport(Lib, EntryPoint = "glGenFramebuffers")]
        public static extern void glGenFramebuffers(int count, out uint framebuffers);

        [DllImport(Lib, EntryPoint = "glBindFramebuffer")]
        public static extern void glBindFramebuffer(int target, uint framebuffer);

        [DllImport(Lib, EntryPoint = "glFramebufferTexture2D")]
        public static extern void glFramebufferTexture2D(int target, int attachment, int textarget, uint texture, int level);

        [DllImport(Lib, EntryPoint = "glRenderbufferStorageMultisample")]
        public static extern void glRenderbufferStorageMultisample(int target, int samples, int format, int width, int height);

        [DllImport(Lib, EntryPoint = "glBufferSubData")]
        public static extern void glBufferSubData(int target, nint offset, nint size, IntPtr data);

        [DllImport(Lib, EntryPoint = "glActiveTexture")]
        public static extern void glActiveTexture(uint texture);

        [DllImport(Lib, EntryPoint = "glGenRenderbuffers")]
        public static extern void glGenRenderbuffers(int count, out uint renderbuffers);

        [DllImport(Lib, EntryPoint = "glBindRenderbuffer")]
        public static extern void glBindRenderbuffer(int target, uint renderbuffer);

        [DllImport(Lib, EntryPoint = "glRenderbufferStorage")]
        public static extern void glRenderbufferStorage(int target, int format, int width, int height);

        [DllImport(Lib, EntryPoint = "glFramebufferRenderbuffer")]
        public static extern void glFramebufferRenderbuffer(int target, int attachment, int renderbuffertarget, uint renderbuffer);

        [DllImport(Lib, EntryPoint = "glCheckFramebufferStatus")]
        public static extern int glCheckFramebufferStatus(int target);

        [DllImport(Lib, EntryPoint = "glGenTextures")]
        public static extern void glGenTextures(int count, out uint textures);

        [DllImport(Lib, EntryPoint = "glBindTexture")]
        public static extern void glBindTexture(int target, uint texture);

        [DllImport(Lib, EntryPoint = "glTexImage2D")]
        public static extern void glTexImage2D(int target, int level, int internalFormat, int width, int height, int border, int format, int type, IntPtr pixels);
    }
}
