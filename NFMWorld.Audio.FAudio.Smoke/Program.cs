using System.Diagnostics;
using System.Runtime.InteropServices;
using NFMWorld.Audio.FAudioBindings;

// Milestone 4 verification. Exercises the direct-FAudio path against the real native library,
// headlessly, and checks the whole chain: native library resolution -> FAudio device ->
// mastering voice -> source voice -> submitted buffer -> the mixer actually consuming samples.
//
// The failure this exists to catch is a silent one. FaudioEngine.Instance returns null when it
// cannot create a device - including when the native library cannot be found at all - and every
// caller treats null as "run silently", so a broken native lookup in the game would show up only
// as "the game has no sound", with nothing in the log.
//
// This is not a substitute for listening to the game: it proves audio is being mixed, not that it
// sounds right. Music playback, looping and tempo stretching still need a human ear.

var failures = new List<string>();

void Check(string what, bool ok, string detail = "")
{
    Console.WriteLine($"  {(ok ? "ok  " : "FAIL")}  {what}{(detail.Length > 0 ? $" - {detail}" : "")}");
    if (!ok) failures.Add(what);
}

static void ExplainFailure()
{
    try
    {
        var create = FAudio.FAudioCreate(out var audio, 0, FAudio.FAUDIO_DEFAULT_PROCESSOR);
        if (create != 0)
        {
            Console.WriteLine($"  FAudioCreate returned {create} - the library loaded but refused to initialise.");
            return;
        }

        FAudio.FAudio_GetDeviceCount(audio, out var count);
        Console.WriteLine($"  FAudioCreate succeeded and reports {count} playback device(s),");
        Console.WriteLine("  so the failure is in CreateMasteringVoice rather than in library resolution.");
        FAudio.FAudio_Release(audio);
    }
    catch (DllNotFoundException e)
    {
        Console.WriteLine($"  The FAudio native library could not be loaded: {e.Message}");
        Console.WriteLine($"  Looked for {Path.Combine(AppContext.BaseDirectory, "libs", "x64", "FAudio.dll")}");
    }
    catch (EntryPointNotFoundException e)
    {
        Console.WriteLine($"  The native library loaded but is missing an expected export: {e.Message}");
        Console.WriteLine("  The deployed FAudio may be older than the vendored bindings.");
    }
}

Console.WriteLine("== FAudio direct-binding smoke test ==");
Console.WriteLine();

var engine = FaudioEngine.Instance;
Check("FaudioEngine.Instance (native resolution + device + mastering voice)", engine is not null);

if (engine is null)
{
    ExplainFailure();
    Console.WriteLine();
    Console.WriteLine("FAILED - no audio device available, nothing further to test.");
    return 1;
}

using var engineLifetime = engine;

// A short sine rather than a real asset: this test is about the audio path, not the decoders.
const int sampleRate = 44100;
const int channels = 2;
const int frames = sampleRate / 2;
const short amplitude = short.MaxValue / 4;

var pcm = new byte[frames * channels * sizeof(short)];
for (var frame = 0; frame < frames; frame++)
{
    var sample = (short)(amplitude * Math.Sin(2 * Math.PI * 440 * frame / sampleRate));
    for (var channel = 0; channel < channels; channel++)
    {
        var at = (frame * channels + channel) * sizeof(short);
        pcm[at] = (byte)sample;
        pcm[at + 1] = (byte)(sample >> 8);
    }
}

var voice = engine.CreateSourceVoice(sampleRate, channels);
Check("CreateSourceVoice", voice != IntPtr.Zero);

if (voice == IntPtr.Zero)
{
    Console.WriteLine();
    Console.WriteLine("FAILED - the device exists but will not give out a source voice.");
    return 1;
}

