using System.Reflection;
using System.Runtime.InteropServices;

namespace NFMWorld.Graphics.Sokol;

/// <summary>
/// Which sokol_gfx backend to load, chosen at launch rather than at build time.
///
/// The choice has to be a runtime one only because a <em>build</em> cannot make it: sokol selects
/// its backend with a compile-time symbol (<c>_SOKOL_ANY_GL</c> and friends, <c>sokol_gfx.h:6003</c>),
/// one per translation unit (<c>SharpSokol/native/sokol_unity.c:9-14</c>), and
/// <c>sg_query_backend</c> then returns that build's fixed assignment (<c>:26529</c>). So each
/// backend is a separate DLL - the same per-backend matrix the SharpSokol workflow builds
/// (<c>.github/workflows/build-native.yml</c>) - and "selecting a backend" means loading the right
/// file. <see cref="NativeDirectory"/> and <see cref="InstallResolver"/> are that entire mechanism.
///
/// This exists for a measurement, not as a shipping option: the point is to run one scene through
/// several backends and compare their per-draw cost.
/// </summary>
public enum SokolBackend
{
    /// <summary>D3D11. The only backend this repository vendored before the others; the default.</summary>
    D3d11,

    /// <summary>
    /// Desktop OpenGL 4.x through sokol's GLCORE backend.
    ///
    /// Windows-only for now, and not for a lack of interest: <c>_SOKOL_USE_WIN32_GL_LOADER</c> is
    /// defined only under <c>SOKOL_GLCORE</c> (<c>sokol_gfx.h:6009</c>), so desktop GL is the one GL
    /// flavour sokol can bring up on this platform on its own. GLES3 needs an EGL loader fed to
    /// sokol by hand.
    /// </summary>
    Glcore,
}

/// <summary>
/// The launch-argument parsing and native-library resolution behind <see cref="SokolBackend"/>.
///
/// Kept out of <c>WorldGame</c> only so that the pieces that are pure mechanism - a flag, a
/// directory name, an assembly resolver - live next to the enum they are about, and so the game's
/// <c>Main</c> reads as the three calls it actually makes.
///
/// The resolver half is not optional. <c>NativeLibrary.SetDllImportResolver</c> is scoped to the
/// assembly that <em>declares</em> the <c>[DllImport]</c>, not the one that calls it, and every
/// <c>[DllImport("sokol")]</c> - 453 of them - lives in SharpSokol. Today <c>sokol.dll</c> resolves
/// only because it happens to sit next to the executable; a resolver registered anywhere else would
/// never be consulted. So one is installed on SharpSokol's own assembly here, which is also what
/// keeps the choice of backend invisible to that submodule.
/// </summary>
public static class SokolBackendSelection
{
    /// <summary>
    /// The launch argument that selects a backend. The syntax is the <c>--flag=value</c> form the
    /// rest of the application's arguments use.
    /// </summary>
    public const string ArgumentPrefix = "--sokol-backend=";

    /// <summary>
    /// The backend named by <paramref name="args"/>, or <see cref="SokolBackend.D3d11"/> when the
    /// argument is absent.
    ///
    /// An unrecognised value throws rather than falling back to the default. The default is a
    /// perfectly good backend, so a typo would otherwise produce a run that looks entirely normal
    /// and is measuring the wrong thing - which is the one failure this whole exercise cannot
    /// afford, since the result of a run is a benchmark number nobody can inspect afterwards.
    /// </summary>
    /// <exception cref="ArgumentException">On an unknown backend name.</exception>
    public static SokolBackend Parse(string[] args)
    {
        foreach (var arg in args)
        {
            if (!arg.StartsWith(ArgumentPrefix, StringComparison.OrdinalIgnoreCase)) continue;

            var value = arg[ArgumentPrefix.Length..];
            if (Enum.TryParse<SokolBackend>(value, ignoreCase: true, out var backend))
                return backend;

            throw new ArgumentException(
                $"Unknown {ArgumentPrefix}{value}. Valid backends: " +
                $"{string.Join(", ", Enum.GetNames<SokolBackend>().Select(n => n.ToLowerInvariant()))}.");
        }

        return SokolBackend.D3d11;
    }

