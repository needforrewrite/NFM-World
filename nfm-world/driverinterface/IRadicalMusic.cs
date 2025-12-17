namespace NFMWorld.DriverInterface;

public interface IRadicalMusic
{
    void SetPaused(bool p0);
    void Unload();
    void Play();
    void SetVolume(float vol);
    public void SetFreqMultiplier(double multiplier);
}