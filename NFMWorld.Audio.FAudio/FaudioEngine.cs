using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorldLibrary;

namespace NFMWorld.Audio.FAudioBindings;

/// <summary>
/// Owns the single FAudio device and its mastering voice for the process. Every
/// <see cref="IRadicalMusic"/>/<see cref="ISoundClip"/> implementation creates source voices
/// against this engine rather than a device of its own.
/// </summary>
/// <remarks>
/// This is the direct-FAudio replacement for what FNA's own <c>FAudioContext</c>
/// (FNA/src/Audio/SoundEffect.cs) did internally for <c>SoundEffect</c>. The two are
/// independent: FNA's context only exists while FNA's audio path is still in use, so this
/// engine can safely own a second FAudio device alongside it during the migration.
/// </remarks>
public sealed class FaudioEngine : IDisposable
{
    private static FaudioEngine? _instance;
    private static bool _attempted;
    private static readonly Lock Gate = new();

    private IntPtr _audio;
    private IntPtr _masteringVoice;
    private bool _disposed;

    /// <summary>
    /// The process-wide engine, created on first use. Returns null if FAudio is unavailable
    /// (missing native library, or no audio device) - callers treat that as "run silently"
    /// rather than failing to start the game.
    /// </summary>
    /// <remarks>
    /// Unlike the underlying device, a failure here is remembered rather than retried: the
    /// callers are sound triggers that can fire every frame, and re-attempting (and re-throwing
    /// the <see cref="DllNotFoundException"/>) once per sound would be far more expensive than
    /// running without audio.
    /// </remarks>
    public static FaudioEngine? Instance
    {
        get
        {
            lock (Gate)
            {
                if (_instance is { _disposed: false })
                    return _instance;

                if (_attempted)
                    return null;

                _attempted = true;
                _instance = TryCreate();
                return _instance;
            }
        }
    }

    private FaudioEngine(IntPtr audio, IntPtr masteringVoice)
    {
        _audio = audio;
        _masteringVoice = masteringVoice;
    }

    private static FaudioEngine? TryCreate()
    {
        // Every failure below is reported once and then the game runs silently. That is the right
        // behaviour for a game, but it also means a broken native deployment would otherwise be
        // invisible, so each reason is logged rather than discarded.
        IntPtr audio;
        try
        {
            // Flags: 0 means FAUDIO_COMMIT_NOW - operation sets take effect immediately, so
            // callers never need an explicit FAudio_CommitOperationSet. Matches what FNA passes.
            if (FAudio.FAudioCreate(out audio, 0, FAudio.FAUDIO_DEFAULT_PROCESSOR) != 0)
            {
                Logging.Warning("FAudio refused to initialise - the game will run without sound.");
                return null;
            }
        }
        catch (DllNotFoundException e)
        {
            Logging.Warning($"The FAudio native library could not be loaded - the game will run without sound. {e.Message}");
            return null;
        }
        catch (EntryPointNotFoundException e)
        {
            Logging.Warning($"The FAudio native library is missing an expected export - the game will run without sound. {e.Message}");
            return null;
        }

        FAudio.FAudio_GetDeviceCount(audio, out var devices);
        if (devices == 0)
        {
            Logging.Warning("No audio playback device is available - the game will run without sound.");
            FAudio.FAudio_Release(audio);
            return null;
        }

        if (FAudio.FAudio_CreateMasteringVoice(
                audio,
                out var masteringVoice,
                FAudio.FAUDIO_DEFAULT_CHANNELS,
                FAudio.FAUDIO_DEFAULT_SAMPLERATE,
                0,
                0,
                IntPtr.Zero) != 0)
        {
            Logging.Warning("Could not create an FAudio mastering voice - the game will run without sound.");
            FAudio.FAudio_Release(audio);
            return null;
        }

        FAudio.FAudio_StartEngine(audio);
        return new FaudioEngine(audio, masteringVoice);
    }

