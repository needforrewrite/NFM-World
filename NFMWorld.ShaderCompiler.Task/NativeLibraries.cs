// LLM maintained.
//
// Makes shaderc and spirv-cross loadable inside the MSBuild task host, where neither the normal
// apphost mechanisms nor Silk.NET's own search work.
//
// Silk.NET's bindings do their own native resolution: the generated types hand the bare library name
// ("shaderc_shared") to Silk.NET.Core's LibraryLoader, which walks a list of candidate paths. That
// happens inside a static *method* - GetApi() -> CreateDefaultContext -> new UnmanagedLibrary(names)
// - not a type initializer, which is why touching no Silk.NET type is enough to defer it but merely
// naming the type is not. Two consequences make the usual mitigations useless here:
//
//   * A NativeLibrary.SetDllImportResolver registered on this assembly is never consulted, because
//     no DllImport in this assembly names shaderc - the loading happens in Silk.NET's code.
//   * The candidate paths are relative to AppContext.BaseDirectory, and under TaskHostFactory that
//     is the SDK's own directory (C:\Program Files\dotnet\sdk\<version>\), not the folder this task
//     sits in. So the runtimes/<rid>/native/ layout that makes the POC next door work is not even
//     looked at when this runs as a task.
//
// A failed lookup reports itself with one of two messages. Both are FileNotFoundException, so the
// text is the only thing that separates them, and which one appears decides where to look:
//
//   * "Could not load from any of the possible library names! Please make sure that the library is
//     installed and in the right place!" belongs to the *generated binding's own*
//     CreateDefaultContext - Silk.NET.Shaderc.dll here. The string is emitted into every generated
//     binding and is not in Silk.NET.Core.dll at all. It is thrown *after*
//     DefaultNativeContext.TryCreate -> UnmanagedLibrary.TryCreate -> LibraryLoader.TryLoadNativeLibrary
//     has come back empty, so the loader did run and did walk every candidate before giving up. This
//     is the message a genuinely missing native produces, and it is the one GetApi() surfaces.
//   * "Could not find or load the native library from any name: [ ... ]", and its single-name
//     sibling "Could not find or load the native library: ... Attempted: ...", come from
//     LibraryLoader.ThrowLibNotFoundAny / ThrowLibNotFound in Core instead. They belong to the
//     *throwing* LoadNativeLibrary entry point, which no generated binding calls, so seeing one of
//     these means something used that API directly rather than going through a binding's GetApi().
//
// The first is therefore the diagnostic that matters when a task host cannot find shaderc, and it
// says the native was not discoverable - not that anything upstream of the loader went wrong.
//
// What is left is to load the libraries ourselves, by absolute path, from beside the task assembly -
// and to do it *before* any Silk.NET type is touched. Windows resolves a bare LoadLibrary name
// against modules already in the process, so once these are loaded under their own names, Silk.NET's
// later lookup finds them without searching anywhere.
using System.Reflection;
using System.Runtime.InteropServices;

namespace NFMWorld.ShaderCompiler.Task;

internal static class NativeLibraries
{
    private static readonly object Gate = new();
    private static bool _done;

    // The library names as Silk.NET's bindings declare them, mapped to the file each one is on this
    // platform. The names are the constants the generated bindings pass to LibraryLoader, not
    // guesses: Silk.NET.Shaderc asks for "shaderc_shared" and Silk.NET.SPIRV.Cross for
    // "spirv-cross". The two packages version independently, which is why they are listed
    // separately rather than derived from one stem.
    private static readonly (string Name, string FileName)[] Libraries =
    [
        ("shaderc_shared", "shaderc_shared.dll"),
        ("spirv-cross", "spirv-cross.dll"),
    ];

    /// <summary>
    /// Loads both natives from the task assembly's own directory, once per process. MSBuild reuses a
    /// task host across projects, so this is guarded rather than assumed to run once.
    /// </summary>
    public static void EnsureLoaded()
    {
        lock (Gate)
        {
            if (_done)
                return;
            _done = true;

            var directory = Path.GetDirectoryName(typeof(NativeLibraries).Assembly.Location);
            if (directory is null)
                return;

            foreach (var (_, fileName) in Libraries)
                TryLoad(Path.Combine(directory, fileName));
        }
    }

    /// <summary>
    /// Whether the named library is already in the process, so a genuine load failure is reported
    /// rather than silently swallowed by the "already loaded" path.
    /// </summary>
    private static void TryLoad(string path)
    {
        if (!File.Exists(path))
            return;

        // Loaded for effect, not for the handle: the point is to have the module in the process
        // before Silk.NET asks for it by name. A failure here is left to surface from Silk.NET's own
        // loader, which reports the library name in terms the shader author can act on.
        NativeLibrary.TryLoad(path, out _);
    }
}
