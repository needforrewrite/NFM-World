using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;
using LibOpenMPT.NET;
using Maxine.Extensions.Collections;
using Microsoft.IO;
using Microsoft.Xna.Framework.Audio;
using NFMWorldLibrary;

namespace NFMWorld.Audio;

/// <summary>
/// Decodes tracker/module formats (MOD, XM, IT, S3M, and more) to 16-bit PCM
/// using libopenmpt via LibOpenMPT.NET.
/// </summary>
public static unsafe class TrackerDecoder
{
    /// <summary>
    /// Default sample rate for tracker rendering. 44100 Hz is the standard
    /// for module playback quality.
    /// </summary>
    private const int DefaultSampleRate = 44100;

    /// <summary>
    /// Default repeat count: -1 means loop forever (we render the full song once
    /// and let our own SoundEffectInstance handle looping).
    /// </summary>
    private const int RenderRepeatCount = 0; // play once, no internal loop

    /// <summary>
    /// Decode a tracker module from raw file data. Returns 16-bit stereo PCM
    /// rendered at 44100 Hz.
    /// </summary>
    public static DecodeResult Decode(Stream stream)
    {
        using var memoryStream = new RecyclableMemoryStream(MemoryManager.Manager, Guid.NewGuid(), "TrackerDecoder stream");
        stream.CopyTo(memoryStream);
        using var arr = SafeArrayPool<byte>.Shared.Rent((int)memoryStream.Length);
        memoryStream.GetReadOnlySequence().CopyTo(arr);
        return Decode(arr);
    }

    /// <summary>
    /// Decode a tracker module from raw file data. Returns 16-bit stereo PCM
    /// rendered at 44100 Hz.
    /// </summary>
    public static DecodeResult Decode(ReadOnlySpan<byte> fileData)
    {
        Module* mod = null;

        try
        {
            fixed (byte* pData = fileData)
            {
                var error = 0;

                mod = NativeMethods.module_create_from_memory2(
                    pData,
                    (nuint)fileData.Length,
                    null, null,  // no log callback
                    null, null,   // no error callback
                    &error, // error code
                    null, // no error message
                    null // no ctls
                );

                if (mod == null)
                {
                    var errorMessage = NativeMethods.error_string(error);
                    string errorMessageStr;
                    try
                    {
                        errorMessageStr = Encoding.UTF8.GetString((byte*)errorMessage).TrimEnd('\0');
                    }
                    finally
                    {
                        NativeMethods.free_string(errorMessage);
                    }

                    throw new InvalidOperationException($"Failed to load tracker module: {errorMessageStr}");
                }
            }

            // Set repeat count: play once (we handle looping via SoundEffectInstance)
            _ = NativeMethods.module_set_repeat_count(mod, RenderRepeatCount);

            // Get total duration to allocate buffer
            var durationSeconds = NativeMethods.module_get_duration_seconds(mod);
            var totalFrames = (int)(durationSeconds * DefaultSampleRate) + DefaultSampleRate; // +1s buffer
            var totalFloatSamples = totalFrames * 2; // stereo interleaved

            // Render to float buffer first (higher quality internal processing)
            using var floatBuffer = SafeArrayPool<float>.Shared.Rent(totalFloatSamples);

            nuint totalFramesRead = 0;
            const int chunkFrames = 4096;

            fixed (float* pOut = floatBuffer)
            {
                while (totalFramesRead < (nuint)totalFrames)
                {
                    var remaining = (nuint)(totalFrames - (int)totalFramesRead);
                    var toRead = remaining < chunkFrames ? remaining : chunkFrames;

                    var framesRead = NativeMethods.module_read_interleaved_float_stereo(
                        mod,
                        DefaultSampleRate,
                        toRead,
                        pOut + (int)totalFramesRead * 2 // advance by frames*2 (stereo)
                    );

                    if (framesRead == 0)
                        break; // end of module

                    totalFramesRead += framesRead;
                }
            }

            // Trim buffer to actual rendered length
            var actualFloatSamples = (int)totalFramesRead * 2;
            var actualFloats = floatBuffer.AsSpan(0, actualFloatSamples);

            // Convert float [-1.0, 1.0] to 16-bit PCM
            var pcmData = SafeArrayPool<byte>.Shared.Rent(actualFloatSamples * 2);
            try
            {
                for (int i = 0; i < actualFloatSamples; i++)
                {
                    var sample = Math.Clamp(actualFloats[i], -1.0f, 1.0f);
                    var int16Sample = (short)(sample * short.MaxValue);
                    pcmData[i * 2] = (byte)(int16Sample & 0xFF);
                    pcmData[i * 2 + 1] = (byte)((int16Sample >> 8) & 0xFF);
                }

                return new DecodeResult(pcmData, DefaultSampleRate, AudioChannels.Stereo, true);
            }
            catch
            {
                pcmData.Dispose();
                throw;
            }
        }
        finally
        {
            if (mod != null)
            {
                NativeMethods.module_destroy(mod);
            }
        }
    }

    /// <summary>
    /// Returns true if the file extension indicates a supported tracker format.
    /// </summary>
    public static bool IsTrackerFormat(string extension)
    {
        var ext = extension.TrimStart('.').ToLowerInvariant();
        return ext is "mod" or "xm" or "it" or "s3m" or "mptm" or "stm" or "nst"
                    or "mtm" or "669" or "amf" or "ams" or "dbm" or "digi"
                    or "dmf" or "dsm" or "far" or "gdm" or "ice" or "imf"
                    or "j2b" or "m15" or "mdl" or "med" or "mo3" or "mt2"
                    or "okt" or "plm" or "psm" or "pt36" or "ptm" or "sfx"
                    or "sfx2" or "st26" or "ult" or "wow" or "dSm" or "symmod";
    }
}