try
{
    FAudio.FAudioVoice_SetVolume(voice, 0.5f, 0);
    FAudio.FAudioVoice_GetVolume(voice, out var volume);
    Check("SetVolume / GetVolume round-trip", Math.Abs(volume - 0.5f) < 0.001f, $"got {volume}");

    FAudio.FAudioSourceVoice_SetFrequencyRatio(voice, 1.5f, 0);
    FAudio.FAudioSourceVoice_GetFrequencyRatio(voice, out var ratio);
    Check("SetFrequencyRatio / GetFrequencyRatio round-trip", Math.Abs(ratio - 1.5f) < 0.001f, $"got {ratio}");

    // The pin mirrors what FaudioMusic/FaudioSoundClip do: FAudio reads this memory for as long
    // as the buffer is queued, so it must stay pinned until the voice is destroyed.
    var pin = GCHandle.Alloc(pcm, GCHandleType.Pinned);
    try
    {
        var buffer = new FAudio.FAudioBuffer
        {
            Flags = 0,
            AudioBytes = (uint)pcm.Length,
            pAudioData = pin.AddrOfPinnedObject(),
            PlayBegin = 0,
            PlayLength = 0,
            LoopBegin = 0,
            LoopLength = 0,
            LoopCount = FAudio.FAUDIO_LOOP_INFINITE,
            pContext = IntPtr.Zero,
        };

        Check("SubmitSourceBuffer", FAudio.FAudioSourceVoice_SubmitSourceBuffer(voice, ref buffer, IntPtr.Zero) == 0);

        FAudio.FAudioSourceVoice_Start(voice, 0, 0);
        Thread.Sleep(250);

        FAudio.FAudioSourceVoice_GetState(voice, out var playing, 0);
        Check(
            "the mixer is consuming samples",
            playing.SamplesPlayed > 0,
            $"{playing.SamplesPlayed} samples played, {playing.BuffersQueued} buffer(s) queued"
        );

        // This is exactly what IRadicalMusic.SetPaused relies on: Stop and Start on a voice whose
        // buffer is still queued must pause and resume it in place, without resubmitting.
        FAudio.FAudioSourceVoice_Stop(voice, 0, 0);
        Thread.Sleep(150);
        FAudio.FAudioSourceVoice_GetState(voice, out var stopped, 0);
        Thread.Sleep(150);
        FAudio.FAudioSourceVoice_GetState(voice, out var stillStopped, 0);
        Check(
            "a stopped voice stops consuming",
            stillStopped.SamplesPlayed == stopped.SamplesPlayed,
            $"{stopped.SamplesPlayed} -> {stillStopped.SamplesPlayed}"
        );

        FAudio.FAudioSourceVoice_Start(voice, 0, 0);
        Thread.Sleep(200);
        FAudio.FAudioSourceVoice_GetState(voice, out var resumed, 0);
        Check(
            "a restarted voice consumes again",
            resumed.SamplesPlayed > stillStopped.SamplesPlayed,
            $"{stillStopped.SamplesPlayed} -> {resumed.SamplesPlayed}"
        );

        FAudio.FAudioSourceVoice_Stop(voice, 0, 0);
    }
    finally
    {
        engine.DestroyVoice(voice);
        voice = IntPtr.Zero;
        pin.Free();
    }
}
finally
{
    engine.DestroyVoice(voice);
}

bool QueueFadingTone(float initialVolume, Action<long> afterDestroy)
{
    var fadingVoice = engine.CreateSourceVoice(sampleRate, channels);
    if (fadingVoice == IntPtr.Zero)
        return false;

    var fadingPin = GCHandle.Alloc(pcm, GCHandleType.Pinned);
    try
    {
        var buffer = new FAudio.FAudioBuffer
        {
            AudioBytes = (uint)pcm.Length,
            pAudioData = fadingPin.AddrOfPinnedObject(),
            LoopCount = FAudio.FAUDIO_LOOP_INFINITE,
        };
        if (FAudio.FAudioSourceVoice_SubmitSourceBuffer(fadingVoice, ref buffer, IntPtr.Zero) != 0
            || FAudio.FAudioVoice_SetVolume(fadingVoice, initialVolume, 0) != 0
            || FAudio.FAudioSourceVoice_Start(fadingVoice, 0, 0) != 0)
            return false;

        var submittedAt = Stopwatch.GetTimestamp();
        engine.FadeOutAndDestroy(fadingVoice, () =>
            {
                fadingPin.Free();
                afterDestroy(submittedAt);
            }
        );
        fadingVoice = IntPtr.Zero;
        return true;
    }
    finally
    {
        if (fadingVoice != IntPtr.Zero)
        {
            engine.DestroyVoice(fadingVoice);
            fadingPin.Free();
        }
    }
}

