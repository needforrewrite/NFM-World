namespace NFMWorld.DriverInterface.DriverInterface;

[ClientOnly]
public interface IRadicalMusic : IDisposable
{
    static float CurrentVolume = 0.8f;

    void SetPaused(bool p0);
    void Play();
    void SetVolume(float vol);
    float GetVolume();
    public void SetFreqMultiplier(double multiplier);
}