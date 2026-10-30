using NFMWorld.PluginSystem;

namespace NFMWorldLibrary;

[PluginVisible]
public enum RaceState : byte
{
    WaitingToStart,
    FailedToStart,
    InProgress,
    Paused,
    Finished
}