var submissionThread = Environment.CurrentManagedThreadId;
var fadeCallbacks = 0;
var fadeThread = submissionThread;
var fadeElapsed = TimeSpan.Zero;
using var fadeCompleted = new ManualResetEventSlim();
var fadeQueued = QueueFadingTone(0.5f, submittedAt =>
    {
        fadeElapsed = Stopwatch.GetElapsedTime(submittedAt);
        fadeThread = Environment.CurrentManagedThreadId;
        Interlocked.Increment(ref fadeCallbacks);
        fadeCompleted.Set();
    }
);
Check("queue a playing voice for a fade", fadeQueued);
var fadeFinished = fadeQueued && fadeCompleted.Wait(TimeSpan.FromSeconds(5));
Check("fade callback completes within a generous timeout", fadeFinished);
if (fadeFinished)
{
    Check("fade callback fires once", Volatile.Read(ref fadeCallbacks) == 1);
    Check(
        "positive-volume fade spans at least 100 ms from submission",
        fadeElapsed.TotalMilliseconds >= FaudioEngine.FadeMilliseconds,
        $"{fadeElapsed.TotalMilliseconds:F1} ms"
    );
    Check("native fade destruction runs off the submitting thread", fadeThread != submissionThread);
}

var mutedCallbacks = 0;
var mutedThread = submissionThread;
using var mutedCompleted = new ManualResetEventSlim();
var mutedQueued = QueueFadingTone(0f, _ =>
    {
        mutedThread = Environment.CurrentManagedThreadId;
        Interlocked.Increment(ref mutedCallbacks);
        mutedCompleted.Set();
    }
);
Check("queue an already-muted voice", mutedQueued);
var mutedFinished = mutedQueued && mutedCompleted.Wait(TimeSpan.FromSeconds(5));
Check("muted voice cleanup completes asynchronously", mutedFinished && mutedThread != submissionThread);

var emptyCallbacks = 0;
engine.FadeOutAndDestroy(IntPtr.Zero, () => emptyCallbacks++);
Check("an empty voice completes once without native work", emptyCallbacks == 1);

var pendingCallbacks = new int[4];
var allPendingAtShutdown = false;
var drainedBeforeDisposeReturned = false;
var shutdownQueuedAll = false;
Exception? shutdownFailure = null;
using var shutdownCompleted = new ManualResetEventSlim();
var shutdownQueued = QueueFadingTone(0.25f, shutdownSubmittedAt =>
    {
        try
        {
            shutdownQueuedAll = true;
            for (var i = 0; i < pendingCallbacks.Length; i++)
            {
                var index = i;
                shutdownQueuedAll &= QueueFadingTone(i % 2 == 0 ? 0.25f : 0f, _ =>
                Interlocked.Increment(ref pendingCallbacks[index]));
            }

            // Enqueuing from this callback keeps every child pending until the reentrant dispose.
            allPendingAtShutdown = pendingCallbacks.All(count => count == 0);
            engine.Dispose();
            drainedBeforeDisposeReturned = pendingCallbacks.All(count => count == 1);
            engine.Dispose();
        }
        catch (Exception ex)
        {
            shutdownFailure = ex;
        }
        finally
        {
            shutdownCompleted.Set();
        }
    }
);
Check("queue callback-driven engine shutdown", shutdownQueued);
var shutdownFinished = shutdownQueued && shutdownCompleted.Wait(TimeSpan.FromSeconds(5));
Check(
    "callback-driven shutdown completes without deadlock",
    shutdownFinished && shutdownFailure is null,
    shutdownFailure?.ToString() ?? ""
);
engine.Dispose();
Check("shutdown queued all native voices", shutdownQueuedAll);
Check("shutdown starts with pending fades", allPendingAtShutdown);
Check("shutdown drains pending pins and callbacks before returning", drainedBeforeDisposeReturned);
Check("repeated disposal does not repeat callbacks", pendingCallbacks.All(count => count == 1));
Check(
    "completed fade callbacks are not repeated at shutdown",
    (!fadeQueued || Volatile.Read(ref fadeCallbacks) == 1)
    && (!mutedQueued || Volatile.Read(ref mutedCallbacks) == 1)
);
Check("the disposed engine is no longer available", FaudioEngine.Instance is null);

Console.WriteLine();
if (failures.Count == 0)
{
    Console.WriteLine("PASSED - the FAudio path is live end to end.");
    return 0;
}

Console.WriteLine($"FAILED - {failures.Count} check(s): {string.Join("; ", failures)}");
return 1;
