using NFMWorld.PluginSystem;

namespace NFMWorldLibrary.Backend;

/// <summary>
/// A finished local time-trial run, copied out of the gamemode so it survives a restart or the
/// gamemode's disposal. <see cref="Recording"/> is the exact bytes a submission would send.
/// </summary>
[PluginVisible]
[method: PluginVisible]
public sealed record TimeTrialCompletion(
    string StageName,
    string CarName,
    long FinishTicks,
    int ReplayVersion,
    byte[] Recording
);

/// <summary>
/// Functions that the gamemode is allowed to call on the client.
/// </summary>
[PluginVisible(Visible.IncludingAllMembersOfType)]
public interface IClientCallbacks
{
    void RaceRestarted()
    {
    }

    void ResetCheckpointGlow();
    void UpdateCheckpointGlow(ushort currentCheckpoint, bool isFinish);
    IClientCarCallbacks GetClientCarCallbacks(BackendCar car);

    /// <summary>
    /// Called once for each run that actually reached the finish. The host decides whether it can
    /// be submitted; headless validation and replay playback keep the no-op default.
    /// </summary>
    void TimeTrialCompleted(TimeTrialCompletion completion)
    {
    }

    /// <summary>Takes ownership of a finished recording that will no longer receive ticks.</summary>
    void TimeTrialCompleted(NFMWorldLibrary.Files.SavedTimeTrial recording, bool saveBest)
    {
        var bytes = recording.ToBytes();
        if (saveBest)
            TheVFS.VFS.WriteAllBytesAtomic(
                NFMWorldLibrary.Files.SavedTimeTrial.GetPathName(recording.CarName, recording.StageName),
                bytes
            );
        TimeTrialCompleted(new TimeTrialCompletion(
            recording.StageName,
            recording.CarName,
            recording.DemoData.Ticks.Count,
            recording.Version ?? 0,
            bytes
        )
        );
    }
}