    /// <summary>
    /// The <c>AppContext.BaseDirectory</c>-relative directory holding <paramref name="backend"/>'s
    /// DLL, matching the deployment layout in this project's csproj.
    ///
    /// One directory per backend rather than one file name per backend, because the file inside is
    /// always called <c>sokol.dll</c>: every SharpSokol <c>[DllImport]</c> names the plain library
    /// <c>"sokol"</c>, so the name at the output root is not ours to vary without editing the
    /// submodule. The directory is ours, so it is what carries the distinction.
    /// </summary>
    public static string NativeDirectory(SokolBackend backend) => backend switch
    {
        SokolBackend.D3d11 => "d3d11",
        SokolBackend.Glcore => "glcore",
        _ => throw new ArgumentOutOfRangeException(nameof(backend), backend,
            $"No native directory is defined for this backend, please update {nameof(NativeDirectory)}"),
    };

    /// <summary>
    /// Installs the resolver that maps SharpSokol's <c>"sokol"</c> imports onto
    /// <paramref name="backend"/>'s DLL.
    ///
    /// Called once, before anything touches sokol. A resolver can only be set once per assembly, so
    /// calling this twice for the same assembly throws <see cref="InvalidOperationException"/> -
    /// which is the right outcome here, since two different backends in one process is not a state
    /// sokol supports anyway (<c>sg_setup</c> is process-global).
    /// </summary>
    public static void InstallResolver(SokolBackend backend)
    {
        // The assembly whose [DllImport("sokol")] declarations this must answer for. A type from
        // SharpSokol is named rather than its assembly name as a string so a rename cannot leave this
        // quietly resolving nothing.
        var declaring = typeof(SharpSokol.Native.Gfx).Assembly;
        NativeLibrary.SetDllImportResolver(declaring, (libraryName, assembly, searchPath) =>
        {
            if (libraryName != NativeLibraryName) return IntPtr.Zero; // not ours: let default probing run

            var path = Path.Combine(AppContext.BaseDirectory, "sokol", NativeDirectory(backend), $"{NativeLibraryName}.dll");

            // Fall back rather than throw. The default probing this defers to still finds the
            // library when it sits beside the executable, which is how the smoke tests load it -
            // they run against the output root, not through a backend subdirectory.
            return File.Exists(path)
                ? NativeLibrary.Load(path)
                : NativeLibrary.TryLoad(NativeLibraryName, assembly, searchPath, out var handle) ? handle : IntPtr.Zero;
        });
    }

    /// <summary>
    /// The library name every SharpSokol P/Invoke declares. Not a variable anywhere in this
    /// repository - it is a fixed property of that submodule's bindings - so it is asserted rather
    /// than passed in.
    /// </summary>
    private const string NativeLibraryName = "sokol";

    /// <summary>
    /// Whether <paramref name="backend"/> can even be attempted on this platform, and why not when it
    /// cannot.
    ///
    /// Cheap and worth having: the alternative is a <c>DllNotFoundException</c> naming a path that
    /// does not exist, several frames of setup later, which reads as a deployment mistake rather
    /// than as an unsupported combination.
    /// </summary>
    public static bool IsSupportedOnThisPlatform(SokolBackend backend, out string? reason)
    {
        reason = null;

        if (!OperatingSystem.IsWindows())
        {
            reason = $"{backend} is only vendored for Windows (see native/ in NFMWorld.Graphics.Sokol).";
            return false;
        }

        if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            reason = $"{backend} is only vendored for win-x64, and this process is {RuntimeInformation.ProcessArchitecture}.";
            return false;
        }

        return true;
    }
}
