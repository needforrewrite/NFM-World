using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
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
            $"{playing.SamplesPlayed} samples played, {playing.BuffersQueued} buffer(s) queued");

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
            $"{stopped.SamplesPlayed} -> {stillStopped.SamplesPlayed}");

        FAudio.FAudioSourceVoice_Start(voice, 0, 0);
        Thread.Sleep(200);
        FAudio.FAudioSourceVoice_GetState(voice, out var resumed, 0);
        Check(
            "a restarted voice consumes again",
            resumed.SamplesPlayed > stillStopped.SamplesPlayed,
            $"{stillStopped.SamplesPlayed} -> {resumed.SamplesPlayed}");

        FAudio.FAudioSourceVoice_Stop(voice, 0, 0);
    }
    finally
    {
        pin.Free();
    }
}
finally
{
    engine.DestroyVoice(voice);
}

Console.WriteLine();
if (failures.Count == 0)
{
    Console.WriteLine("PASSED - the FAudio path is live end to end.");
    return 0;
}

Console.WriteLine($"FAILED - {failures.Count} check(s): {string.Join("; ", failures)}");
return 1;
