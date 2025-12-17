using ManagedBass;

namespace NFMWorld.SkiaDriver;

using System.IO.Compression;
using ManagedBass.Fx;
using NFMWorld.DriverInterface;
using NFMWorld.Mad;
using File = Util.File;

internal class RadicalMusic : IRadicalMusic
{
    private bool _readable;
    private readonly int _music;

    public RadicalMusic(File file, double tempomul)
    {
#if USE_BASS
        try
        {
            using var fileStream = System.IO.File.OpenRead(file.Path);
            using var resultStream = new MemoryStream();

            if(file.Path.EndsWith("mp3"))
            {
                byte[] f = System.IO.File.ReadAllBytes(file.Path);

                if ((_music = Bass.CreateStream(f, 0, f.Length, BassFlags.Loop)) == 0)
                {
                    // it ain't playable
                    throw new Exception(SoundClip.GetBassError(Bass.LastError));
                }  
            } else if(file.Path.EndsWith("zipo")
                || file.Path.EndsWith("radq")
                || file.Path.EndsWith("zip"))
            {
                using var zipStream = new ZipArchive(fileStream, ZipArchiveMode.Read);

                zipStream.Entries.First().Open().CopyTo(resultStream);
                var arr = resultStream.ToArray();

                if ((_music = Bass.MusicLoad(arr, 0, arr.Length, BassFlags.Loop | BassFlags.Decode)) == 0)
                {
                    // it ain't playable
                    throw new Exception(SoundClip.GetBassError(Bass.LastError));
                }  
            } else
            {
                byte[] f = System.IO.File.ReadAllBytes(file.Path);

                if ((_music = Bass.MusicLoad(f, 0, f.Length, BassFlags.Loop)) == 0)
                {
                    // it ain't playable
                    throw new Exception(SoundClip.GetBassError(Bass.LastError));
                }  
            }

            Bass.Configure(Configuration.PlaybackBufferLength, 1000);
            _music = BassFx.TempoCreate(_music, BassFlags.Loop);
            Bass.ChannelSetAttribute(_music, ChannelAttribute.Tempo, tempomul);

            _readable = true;
            //SetVolume(GameSparker.Volume);
            SetVolume(1.0f);   
        } catch(Exception e)
        {
            GameSparker.Writer.WriteLine("Error loading music " + file.Path + ": " + e.ToString());
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
        if (!_readable) return;
        Bass.ChannelSetAttribute(_music, ChannelAttribute.Volume, vol);
#endif
    }

    public void SetFreqMultiplier(double multiplier)
    {
        // we allow people to set this in the file so apply some bounds
        multiplier = Math.Clamp(multiplier, 0.50, 2.0);

        double s = Bass.ChannelGetAttribute(_music, ChannelAttribute.Frequency);
        Bass.ChannelSetAttribute(_music, ChannelAttribute.Frequency, s * multiplier);
    }
}