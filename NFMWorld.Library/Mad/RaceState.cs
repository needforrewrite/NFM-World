namespace NFMWorldLibrary;

public enum RaceState : byte
{
    WaitingToStart,
    FailedToStart,
    InProgress,
    Paused,
    Finished
}