    /// <summary>
    /// Creates a source voice feeding the mastering voice, for 16-bit PCM at the given rate.
    /// Returns <see cref="IntPtr.Zero"/> if the voice could not be created.
    /// </summary>
    /// <param name="sampleRate">Sample rate of the PCM data that will be submitted.</param>
    /// <param name="channels">Channel count of the PCM data that will be submitted.</param>
    /// <param name="maxFrequencyRatio">
    /// Upper bound on the playback rate multiplier - <see cref="IRadicalMusic.SetFreqMultiplier"/>
    /// clamps to 2.0, so that is the default ceiling.
    /// </param>
    public IntPtr CreateSourceVoice(int sampleRate, int channels, float maxFrequencyRatio = 2.0f)
    {
        if (_disposed || _audio == IntPtr.Zero)
            return IntPtr.Zero;

        // WAVE_FORMAT_PCM. FAudio.cs carries no named constant for it (FNA hardcodes it in
        // DynamicSoundEffectInstance too), so it lives here rather than in the vendored file.
        const ushort waveFormatPcm = 1;

        var format = new FAudio.FAudioWaveFormatEx
        {
            wFormatTag = waveFormatPcm,
            nChannels = (ushort)channels,
            nSamplesPerSec = (uint)sampleRate,
            wBitsPerSample = 16,
            nBlockAlign = (ushort)(channels * sizeof(short)),
            nAvgBytesPerSec = (uint)(sampleRate * channels * sizeof(short)),
            cbSize = 0,
        };

        return FAudio.FAudio_CreateSourceVoice(
            _audio,
            out var voice,
            ref format,
            Flags: 0,
            MaxFrequencyRatio: maxFrequencyRatio,
            pCallback: IntPtr.Zero,
            pSendList: IntPtr.Zero,
            pEffectChain: IntPtr.Zero) != 0
            ? IntPtr.Zero
            : voice;
    }

    /// <summary>Destroys a voice created by <see cref="CreateSourceVoice"/>.</summary>
    public void DestroyVoice(IntPtr voice)
    {
        if (voice != IntPtr.Zero)
            FAudio.FAudioVoice_DestroyVoice(voice);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_masteringVoice != IntPtr.Zero)
        {
            FAudio.FAudioVoice_DestroyVoice(_masteringVoice);
            _masteringVoice = IntPtr.Zero;
        }

        if (_audio != IntPtr.Zero)
        {
            FAudio.FAudio_StopEngine(_audio);
            FAudio.FAudio_Release(_audio);
            _audio = IntPtr.Zero;
        }
    }
}

/// <summary>
/// Registers the FAudio native-library name mapping (bare "FAudio" -> the platform's actual
/// file name) for this assembly. <c>NativeLibrary.SetDllImportResolver</c> is per-assembly, and
/// the game's own resolver in <c>WorldGame.Main</c> only covers the assemblies it registers, so
/// this has to be self-contained - otherwise these DllImports fail to resolve when this assembly
/// is loaded without the game (e.g. from a test).
/// </summary>
internal static class FaudioNativeResolver
{
    [ModuleInitializer]
    internal static void Register()
    {
        NativeLibrary.SetDllImportResolver(typeof(FAudio).Assembly, Resolve);
    }

    private static IntPtr Resolve(string libraryName, System.Reflection.Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != "FAudio")
            return IntPtr.Zero; // let the default resolution handle anything else

        var mapped = NativeFileName();
        if (mapped is null)
            return IntPtr.Zero;

        // Both the game and this library deploy their native binaries under libs/<arch>/ relative
        // to the app base directory (NFMWorld.NativeLibs copies them there; WorldGame's own
        // resolver anchors to the same place). A bare file name does not search that directory,
        // so look there first.
        if (ArchitectureDirectory() is { } arch)
        {
            var deployed = Path.Combine(AppContext.BaseDirectory, "libs", arch, mapped);
            if (File.Exists(deployed))
            {
                try
                {
                    return NativeLibrary.Load(deployed);
                }
                catch (DllNotFoundException)
                {
                    // fall through to default probing
                }
            }
        }

        // Fall back to default probing, which finds the library when it sits next to the
        // executable (as in the smoke test) or on the system library path.
        return NativeLibrary.TryLoad(mapped, assembly, searchPath, out var handle) ? handle : IntPtr.Zero;
    }

    /// <summary>
    /// The <c>libs/</c> subdirectory this platform's native binaries are deployed into.
    /// Mirrors the mapping in <c>WorldGame.ImportResolver</c>; worth consolidating into one
    /// shared helper once the platform and graphics backends need it too.
    /// </summary>
    private static string? ArchitectureDirectory()
    {
        var cpu = RuntimeInformation.ProcessArchitecture;

        if (OperatingSystem.IsWindows())
        {
            return cpu switch
            {
                Architecture.X64 => "x64",
                Architecture.X86 => "x86",
                Architecture.Arm64 => "arm64",
                _ => null,
            };
        }

        if (OperatingSystem.IsMacOS())
            return "osx";

        if (OperatingSystem.IsLinux() || OperatingSystem.IsFreeBSD())
        {
            return cpu switch
            {
                Architecture.X64 => "lib64",
                Architecture.X86 => "lib32",
                Architecture.Arm64 => "libaarch64",
                Architecture.Arm => "libarmhf",
                _ => null,
            };
        }

        return null;
    }

    private static string? NativeFileName()
    {
        if (OperatingSystem.IsWindows())
            return "FAudio.dll";
        if (OperatingSystem.IsMacOS())
            return "libFAudio.0.dylib";
        if (OperatingSystem.IsLinux() || OperatingSystem.IsFreeBSD())
            return "libFAudio.so.0";

        return null;
    }
}
