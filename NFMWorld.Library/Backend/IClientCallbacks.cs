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
/// A HUD widget a gamemode wants mounted. The gamemode supplies a label and a 0..1 value
/// accessor; the default HUD skins build and place the node at the named anchor. Keeping the
/// drawing host-side is what lets a sandboxed plugin declare UI without referencing Sx.
/// </summary>
[PluginVisible]
[method: PluginVisible]
public sealed record GamemodeHudWidget(
    /// <summary>Anchor slot in the HUD layout; see <see cref="GamemodeHudAnchor"/>.</summary>
    string Anchor,
    /// <summary>Short label, e.g. "BOOST".</summary>
    string Label,
    /// <summary>Current fill, 0..1. Read every frame; the host clamps it.</summary>
    Func<float> Value
);

/// <summary>The anchor positions the default HUD skins offer a gamemode widget.</summary>
[PluginVisible]
public static class GamemodeHudAnchor
{
    /// <summary>Top-left, under the lap/power panel (World) or under the lap text (Classic).</summary>
    public const string TopLeft = "topleft";

    /// <summary>Top-centre, under the arrow (World) or the lap time (Classic).</summary>
    public const string TopCenter = "topcenter";

    /// <summary>Top-right, under the standings sidebar (World) or the meters (Classic).</summary>
    public const string TopRight = "topright";

    /// <summary>Bottom-centre, above the speed readout.</summary>
    public const string BottomCenter = "bottomcenter";
}

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
    /// Declares the HUD widgets this gamemode wants mounted. Called once when the HUD builds
    /// (and again when it re-mounts); null or an empty list mounts nothing.
    /// </summary>
    IReadOnlyList<GamemodeHudWidget>? GetHudWidgets() => null;

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
