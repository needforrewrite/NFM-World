// A one-off harness for the GLCORE path, not part of the game.
//
// Why it exists: sokol reports "GL_SHADER_COMPILATION_FAILED" through sg_logger and puts the
// driver's actual GLSL diagnostic in a *separate* _SG_LOGMSG at log level 3. The game's logger
// dropped that level, so the only thing a failed run could show was that something failed. This
// program installs a logger that prints everything, builds the real generated bundles through the
// real sokol.dll, and so surfaces the compiler's own message.
//
// It reads the bundles by reflection because they are internal to the game assembly's compile, and
// it drives sokol directly rather than through SokolGraphicsDevice - which is where the uniform-block
// split lives and which needs a window anyway.
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SharpSokol.Native;

namespace GlslValidate;

internal static unsafe class Program
{
    private static readonly List<string> Messages = [];
    private static bool _sawFailure;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void Logger(
        sbyte* tag, uint level, uint item, sbyte* message, uint lineNr, sbyte* file, void* user)
    {
        var msg = message is null ? "" : Marshal.PtrToStringUTF8((nint)message) ?? "";
        var where = file is null ? "?" : Marshal.PtrToStringUTF8((nint)file) ?? "?";
        var text = msg.Length == 0
            ? $"[lvl{level} item{item} {where}:{lineNr}]"
            : $"[lvl{level} item{item}] {msg}";
        Messages.Add(text);
        if (msg.Length > 0) Console.WriteLine("      " + msg.TrimEnd());
    }

    private static int Main()
    {
        var desc = new sg_desc
        {
            environment = default,
            logger = new sg_logger
            {
                func = (delegate* unmanaged[Cdecl]<sbyte*, uint, uint, sbyte*, uint, sbyte*, void*, void>)&Logger,
            },
        };
        Gfx.setup(&desc);
        Console.WriteLine($"backend: {Gfx.query_backend()}");
        Console.WriteLine();

        // The generated bundle types are internal, so they are reached by name.
        var assembly = typeof(Program).Assembly;
        var programs = new[] { "LineBasic", "PolyBasic", "GroundFullbright", "ParticleFullbright", "ImGuiFullbright" };

        var failures = 0;
        foreach (var name in programs)
        {
            var type = assembly.GetType($"NFMWorld.Shaders.Generated.{name}")
                ?? assembly.GetTypes().FirstOrDefault(t => t.Name == name);
            if (type is null) { Console.WriteLine($"{name}: TYPE NOT FOUND"); failures++; continue; }

            Console.WriteLine($"=== {name} ===");
            if (!Check(type, "vertex", "VertexGlsl", sg_shader_stage.SG_SHADERSTAGE_VERTEX)) failures++;
            if (!Check(type, "pixel", "PixelGlsl", sg_shader_stage.SG_SHADERSTAGE_FRAGMENT)) failures++;
        }

        Gfx.shutdown();
        Console.WriteLine();
        Console.WriteLine(failures == 0
            ? "RESULT: every stage compiled clean"
            : $"RESULT: {failures} stage(s) produced log output");
        return failures == 0 ? 0 : 1;
    }

    private static bool Check(Type type, string stage, string field, sg_shader_stage sgStage)
    {
        var source = type
            .GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            ?.GetValue(null) as string;
        if (source is null) { Console.WriteLine($"  {stage}: {field} NOT FOUND"); return false; }

        Messages.Clear();
        var shaderDesc = new sg_shader_desc();
        var func = new sg_shader_function { source = Utf8(source), entry = Utf8("main") };
        if (sgStage == sg_shader_stage.SG_SHADERSTAGE_VERTEX)
            shaderDesc.vertex_func = func;
        else
            shaderDesc.fragment_func = func;

        var shader = Gfx.make_shader(&shaderDesc);
        var rejected = shader.id == 0;
        Console.WriteLine($"  {stage}: {(rejected ? "REJECTED" : "ok")}");
        if (!rejected) Gfx.destroy_shader(shader);
        return !rejected && Messages.Count == 0;
    }

    private static sbyte* Utf8(string s)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(s + "\0");
        var p = (sbyte*)NativeMemory.Alloc((nuint)bytes.Length);
        for (var i = 0; i < bytes.Length; i++) p[i] = (sbyte)bytes[i];
        return p;
    }
}
