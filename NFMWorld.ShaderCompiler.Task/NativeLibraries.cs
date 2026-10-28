// Adds the task assembly directory to Silk.NET's native-library search paths. Under TaskHostFactory,
// AppContext.BaseDirectory points to the SDK rather than this assembly, so its native assets are not
// found by Silk.NET's default resolver.
using Silk.NET.Core.Loader;

namespace NFMWorld.ShaderCompiler.Task;

internal static class NativeLibraries
{
    private static readonly object Gate = new();
    private static bool _done;

    private static readonly (string Name, string FileName)[] Libraries = GetLibraries();

    /// <summary>
    /// Adds the task assembly's native libraries to Silk.NET's resolver once per process.
    /// </summary>
    public static void EnsureLoaded()
    {
        lock (Gate)
        {
            if (_done)
                return;

            var directory = Path.GetDirectoryName(typeof(NativeLibraries).Assembly.Location);
            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException("The shader compiler task assembly has no location.");

            if (PathResolver.Default is not DefaultPathResolver resolver)
                throw new InvalidOperationException("Silk.NET's default native library resolver is not configurable.");

            resolver.Resolvers.Insert(0, name =>
                Libraries
                    .Where(library => library.Name == name)
                    .Select(library => Path.Combine(directory, library.FileName)));
            _done = true;
        }
    }

    private static (string Name, string FileName)[] GetLibraries()
    {
        var extension = OperatingSystem.IsWindows()
            ? ".dll"
            : OperatingSystem.IsMacOS() ? ".dylib" : OperatingSystem.IsLinux() ? ".so" : null;
        if (extension is null)
            throw new PlatformNotSupportedException("The shader compiler native libraries are not available on this platform.");

        var prefix = extension == ".dll" ? "" : "lib";
        var shaderc = $"{prefix}shaderc_shared{extension}";
        var spirvCross = $"{prefix}spirv-cross{extension}";
        // Silk.NET requests the platform-specific filenames, not just the library stems.
        return
        [
            (shaderc, shaderc),
            (spirvCross, spirvCross),
        ];
    }
}
