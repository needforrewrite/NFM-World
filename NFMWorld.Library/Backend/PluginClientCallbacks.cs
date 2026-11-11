using NFMWorld.PluginSystem;

namespace NFMWorldLibrary.Backend;

/// <summary>Exposes rendering callbacks through trusted library methods for sandboxed plugins.</summary>
[PluginVisible]
public sealed class PluginClientCallbacks(IClientCallbacks callbacks) : IClientCallbacks
{
    [PluginVisible]
    public void RaceRestarted() => callbacks.RaceRestarted();

    [PluginVisible]
    public void ResetCheckpointGlow() => callbacks.ResetCheckpointGlow();

    [PluginVisible]
    public void UpdateCheckpointGlow(ushort currentCheckpoint, bool isFinish) =>
        callbacks.UpdateCheckpointGlow(currentCheckpoint, isFinish);

    [PluginVisible]
    public IClientCarCallbacks GetClientCarCallbacks(BackendCar car) =>
        new PluginCarCallbacks(callbacks.GetClientCarCallbacks(car));

    [PluginVisible]
    public void TimeTrialCompleted(TimeTrialCompletion completion) => callbacks.TimeTrialCompleted(completion);
}

[PluginVisible]
public sealed class PluginCarCallbacks(IClientCarCallbacks callbacks) : IClientCarCallbacks
{
    [PluginVisible]
    public bool CastsShadow
    {
        get => callbacks.CastsShadow;
        set => callbacks.CastsShadow = value;
    }

    [PluginVisible]
    public bool? GetsShadowed
    {
        get => callbacks.GetsShadowed;
        set => callbacks.GetsShadowed = value;
    }

    [PluginVisible]
    public float? AlphaOverride
    {
        get => callbacks.AlphaOverride;
        set => callbacks.AlphaOverride = value;
    }

    [PluginVisible]
    public bool? Glow
    {
        get => callbacks.Glow;
        set => callbacks.Glow = value;
    }

    [PluginVisible]
    public bool? Finish
    {
        get => callbacks.Finish;
        set => callbacks.Finish = value;
    }
}
