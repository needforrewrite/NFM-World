namespace NFMWorld.DriverInterface;

public interface IRadicalMusic
{
    static float CurrentVolume = 0.8f;

    void SetPaused(bool p0);
    void Unload();
    void Play();
    void SetVolume(float vol);
    float GetVolume();
    public void SetFreqMultiplier(double multiplier);
}