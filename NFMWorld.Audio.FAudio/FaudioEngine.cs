using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Numerics;
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
    private byte[]? _spatialHandle;
    private float[] _spatialMatrix = [];
    private bool _spatialUnavailable;

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
            Logging.Warning(
                $"The FAudio native library is missing an expected export - the game will run without sound. {e.Message}"
            );
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
                IntPtr.Zero
            )
            != 0)
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
                   pEffectChain: IntPtr.Zero
               )
               != 0
            ? IntPtr.Zero
            : voice;
    }

    /// <summary>
    /// Destroys a voice created by <see cref="CreateSourceVoice"/> without a fade.
    /// </summary>
    public void DestroyVoice(IntPtr voice)
    {
        if (voice != IntPtr.Zero)
            FAudio.FAudioVoice_DestroyVoice(voice);
    }

    public unsafe void ApplySpatial(
        IntPtr voice,
        Vector3 listenerPosition,
        Vector3 listenerForward,
        Vector3 listenerUp,
        Vector3 emitterPosition
    )
    {
        if (_disposed || voice == IntPtr.Zero || _spatialUnavailable) return;
        try
        {
            if (_spatialHandle is null)
            {
                FAudio.FAudioVoice_GetVoiceDetails(_masteringVoice, out var details);
                if (FAudio.FAudio_GetDeviceDetails(_audio, 0, out var device) != 0
                    || details.InputChannels == 0
                    || BitOperations.PopCount(device.OutputFormat.dwChannelMask) != details.InputChannels)
                    throw new InvalidOperationException("Could not query the audio speaker layout.");
                _spatialMatrix = new float[details.InputChannels];
                var handle = new byte[FAudio.F3DAUDIO_HANDLE_BYTESIZE];
                FAudio.F3DAudioInitialize(device.OutputFormat.dwChannelMask, 34300, handle);
                _spatialHandle = handle;
            }

            var listener = new FAudio.F3DAUDIO_LISTENER
            {
                Position = ToAudioVector(listenerPosition),
                OrientFront = ToAudioVector(listenerForward),
                OrientTop = ToAudioVector(listenerUp)
            };
            FAudio.F3DAUDIO_DISTANCE_CURVE_POINT* points = stackalloc FAudio.F3DAUDIO_DISTANCE_CURVE_POINT[2];
            // Distance gain is shared with Web Audio rather than applied twice.
            points[0] = new() { Distance = 0, DSPSetting = 1 };
            points[1] = new() { Distance = 1, DSPSetting = 1 };
            var curve = new FAudio.F3DAUDIO_DISTANCE_CURVE { pPoints = (IntPtr)points, PointCount = 2 };
            var emitter = new FAudio.F3DAUDIO_EMITTER
            {
                Position = ToAudioVector(emitterPosition),
                OrientFront = new() { z = 1 },
                OrientTop = new() { y = 1 },
                ChannelCount = 1,
                InnerRadius = 300,
                CurveDistanceScaler = SoundSpatialization.ReferenceDistance,
                pVolumeCurve = (IntPtr)(&curve)
            };
            fixed (float* matrix = _spatialMatrix)
            {
                var dsp = new FAudio.F3DAUDIO_DSP_SETTINGS
                {
                    pMatrixCoefficients = (IntPtr)matrix,
                    SrcChannelCount = 1,
                    DstChannelCount = (uint)_spatialMatrix.Length
                };
                FAudio.F3DAudioCalculate(_spatialHandle, ref listener, ref emitter, FAudio.F3DAUDIO_CALCULATE_MATRIX, ref dsp);
                if (FAudio.FAudioVoice_SetOutputMatrix(voice, _masteringVoice, 1, dsp.DstChannelCount, (IntPtr)matrix, 0) != 0)
                    throw new InvalidOperationException("Could not apply the 3D speaker matrix.");
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or InvalidOperationException)
        {
            _spatialUnavailable = true;
            Logging.Error($"3D speaker mixing is unavailable; retaining distance attenuation. {ex.Message}");
        }
    }

    // Reflect the game's downward Y axis into F3DAudio's left-handed coordinates.
    private static FAudio.F3DAUDIO_VECTOR ToAudioVector(Vector3 vector) => new() { x = vector.X, y = -vector.Y, z = vector.Z };

    /// <summary>
    /// Fades a voice out from its current volume and then destroys it, so a sound stopped
    /// mid-sample does not click. The ramp runs on the thread pool: <paramref name="afterDestroy"/>
    /// fires once the voice is gone, and that is where the caller releases the PCM it had pinned.
    /// </summary>
    /// <remarks>
    /// The fade has to span several of FAudio's ~20 ms mix quanta, so it cannot run inline in a
    /// frame - and a real sleep is no use for a ramp at all (it costs ~15 ms here), hence the spin.
    /// </remarks>
    public void FadeOutAndDestroy(IntPtr voice, Action? afterDestroy = null)
    {
        if (voice == IntPtr.Zero)
        {
            afterDestroy?.Invoke();
            return;
        }

        FAudio.FAudioVoice_GetVolume(voice, out var volume);
        if (volume <= 0f)
        {
            DestroyVoice(voice);
            afterDestroy?.Invoke();
            return;
        }

        Task.Run(() =>
            {
                var clock = Stopwatch.StartNew();
                double remaining;
                while ((remaining = 1.0 - clock.Elapsed.TotalMilliseconds / FadeMilliseconds) > 0.0)
                {
                    FAudio.FAudioVoice_SetVolume(voice, volume * (float)remaining, 0);
                    Thread.Yield();
                }

                DestroyVoice(voice);
                afterDestroy?.Invoke();
            }
        );
    }

    /// <summary>
    /// Length of the fade in <see cref="FadeOutAndDestroy"/>. Measured against the device output:
    /// an abrupt destroy steps the waveform 11x its natural slope (the click), 50 ms still leaves 3x,
    /// and from 100 ms the step is indistinguishable from the tone itself.
    /// </summary>
    public const int FadeMilliseconds = 100;

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
