namespace NFMWorld.Audio;

public static class SoundEffects
{
    public static float Volume { get; private set; } = 1f;

    public static void SetVolume(float volume)
    {
        Volume = volume;
        if (OperatingSystem.IsBrowser())
            WebAudioBackend.SetAllVolumes(volume);
        else
            FaudioSoundClip.SetAllVolumes(volume);
    }

    public static void SetPaused(bool paused)
    {
        if (OperatingSystem.IsBrowser())
            WebAudioSoundClip.SetAllPaused(paused);
        else
            FaudioSoundClip.SetAllPaused(paused);
    }
}
