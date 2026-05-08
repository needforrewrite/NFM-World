using System.IO.Compression;
using ManagedBass;
using ManagedBass.Fx;
using NFMWorld.DriverInterface;
using NFMWorldLibrary;

namespace NFMWorld.SkiaDriver;

internal class RadicalMusic : IRadicalMusic
{
    private bool _readable;
    private readonly int _music;
    
#if USE_BASS
    private static int CreateHandle(ReadOnlySpan<byte> data, string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".mp3" or ".ogg" or ".wav" or ".aiff" => BassEx.CreateStream(data, BassFlags.Loop | BassFlags.Decode),
            ".opus" => BassEx.OpusCreateStream(data, BassFlags.Loop | BassFlags.Decode),
            _ => BassEx.MusicLoad(data, BassFlags.Loop | BassFlags.Decode)
        };
    }
#endif

    public RadicalMusic(string file, double tempomul)
    {
#if USE_BASS
        try
        {
            using var fileStream = VFS.OpenRead(file);

            if(file.EndsWith("zipo") || file.EndsWith("radq") || file.EndsWith("zip"))
            {
                using var zipStream = new ZipArchive(fileStream, ZipArchiveMode.Read);
                using var resultStream = new MemoryStream();

                var entry = zipStream.Entries.First();
                entry.Open().CopyTo(resultStream);
                var arr = resultStream.GetBuffer().AsSpan(0, (int)resultStream.Length);

                if ((_music = CreateHandle(arr, entry.FullName)) == 0)
                {
                    // it ain't playable
                    throw new Exception(SoundClip.GetBassError(Bass.LastError));
                }
            }
            else
            {
                var ms = new MemoryStream();
                fileStream.CopyTo(ms);
                var span = ms.GetBuffer().AsSpan(0, (int)ms.Length);

                if ((_music = CreateHandle(span, file)) == 0)
                {
                    // it ain't playable
                    throw new Exception(SoundClip.GetBassError(Bass.LastError));
                }
            }

            Bass.Configure(Configuration.PlaybackBufferLength, 1000);
            _music = BassFx.TempoCreate(_music, BassFlags.Loop);
            Bass.ChannelSetAttribute(_music, ChannelAttribute.Tempo, tempomul);

            _readable = true;
        }
        catch(Exception e)
        {
            SentrySdk.CaptureException(e);
            Logging.Error($"Error loading music {file}: {e}");
        }
#endif
    }

    public RadicalMusic()
    {
        // empty
    }

    public void SetPaused(bool p0)
    {
#if USE_BASS
        if (!_readable) return;
        if (p0) Bass.ChannelPause(_music);
        else Bass.ChannelPlay(_music);
#endif
    }

    public void Unload()
    {
#if USE_BASS
        if (!_readable) return;
        Bass.ChannelStop(_music);
        Bass.MusicFree(_music);
        _readable = false;
#endif
    }

    public void Play()
    {
#if USE_BASS
        if (!_readable) return;
        Bass.ChannelPlay(_music);
#endif
    }

    public void SetVolume(float vol)
    {
#if USE_BASS
        IRadicalMusic.CurrentVolume = vol;
        if (!_readable) return;
        Bass.ChannelSetAttribute(_music, ChannelAttribute.Volume, vol);
#endif
    }

    public float GetVolume()
    {
        if (!_readable) return 0f;
        return (float)Bass.ChannelGetAttribute(_music, ChannelAttribute.Volume);
    }

    public void SetFreqMultiplier(double multiplier)
    {
        // we allow people to set this in the file so apply some bounds
        multiplier = Math.Clamp(multiplier, 0.50, 2.0);

        double s = Bass.ChannelGetAttribute(_music, ChannelAttribute.Frequency);
        Bass.ChannelSetAttribute(_music, ChannelAttribute.Frequency, s * multiplier);
    }
}